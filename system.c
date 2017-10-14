/*
   Copyright (c) 2014, Intel Corporation
   All rights reserved.
  
   Redistribution and use in source and binary forms, with or without
   modification, are permitted provided that the following conditions are met:
 
       * Redistributions of source code must retain the above copyright
         notice, this list of conditions and the following disclaimer.
       * Redistributions in binary form must reproduce the above copyright
         notice, this list of conditions and the following disclaimer in the
         documentation and/or other materials provided with the distribution.
       * Neither the name of Intel Corporation nor the names of its
         contributors may be used to endorse or promote products derived from
         this software without specific prior written permission.
   
   THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
   AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
   IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
   ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
   LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR 
   CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF 
   SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
   INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
   CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
   ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
   POSSIBILITY OF SUCH DAMAGE.
 */

/* Written by: Jisoo Yang <jisoo.yang (at) unlv.edu> */

#define _GNU_SOURCE
#include <stdlib.h>
#include <stdio.h>
#include <string.h>
#include <math.h>
#include <inttypes.h>
#include <errno.h>


#ifdef _WIN32
#include <memory.h>
#include <io.h>
#include <windows.h>
#include <rpcdce.h> /* for UuidCreate */
#include "argp.h"
#else
#include <sys/mman.h>
#include <unistd.h>
#include <argp.h>
#include <sys/utsname.h>
#include <time.h>
#include <uuid/uuid.h>
#endif

#include "system.h"

#include "rdtsc.h"
#include "cpuid.h"
#include "pattern.h"
#include "pmbench.h"

/*
 * for leaf with ecx subleaf, this structure only caches ECX=0 leaf. 
 */
struct cpuid_struct {
    uint32_t leaf_pop, leaf_ex_pop;
    struct {
    	uint32_t r[4];
    } leaf[32], leaf_ex[10];
}; 

static struct cpuid_struct _cpuid = {
    .leaf_pop = 0u,
    .leaf_ex_pop = 0u,
};


static
void cpuid_populate_local_leaf(uint32_t idx)
{
    idx &= 0x0f;
    static const int A = 0, B = 1, C = 2, D = 3;
    uint32_t *r = _cpuid.leaf[idx].r;

    r[A] = idx;
    r[C] = 0;
    _sys_cpuid(&r[A], &r[B], &r[C], &r[D]);
    _cpuid.leaf_pop |= (1u << idx);
}

static inline
int is_leaf_supported_idx(uint32_t idx)
{
    uint32_t eax = idx & 0xff;

    if (eax >= 0x18) return 0;	    /* as of IDM Sep 2016 */
    if (!(_cpuid.leaf_pop & 0x01)) cpuid_populate_local_leaf(0);

    if (_cpuid.leaf[0].r[0] < eax) return 0;

    return 1;
}

static
void cpuid_populate_local_leaf_ex(uint32_t idx)
{
    static const int A = 0, B = 1, C = 2, D = 3;
    uint32_t *r = _cpuid.leaf_ex[idx & 0x0f].r;

    r[A] = 0x80000000 | idx;
    r[C] = 0;
    _sys_cpuid(&r[A], &r[B], &r[C], &r[D]);
    _cpuid.leaf_ex_pop |= (1u << (idx & 0x0f));
}

/*
 * ex_idx can be specified either as 0x800000idx or idx
 */
static
int is_leaf_ex_supported_idx(uint32_t ex_idx)
{
    uint32_t eax = 0x80000000u | ex_idx;
    ex_idx &= 0x0f; 

    if (eax >= 0x80000009) return 0;  /* as of IDM Sep 2016 */
    if (!(_cpuid.leaf_ex_pop & 0x01)) cpuid_populate_local_leaf_ex(0);

    if (_cpuid.leaf_ex[0].r[0] < eax) return 0;

    return 1;
}

/****************************/

int is_rdtscp_available(void)
{
    if (!is_leaf_ex_supported_idx(1)) return 0;

    if (!(_cpuid.leaf_ex_pop & (1 << 1))) {
	cpuid_populate_local_leaf_ex(1);
    }
    
    /* bit 27 of EDX */
    if (_cpuid.leaf_ex[1].r[3] & (1u << 27)) return 1;
    return 0;
}

int is_tsc_invariant(void)
{
    if (!is_leaf_ex_supported_idx(7)) return 0;

    if (!(_cpuid.leaf_ex_pop & (1 << 7))) {
	cpuid_populate_local_leaf_ex(7);
    }
    /* bit 8 of EDX */
    if (_cpuid.leaf_ex[7].r[3] & 0x100) return 1;
    return 0;
}

/* 
 * when detected, returns string length, including the null character.
 * This functions strips away the leading white spaces.
 * returns 0 when string is unsupported.
 */
int __cpuid_obtain_brand_string(char* buf)
{
    char* src, *buf_start = buf;

    if (!is_leaf_ex_supported_idx(4)) {
	buf[0] = 0;
	return 0;
    }
    if (!(_cpuid.leaf_ex_pop & (1 << 2))) cpuid_populate_local_leaf_ex(2);
    if (!(_cpuid.leaf_ex_pop & (1 << 3))) cpuid_populate_local_leaf_ex(3);
    if (!(_cpuid.leaf_ex_pop & (1 << 4))) cpuid_populate_local_leaf_ex(4);
    
    /*
     * We take advantage the way _cpuid is structured:
     * (at least x86 SYSV ABI dictates it)
     * the leaves and registers are adjacent in order so 
     * simple string copy (memcopy) 
     */
    src = (char*)_cpuid.leaf_ex[2].r;
    while (*src == ' ') src++;
    
    while ((*buf++ = *src++) != 0);
    return (int)(buf - buf_start);
}

/* @output must be at least 16 bytes long */
static inline 
int __cpuid_cache_tlb_info(uint8_t *output)
{
    static const int A = 0, B = 1, C = 2, D = 3;
    uint32_t r[4];
    uint8_t *c;
    uint8_t *save = output;
    int rep, i;

    c = (uint8_t *)r;

    r[A] = 2; r[C] = 0;
    _sys_cpuid(&r[A], &r[B], &r[C], &r[D]);

    rep = (int)r[A] & 0xFF;

    do {
	for (i = 1; i < 16; ++i) {
	    if (c[i]) *output++ = c[i];
	}
	rep--;
	if (!rep) break;
	r[A] = 2; r[C] = 0;
	_sys_cpuid(&r[A], &r[B], &r[C], &r[D]);
    } while (1);
    return (int)(output - save);
}

/* these are not zero-based values */
struct cpu_cache_info {
    uint32_t sets;
    short linesize;
    short partitions;
    short ways;
    short level;
    enum {
	DATACACHE = 1,
	INSTCACHE,
	UNICACHE
    } cachetype;
};

struct cpu_cache_info_ebx_encode {
    unsigned int z_linesize:12;
    unsigned int z_partition:10;
    unsigned int z_way:10;
};

/* 
 * structured cache info (EAX=0x04) 
 * returns cache items + 1
 */
static inline
int __cpuid_deterministic_cache_info(struct cpu_cache_info* output, int size)
{
    static const int A = 0, B = 1, C = 2, D = 3;
    uint32_t r[4];
    struct cpu_cache_info_ebx_encode* ebx_encode = (void*)&r[B];

    int item;

    for (item = 0; item < size; item++) {
	r[A] = 4; r[C] = item;
	_sys_cpuid(&r[A], &r[B], &r[C], &r[D]);
	if (!(r[A] & 0x1f)) return item;
	output[item].cachetype = r[A] & 0x1f;
	output[item].level = (r[A] >> 5) & 0x7;

	output[item].sets = r[C] + 1;
	output[item].linesize = ebx_encode->z_linesize + 1;
	output[item].partitions = ebx_encode->z_partition + 1;
	output[item].ways = ebx_encode->z_way + 1;
    }
    return item;
}


/*****************************************************/
uint8_t tlb_info_buf[16];
int gl_tlb_info_buf_len;

int print_tlb_info()
{
    int i, len = __cpuid_cache_tlb_info(tlb_info_buf);
    for (i = 0; i < len; i++) {
	switch (tlb_info_buf[i]) {
	    CPUID02_TLB_PRN();
	default:
	    break;
	}
    }
    gl_tlb_info_buf_len = len;
    return len;
}

uint8_t get_tlb_info(int i)
{
    return tlb_info_buf[i];
} //also need something for the switch statement

struct cpu_cache_info cash[8];
int gl_det_cache_info_len;

int print_cache_info(void)
{
    unsigned char buf[16];
    uint32_t capacity;
    int i, len, deterministic = 0;

    len = __cpuid_cache_tlb_info(buf);
    for (i = 0; i < len; i++) {
	switch (tlb_info_buf[i]) {
	    CPUID02_CACHE_PRN();
	case 0xFF:
	    deterministic = 1;
	    break;
	default:
	    break;
	}
    }
    if (!deterministic) return 0;

    memset(cash, 0, sizeof(cash));
    len = __cpuid_deterministic_cache_info(cash, 8);
    for (i = 0; i < len; i++) {
	switch (cash[i].cachetype) {
	case DATACACHE:
	    printf("DCACHE:");
	    break;
	case INSTCACHE:
	    printf("ICACHE:");
	    break;
	case UNICACHE:
	    printf("CACHE:");
	    break;
	default:
	    break;
	}
	printf(" lvl %d, ", cash[i].level);
	capacity = cash[i].sets * cash[i].linesize *
	    cash[i].partitions * cash[i].ways;
	printf("%d KB, ", capacity >> 10);
	printf("sets:%d, linesz:%d, part:%d, ways:%d\n",
		cash[i].sets, cash[i].linesize, cash[i].partitions, cash[i].ways);
    }
    gl_det_cache_info_len = len;
    return len;
}

char* get_cache_type(int i)
{
    switch (cash[i].cachetype) {
    case DATACACHE: return "DCACHE";
    case INSTCACHE: return "ICACHE";
    case UNICACHE: return "CACHE";
    default:
       printf("get_cache_type(%d): Error\n", i);
       return "ERRORCACHE";
    }
}

int get_cache_info(int i, int m)
{
    switch (m) {
    case 0: return cash[i].sets;
    case 1: return cash[i].linesize;
    case 2: return cash[i].partitions;
    case 3: return cash[i].ways;
    case 4: return cash[i].level;
    case 5: return ((cash[i].sets * cash[i].linesize * cash[i].partitions * cash[i].ways) >> 10);
    default:
	    printf("get_cache_info(%d, %d): Error\n", i, m);
	    return -1;
    }
}

/*
 * returns -1 if cpu model not found
 */
static 
uint32_t cpuid_cpu_model(uint32_t* out_fam)
{
    static const uint32_t LEAF = 0x1;
    static const int A = 0;

    uint32_t family, model;
    
    if (!is_leaf_supported_idx(LEAF)) return (uint32_t)(-1);

    if (!(_cpuid.leaf_pop & (1 << LEAF))) {
	cpuid_populate_local_leaf(LEAF);
    }
    
    union {
	struct verinfo {
	    uint32_t stepping :4;
	    uint32_t model    :4;
	    uint32_t family   :4;
	    uint32_t type     :2;
	    uint32_t res1     :2;
	    uint32_t ex_model :4;
	    uint32_t ex_family:8;
	    uint32_t res2     :4;
	} f;
	uint32_t val;
    } eax;
    eax.val = _cpuid.leaf[LEAF].r[A];
    /*
     * family/model id extraction rule as of IDM Sep 2016 
     */
    family = (eax.f.family != 0xf) ? eax.f.family : eax.f.family + eax.f.ex_family;
    if (eax.f.family == 0x6 || eax.f.family == 0xf) {
	model = (eax.f.ex_model << 4) + eax.f.model;
    } else model = eax.f.model;

    if (out_fam) *out_fam = family;
    return model;
}

static inline
uint32_t calculate_cpuid_freq(uint32_t a, uint32_t b, uint32_t c) {
    uint64_t val = (uint64_t)c * b / a / 1000;  /* report in KHz */
    return (uint32_t)val;
}

/*
 * returns cpuid-reported nominal time stamp counter frequency in KHz
 * returns 0 if unavailable
 */
static 
uint32_t get_tsc_freq_from_cpuid(void)
{
    static const uint32_t LEAF = 0x15;
    static const int A = 0, B = 1, C = 2;

    if (!is_leaf_supported_idx(LEAF)) return 0;

    if (!(_cpuid.leaf_pop & (1 << LEAF))) {
	cpuid_populate_local_leaf(LEAF);
    }
    
    /*
     * according to Section 18.18.3 of Sep 2016 Intel SDM, 
     * tsc freq = "core clock freq" * "ratio of TSC freq and core clock freq" 
     *          = ECX * (EBX / EAX) 
     * if (EBX/EAX) is present, but ECX is zero, then
     *    if 6th/7th gen non-xeon Core, use 24 MHz for ECX
     *    if Goldmont (CPUID 06_5CH), use 19.2 MHz for ECX
     */

    /*
     * we have to assume 6th gen core to be Skylake and 7th Gen to be Kabylake.
     * As of Dec 2016, I only know of 06_5E (Skylake) and 06_9E (Kabylake)
     */

    if (_cpuid.leaf[LEAF].r[A] == 0 || _cpuid.leaf[LEAF].r[B] == 0) return 0;
    if (_cpuid.leaf[LEAF].r[C] != 0) {
	return calculate_cpuid_freq(_cpuid.leaf[LEAF].r[A],
		_cpuid.leaf[LEAF].r[B],
		_cpuid.leaf[LEAF].r[C]);
    } else {
	uint32_t model,family;
	model = cpuid_cpu_model(&family);
	if (~model == 0) return 0;
	if (family == 0x6) {
	    if (model == 0x5E || model == 0x9E) {
		return calculate_cpuid_freq(_cpuid.leaf[LEAF].r[A],
			_cpuid.leaf[LEAF].r[B],
			24000*1000);
	    }
	    if (model == 0x5C) {
		return calculate_cpuid_freq(_cpuid.leaf[LEAF].r[A],
			_cpuid.leaf[LEAF].r[B],
			19200*1000);
	    }
	}
    }
    return 0;
}

/*
 * returns rdtsc frequency in KHz using MSR_PLATFORM_INFO[15:8] value
 * returns 0 if unavailable
 */
#define MSR_PLATFORM_INFO (0xCE)
#ifdef _WIN32
static 
uint32_t get_tsc_freq_from_msr(void)
{
    /* XXX */
    return 0;
}
#else
/*
 * process must have read privilege on /dev/cpu/0/msr.
 */
static 
uint32_t get_tsc_freq_from_msr(void)
{
    uint64_t msr_val;
    int fd;
    ssize_t ret;
    uint32_t family, model;

    fd = open("/dev/cpu/0/msr", O_RDONLY);
    if (fd < 0) return 0;
    
    ret = pread(fd, (void*)&msr_val, sizeof(uint64_t), MSR_PLATFORM_INFO);
    if (ret != sizeof(uint64_t)) return 0;

    msr_val = (msr_val & 0xffff) >> 8;

    model = cpuid_cpu_model(&family);
    if (~model == 0) return 0;
    /*
     * according to Intel SDM, use 100MHz for SNB, IVB, HSW, Broadwell,
     * use 133MHz for Nehalem, and other crazy rule for Atoms and Xeons which
     * we don't give a damn in here.. 
     */
    if (family != 0x6) return 0;
    if (model == 0x2A || // SNB
	    model == 0x3A || //IVB
	    model == 0x3C || // HSW
	    model == 0x45 || // HSW (Y/U)
	    model == 0x46 || // HSW (crystalwell)
	    model == 0x3D ) // Broadwell
    {
	return (uint32_t)(msr_val*(100*1000*1000/1000));
    }
    if (model == 0x1A || // Nehalem EP
	    0x1E || // Nehalem
	    0x2E ) // Nehalem EX
    {
	return (uint32_t)(msr_val*(133*1000*1000/1000));
    }
    return 0;
}
#endif

/*
 * obtain frequency rating from brand string prescribed by CPUID SDM
 */
static
uint32_t get_tsc_freq_from_brandstring(void)
{
    char brand[52];
    int len;
    char* cur;
    uint32_t mult;
    float num;

    len = __cpuid_obtain_brand_string(brand);
    if (len == 0) return 0;

    // scan backwards
    cur = &brand[len];
    while (cur >= brand) {
	if (*cur-- != 'z') continue;
	if (*cur-- != 'H') continue;
	if (*cur == 'G') {
	    mult = 1000*1000;
	    goto extract_number;
	} else if (*cur == 'M') {
	    mult = 1000;
	    goto extract_number;
	}
    }
    return 0;

extract_number:
    *cur-- = ' ';	// place white space for sscanf
    while (cur >= brand) {
	if (*cur-- == ' ') {
	    sscanf(cur+1, "%f", &num);
	    return (uint32_t)(num * mult);
	}
    }
    return 0;
}

/* 
 * returns frequency in KHz
 * methodology: time usleep duration with rdtsc.
 * we're relying on the CPU has constant rate timestamp counter. most modern
 * CPUs have them.  
 *
 * We use simple linear regression to offset error.
 *   y = ax + b   (x: usleep sec, y: tsc)
 * we have (x1, y1) and (x2, y2)
 * then b = (x2*y1)/(x2-x1) - (x1*y2)/(x2-x1)
 */
static
uint32_t measure_rdtsc_frequency(void)
{
    uint64_t tscA, tscB, y1, y2, offset;
    uint32_t ret, rdtsc_freq_measured;
    static const int x1 = 128, x2 = 256; // ~ms
    tscA = rdtsc();
    ret = usleep(x1*1024);
    tscB = rdtsc();
    if (ret) {
	printf("CPU frequency detection failed (sleep) Bailing out\n");
	return 0;
    }
    y1 = tscB - tscA;
    rdtsc_freq_measured = (uint32_t)((y1*1000ull)/(x1*1024));
    tscA = rdtsc();
    ret = usleep(x2*1024);
    tscB = rdtsc();
    if (ret) {
	printf("CPU frequency detection failed (sleep) Bailing out\n");
	return 0;
    }
    y2 = tscB - tscA;
    offset = ((y1*x2) - (y2*x1))/(x2-x1);
    rdtsc_freq_measured = (uint32_t)(((y1 - offset)*1000ull)/(x1*1024));
//printf("rdtsc_freq_measured: %d, offset: %lld\n", rdtsc_freq_measured, offset);
    return rdtsc_freq_measured;
}

#ifdef _WIN32
/*
   //Windows 'recommended' way of timestamping: 
   LARGE_INTEGER frequency, perfCount;
   QueryPerformanceFrequency(&frequency);
   QueryPerformanceCounter(&perfCount);
   start = perfCount.QuadPart;
   Sleep(1000);
   QueryPerformanceCounter(&perfCount);
   end = perfCount.QuadPart;
   elapsed = (double)(end - start) / frequency.QuadPart;
  */
uint32_t get_cycle_freq_fallback(void)
{
    LARGE_INTEGER i;
    uint32_t rdtsc_khz;

    /* Note on QueryPerformanceFrequency():
     * This function will usually return TSC counter frequency.
     * This is true on most modern x86 CPUs, and TSC counter frequency
     * can be equated to 'sticker' CPU frequency as TSC counter
     * frequency remains constant even though CPU frequency changes
     * due to frequency scaling/turbo boost/throttling.
     * 
     * The point is that for our pmbench purpose we use rdtsc to measure
     * wall clock time, so the TSC counter frequency returned by
     * this function perfectly suits our needs. (we're not measuring
     * CPU speed)
     *
     * N.B.: On systems lacking TSC counter, this function returns 
     * low (a few MHZ) number, which doesn't reflect CPU frequency.
     * (This is due to it reports frequncy of ACPI timer or even 8254 PIT)
     */
    if (!QueryPerformanceFrequency(&i)) {
        printf("QueryPerformanceFrequency failed!\n");
	return 0;
    }
    if (i.QuadPart < (1ll << 26)) { // should be greater than 64 MiHZ
        printf("QueryPerformanceFrequency() - too small at %"PRIu64" HZ\n", i.QuadPart);
	printf("Alternative: rdtsc-based:");
	rdtsc_khz = measure_rdtsc_frequency();
	printf("rdtsc frequency at %dKHZ\n", rdtsc_khz);
	return rdtsc_khz;
    } else {
	return (uint32_t)(i.QuadPart);   // perfFreq in Hz
    }
}
#else
uint32_t get_cycle_freq_fallback(void)
{
    /* methodology 1: grep for 'cpu MHz' line in /proc/cpuinfo */
    FILE* fp;
    static char buf[512];
    char* cursor;
    float cpumhz;
    uint32_t cpu_freq_reported, rdtsc_freq_measured;

    fp = fopen("/proc/cpuinfo", "r");
    if (!fp) {
	perror("Can't open /proc/cpuinfo - Bailing out");
	return 0;
    }
    fread(buf, 512, 1, fp);

    cursor = strstr(buf, "cpu MHz");
    cursor += 11; // consume "cpu MHz \t: "
    sscanf(cursor, "%f", &cpumhz);
    cpu_freq_reported = (uint32_t)(cpumhz * 1000.0f);

    if (cpu_freq_reported < 500000 || cpu_freq_reported > 5000000) {
	printf("CPU frequency detection failed. Bailing out\n");
	return 0;
    }

    rdtsc_freq_measured = measure_rdtsc_frequency();
    if (!rdtsc_freq_measured) {
	return cpu_freq_reported;
    }
    if (abs(cpu_freq_reported - rdtsc_freq_measured) / 1000 < 100 ) { // tolerance: 100Mhz
	return cpu_freq_reported;
    } else {
	return rdtsc_freq_measured;
    }
}
#endif

/*
 * test rdtsc with os timer and see if it falls within torerance
 * returns 1 upon success, 0 upon failure
 */
static
int validate_freq(uint32_t freq_khz) 
{
    uint32_t rdtsc_freq_measured;

    rdtsc_freq_measured = measure_rdtsc_frequency();

    if (!rdtsc_freq_measured) return 0;
    
    // tolerance: 100Mhz
    if (abs(freq_khz - rdtsc_freq_measured) / 1000 < 100 ) {
	return 1;
    } else {
	printf("rdtsc frequency validation failed. Trying different method\n");
	return 0;
    }
}

/*
 * returns timestamp counter (rdtsc) frequency in KHz
 */
uint32_t get_cycle_freq(void)
{
    uint32_t freq_khz;
    /* 
     * SDM recommends using CPUID tsc freq method over MSR_PLATFORM_INFO
     */
    freq_khz = get_tsc_freq_from_cpuid();
    if (freq_khz) {
	if (validate_freq(freq_khz)) return freq_khz;
    }
    freq_khz = get_tsc_freq_from_msr();
    if (freq_khz) {
	if (validate_freq(freq_khz)) return freq_khz;
    }
    freq_khz = get_tsc_freq_from_brandstring();
    if (freq_khz) {
	if (validate_freq(freq_khz)) return freq_khz;
    }

    return get_cycle_freq_fallback();
}

static
_code
uint64_t _ops_rdtsc(void)
{
    return rdtsc();
}

/*
 * returns 0 upon success, non zero upon failure
 */
int _ops_rdtsc_init_base_freq(struct sys_timestamp* sts)
{
    //uint32_t freq_khz = measure_rdtsc_frequency();
    uint32_t freq_khz = get_cycle_freq();
    if (!freq_khz) return -1;
    sts->base_freq_khz = freq_khz;
    return 0;
}

/* timestamp measure based on rdtsc */
struct sys_timestamp rdtsc_ops = {
    .timestamp = _ops_rdtsc,
    .init_base_freq = _ops_rdtsc_init_base_freq,
    .base_freq_khz = 0,
    .name = "rdtsc",
};

static
_code
uint64_t _ops_rdtscp(void)
{
    return rdtscp();
}

/*
 * returns 0 upon success, non zero upon failure
 */
int _ops_rdtscp_init_base_freq(struct sys_timestamp* sts)
{
    //uint32_t freq_khz = measure_rdtsc_frequency();
    uint32_t freq_khz = get_cycle_freq();
    if (!freq_khz) return -1;
    sts->base_freq_khz = freq_khz;
    return 0;
}

/* timestamp measure based on rdtscp */
struct sys_timestamp rdtscp_ops = {
    .timestamp = _ops_rdtscp,
    .init_base_freq = _ops_rdtsc_init_base_freq,
    .base_freq_khz = 0,
    .name = "rdtscp",
};

#ifdef _WIN32
uint64_t _ops_perfc(void)
{
    LARGE_INTEGER counter;
    QueryPerformanceCounter(&counter);
    return counter.QuadPart << 10;
}

int _ops_perfc_init_base_freq(struct sys_timestamp* sts)
{
    LARGE_INTEGER frequency;
    if (!QueryPerformanceFrequency(&frequency)) return -1;
    sts->base_freq_khz = frequency.QuadPart;
    return 0;
}

/* timestamp measure based on QueryPerformance call */ 
struct sys_timestamp perfc_ops = {
    .timestamp = _ops_perfc,
    .init_base_freq = _ops_perfc_init_base_freq,
    .base_freq_khz = 0,
    .name = "perfc",
};
#else 
// XXX for now perfc_ops is identical to rdtsc_ops
/*
struct sys_timestamp perfc_ops = {
    .timestamp = _ops_rdtsc,
    .init_base_freq = _ops_rdtsc_init_base_freq,
    .base_freq_khz = 0,
    .name = "perfc",
};
*/
#endif

static struct sys_timestamp* all_sys_timestamp[] = {
	&rdtsc_ops, &rdtscp_ops, 
#ifdef _WIN32
	&perfc_ops, 
#endif
   0
};

struct sys_timestamp* get_timestamp_from_name(const char* str)
{
    int i = 0;
    if (!str) return 0;
    while (all_sys_timestamp[i]) {
	if (!my_strncmp(all_sys_timestamp[i]->name, str, 16))
	    return all_sys_timestamp[i];
	i++;
    }
    return 0;
}

/*
 * return non-zero upon error
 */
#ifdef _WIN32
int sys_stat_mem_init(sys_mem_ctx* ctx)
{
    return 0;
}

/*
 * sys_stat_mem_update()
 * get system memory stat update
 */
int sys_stat_mem_update(sys_mem_ctx* ctx, sys_mem_item* info)
{
    info->memstatex.dwLength = sizeof(info->memstatex);

    GlobalMemoryStatusEx(&info->memstatex);
    
    info->recorded = 1;
    return 0;
}

/*
 *  clean up memory stat metainfo
 */
int sys_stat_mem_exit(sys_mem_ctx* ctx)
{
    return 0;
}

void
__attribute__((cold))
sys_stat_mem_print_header(void)
{
    printf("            free(K) in_use(%%) pgfile(K) avail_pgfile(K) avail_virt(K)\n");
}

void
__attribute__((cold))
sys_stat_mem_print(const sys_mem_item* info)
{
   const MEMORYSTATUSEX* stat = &info->memstatex;
	printf("%8"PRIu64" %8"PRId32" %10"PRIu64" %13"PRIu64" %12"PRIu64"\n",
	    	stat->ullAvailPhys/1000, (int)stat->dwMemoryLoad, 
	    	stat->ullTotalPageFile/1000, stat->ullAvailPageFile/1000,
            	stat->ullAvailVirtual/1000); 
}

int64_t sys_stat_mem_get(const sys_mem_item *info, int i)
{
    const MEMORYSTATUSEX* stat = &info->memstatex;
    switch (i) {
    case (0): return stat->ullAvailPhys/1000;
    case (1): return (int)stat->dwMemoryLoad;
    case (2): return stat->ullTotalPageFile/1000;
    case (3): return stat->ullAvailPageFile/1000;
    case (4): return stat->ullAvailVirtual/1000;
    default:
	  printf("sys_stat_mem_get(%p, %d): Error\n", info, i);
	  return 0;
    }
}

void 
__attribute__((cold))
sys_stat_mem_print_delta(const sys_mem_item* before, const sys_mem_item* after)
{
    const MEMORYSTATUSEX* stat_b = &before->memstatex;
    const MEMORYSTATUSEX* stat_a = &after->memstatex;
    printf("%8"PRId64" %8"PRId32" %10"PRId64" %13"PRId64" %12"PRId64"\n",
	    (int64_t)(stat_a->ullAvailPhys - stat_b->ullAvailPhys)/1000, 
	    (int)(stat_a->dwMemoryLoad - stat_b->dwMemoryLoad), 
	    (int64_t)(stat_a->ullTotalPageFile - stat_b->ullTotalPageFile)/1000, 
	    (int64_t)(stat_a->ullAvailPageFile - stat_b->ullAvailPageFile)/1000, 
	    (int64_t)(stat_a->ullAvailVirtual - stat_b->ullAvailVirtual)/1000); 
}

int64_t sys_stat_mem_get_delta(const sys_mem_item* before, const sys_mem_item* after, int i)
{
    const MEMORYSTATUSEX* stat_b = &before->memstatex;
    const MEMORYSTATUSEX* stat_a = &after->memstatex;
    switch (i) {
    case (0): return (int64_t)(stat_a->ullAvailPhys - stat_b->ullAvailPhys)/1000;
    case (1): return (int)(stat_a->dwMemoryLoad - stat_b->dwMemoryLoad);
    case (2): return (int64_t)(stat_a->ullTotalPageFile - stat_b->ullTotalPageFile)/1000;
    case (3): return (int64_t)(stat_a->ullAvailPageFile - stat_b->ullAvailPageFile)/1000;
    case (4): return (int64_t)(stat_a->ullAvailVirtual - stat_b->ullAvailVirtual)/1000;
    default:
	printf("sys_stat_mem_get_delta(%p, %p, %d): Error\n", before, after, i);
	return 0;
    }
}
#else
int sys_stat_mem_init(sys_mem_ctx* ctx)
{
    int r1, r2;

    r1 = open("/proc/meminfo", O_RDONLY);
    r2 = open("/proc/vmstat", O_RDONLY);

    if (r1 == -1 || r2 == -1) {
	if (r1 != -1) close(r1);
	if (r2 != -1) close(r2);
	fprintf(stderr, "%s: failed to open proc entry\n", __func__);
	return -1;
    }
    ctx->fd_meminfo = r1;
    ctx->fd_vmstat = r2;
    
    return 0;
}

/*
 * returns parsed out integer value from a meminfo line
 * @buf points to the start of the 28-byte lengh meminfo line
 */
static inline
int meminfo_get_line(const char* buf) {
    const char* pos = strchr(buf, ':');
    return atoi(pos + 1);
}

int sys_stat_mem_update(sys_mem_ctx* ctx, sys_mem_item* info)
{
#define BUF_SIZE 2048
    static const int linelen = 28; // meminfo line length

    static char buf_meminfo[BUF_SIZE];
    static char buf_vmstat[BUF_SIZE];
    char* pos;
    int n;

    n = pread(ctx->fd_meminfo, buf_meminfo, BUF_SIZE, 0);
    if (n == -1) return -1;
    n = pread(ctx->fd_vmstat, buf_vmstat, BUF_SIZE, 0);
    if (n == -1) return -1;
    
    // meminfo
    pos = buf_meminfo;
    info->total_kib = meminfo_get_line(pos); pos += linelen;
    info->free_kib = meminfo_get_line(pos); pos += linelen;
    info->buffer_kib = meminfo_get_line(pos); pos += linelen;
    info->cache_kib = meminfo_get_line(pos); pos += 2*linelen;
    info->active_kib = meminfo_get_line(pos); pos += linelen;
    info->inactive_kib = meminfo_get_line(pos);

    // vmstat
    pos = strstr(buf_vmstat, "pgpgin");
    info->pgpgin = atoll(pos + 7);
    pos = strchr(pos + 7, '\n') + 1;
    info->pgpgout = atoll(pos + 8);
    pos = strchr(pos + 8, '\n') + 1;
    info->pswpin = atoll(pos + 7);
    pos = strchr(pos + 7, '\n') + 1;
    info->pswpout = atoll(pos + 8);
    pos = strstr(pos + 8, "pgmajfault");
    info->pgmajfault = atoll(pos + 11);
    
    info->recorded = 1;
    return 0;
#undef BUF_SIZE
}

int sys_stat_mem_exit(sys_mem_ctx* ctx)
{
    close(ctx->fd_meminfo);
    close(ctx->fd_vmstat);
    return 0;
}

void sys_stat_mem_print_header(void)
{
    printf("            free(K) buffer(K) cache(K) active(K) inactv(K)"
	   " pgpgin   pgpgout    pswpin   pswpout pgmajfaut\n");
}

void sys_stat_mem_print(const sys_mem_item* info)
{
    printf("%8d %8d %8d %8d %8d %9"PRId64" %9"PRId64" %9"PRId64" %9"PRId64" %9"PRId64"\n",
	    info->free_kib, info->buffer_kib,
	    info->cache_kib, info->active_kib, info->inactive_kib,
	    info->pgpgin, info->pgpgout, info->pswpin,
	    info->pswpout, info->pgmajfault);
}

int64_t sys_stat_mem_get(const sys_mem_item *info, int i)
{
    switch (i) {
	case (0): return info->free_kib;
	case (1): return info->buffer_kib;
	case (2): return info->cache_kib;
	case (3): return info->active_kib;
	case (4): return info->inactive_kib;
	case (5): return info->pgpgin;
	case (6): return info->pgpgout;
	case (7): return info->pswpin;
	case (8): return info->pswpout;
	case (9): return info->pgmajfault;
	default:
	  printf("sys_stat_mem_get(%p, %d) Error\n", info, i);
	  return 0;
    }
}

int64_t sys_stat_mem_get_delta(const sys_mem_item* before, const sys_mem_item* after, int i)
{
    switch (i) {
	case (0): return after->free_kib - before->free_kib;
	case (1): return after->buffer_kib - before->buffer_kib;
	case (2): return after->cache_kib - before->cache_kib;
	case (3): return after->active_kib - before->active_kib;
	case (4): return after->inactive_kib - before->inactive_kib;
	case (5): return after->pgpgin - before->pgpgin;
	case (6): return after->pgpgout -  before->pgpgout;
	case (7): return after->pswpin - before->pswpin;
	case (8): return after->pswpout - before->pswpout;
	case (9): return after->pgmajfault - before->pgmajfault;
	default:
	    printf("sys_stat_mem_get_delta(%p, %p, %d): Error\n", before, after, i);
	    return 0;
    }
}

void sys_stat_mem_print_delta(const sys_mem_item* before, const sys_mem_item* after)
{

    printf("%8d %8d %8d %8d %8d %9"PRId64" %9"PRId64" %9"PRId64" %9"PRId64" %9"PRId64"\n",
	after->free_kib - before->free_kib, after->buffer_kib - before->buffer_kib,
	after->cache_kib - before->cache_kib, after->active_kib - before->active_kib,
	after->inactive_kib - before->inactive_kib,
	after->pgpgin - before->pgpgin, after->pgpgout -  before->pgpgout, 
	after->pswpin - before->pswpin,
	after->pswpout - before->pswpout, after->pgmajfault - before->pgmajfault);
}

#endif


void sys_print_pmbench_info(void) 
{ 
    printf("pmbench version: %s\n", argp_program_version); 
}

#ifdef _WIN32
#define NAME_BUF_SIZE 64

DWORD os_version;
TCHAR infBuf[NAME_BUF_SIZE];
char* sys_print_hostname(void)
{
    os_version = GetVersion();
    DWORD bufCharCount = NAME_BUF_SIZE;
    if (!GetComputerName(infBuf, &bufCharCount)) {
	printf("Hostname unknown.\n");
	return "unknown";
    } else printf("Hostname       : %s\n", infBuf);
    return infBuf;
}

char* sys_get_hostname(void)
{
    os_version = GetVersion();
    DWORD bufCharCount = NAME_BUF_SIZE;
    if (!GetComputerName(infBuf, &bufCharCount)) {
	return "unknown";
    } 
    return infBuf;
}

int sys_get_os_version_value(int i)
{
    switch (i) {
	case 1: return (int)(LOBYTE(LOWORD(os_version)));
	case 2: return (int)(HIBYTE(LOWORD(os_version)));
	case 3:
	    if (os_version < 0x80000000) {
		return (int)(HIWORD(os_version));
	    } else {
		return 0;
	    }
	default: return 0;
    }
}

char* sys_get_cpu_arch(void)
{
    SYSTEM_INFO sysinfo;
    GetNativeSystemInfo(&sysinfo);
    switch (sysinfo.wProcessorArchitecture) {
	case 9: return "x64";
	case 6: return "Itanium";
	case 0: return "x86";
	default: return "unknown";
    }
}

#ifndef LOCALE_INVARIANT
#define LOCALE_INVARIANT 0x007f
#endif
int gl_goodtime = 0;
int getDateFormat_ret;
char time_strbuf[64], date_strbuf[64], year_strbuf[8];

int sys_print_time_info(void)
{
    SYSTEMTIME systime;
    GetLocalTime(&systime);
    getDateFormat_ret = GetDateFormat(LOCALE_INVARIANT, 
	    0, &systime, "ddd MMM dd", date_strbuf, 64);
    if (!getDateFormat_ret) {
	printf("date/time failed\n");
	return 0;
    }
    getDateFormat_ret = GetDateFormat(LOCALE_INVARIANT, 
	    0, &systime, "yyyy", year_strbuf, 8);
    if (!getDateFormat_ret) {
	printf("date/time failed\n");
	return 0;
    }
    getDateFormat_ret = GetTimeFormat(LOCALE_INVARIANT, 
	    TIME_FORCE24HOURFORMAT, &systime, NULL, time_strbuf, 64);
    if (!getDateFormat_ret) {
	printf("date/time failed\n");
	return 0;
    }
    printf("Reported on    : %s %s %s\n", date_strbuf, time_strbuf, year_strbuf);
    gl_goodtime = 1;
    return 1;
}

char* sys_get_time_info_string(int i)
{
    if (!getDateFormat_ret) return "unavailable";

    switch (i) {
    case (9): return date_strbuf;
    case (10): return time_strbuf;
    case (5): return year_strbuf;
    default:
	printf("sys_get_time_info_string(%d) Error\n", i);
	return "error";
    }
}

uint8_t* gl_rpc_str;

char* sys_print_uuid(void)
{
    uint8_t *rpc_str;
    UUID uu;
    RPC_STATUS rpc_ret;
    rpc_ret = UuidCreate(&uu);
    rpc_ret = UuidToString(&uu, &rpc_str);
    if (rpc_ret != RPC_S_OK) {
	printf("Benchmark UUID : failed to generate UUID\n");
	return "failed";
    }   
    printf("Benchmark UUID : %s\n", rpc_str);
    gl_rpc_str = rpc_str;
    return (char*)rpc_str; //RpcStringFree(&rpc_str);
}

char* sys_get_uuid(void)
{
    return (char*)gl_rpc_str;
}

#else

struct utsname uname_buf;
int uname_ret;
char* sys_print_hostname(void)
{
    uname_ret = uname(&uname_buf);
    if (uname_ret) {
	perror("uname() failed");
	printf("Hostname unknown.\n");
	return "unknown";
    }
    return uname_buf.nodename;
}

char* sys_get_hostname(void)
{
    uname_ret = uname(&uname_buf);
    if (uname_ret) {
	perror("uname() failed");
	return "unknown";
    }
    return uname_buf.nodename;
}

char* sys_get_os_version_string(int i)
{
    switch (i) {
    case 0: return uname_buf.sysname;
    case 4: return uname_buf.release;
    default: return 0;
    }
}

char* sys_get_cpu_arch(void) 
{
    return uname_buf.machine;
}

int gl_goodtime = 0;
struct tm timestamp_time;

int sys_print_time_info(void)
{
    char strbuf[64]; 
    time_t t = time(NULL);
    if (t == ((time_t) -1)) {
	perror("time() failed"); 
	return 0;
    }
    printf("Reported on    : %s", ctime_r(&t, strbuf));  /* N.B. ctime adds \n at the end */
    timestamp_time = *gmtime(&t);
    gl_goodtime = 1;
    return 1;
}


int sys_get_time_info_value(int i)
{
    switch (i) {
    case (0): return timestamp_time.tm_sec;
    case (1): return timestamp_time.tm_min;
    case (2): return timestamp_time.tm_hour;
    case (3): return timestamp_time.tm_mday;
    case (4): return timestamp_time.tm_mon;
    case (5): return timestamp_time.tm_year;
    case (6): return timestamp_time.tm_wday;
    case (7): return timestamp_time.tm_yday;
    case (8): return timestamp_time.tm_isdst;
    default:
	printf("sys_get_time_info_value(%d): Error\n", i);
	return 0;
    }
}

char uuid_str[37];
char* sys_print_uuid(void)
{
    //char str[37]; /* 36-byte string plus null */
    uuid_t uu;
    uuid_generate(uu);
    uuid_unparse(uu, uuid_str); 
    printf("Benchmark UUID : %s\n", uuid_str);
    return uuid_str;
}

char* sys_get_uuid(void)
{
    return uuid_str;
}

#ifndef _WIN32
static int trace_marker_fd = -1;
void trace_marker_init()
{
    if (params.threshold == 0) return;

    trace_marker_fd = open("/sys/kernel/debug/tracing/trace_marker", O_WRONLY);
    if (trace_marker_fd == -1) {
	perror("ftrace_init open trace_marker failed");
    }
}

_code
void mark_long_latency(uint32_t nsec)
{
    char buf[64];
    int len;

    if (trace_marker_fd == -1) return;
    if (nsec >= params.threshold) {
	len = sprintf(buf, "latency > %" PRIu32 "ns: %" PRIu32, 
		params.threshold, nsec);
	if (write(trace_marker_fd, buf, len) == -1) {
	    perror("mark_long_latency failed");
	}
    }
}

void trace_marker_exit()
{
    if (trace_marker_fd >= 0) {
	if (close(trace_marker_fd) == -1) {
	    perror("ftrace_finish close trace_marker_fd failed");
	}
    }
}
#endif	//_WIN32

#ifdef PMB_NUMA
/*
 * find set of nodes covering all cpus in cpuin
 */
static
struct bitmask* numa_nodemask_from_cpumask(struct bitmask* cpuin)
{
    int node, i;
    unsigned int bsz = numa_bitmask_nbytes(cpuin);

    struct bitmask* nodeout;

    nodeout = numa_allocate_nodemask();
    if (!nodeout) return NULL;

    for (i = 0; i < bsz*8; i++) {
	if (numa_bitmask_isbitset(cpuin, i)) {
	    node = numa_node_of_cpu(i);
	    numa_bitmask_setbit(nodeout, node);
	}
    }
    return nodeout;
}

/* 
 * convert cpumask to cpuset
 * but can't we just memcopy?
 */
void sys_numa_cpuset_from_cpumask(cpu_set_t* cpuset, struct bitmask* cpumask)
{
    int i;
    unsigned int bsz = numa_bitmask_nbytes(cpumask);

    CPU_ZERO(cpuset);
    for (i = 0; i < bsz*8; i++) {
	if (numa_bitmask_isbitset(cpumask, i)) {
	    CPU_SET(i, cpuset);
	}
    }
}

void numa_print_bitmask(struct bitmask* bits)
{
    int i;
    unsigned int bsz = numa_bitmask_nbytes(bits);
    unsigned char* ptr = (unsigned char*)(bits->maskp);

//    printf("bitmask size: %d bytes\n", bsz);

    for (i = 0; i < bsz; i++) {
	printf("%02x ", ptr[bsz-1-i]);
    }
    printf("\n");
}

int test_parse_numa_option(void)
{
    char* cpumap_string = "!0";

    struct bitmask* cpumask ;
    struct bitmask* nodemask; 

   // printf("possible numa nodes: %d\n", numa_num_possible_nodes());
    
    cpumask = numa_parse_cpustring_all(cpumap_string);

    if (!cpumask) {
	perror("cpumask allocation failed");
    }
    
    nodemask = numa_nodemask_from_cpumask(cpumask);
    if (!nodemask) {
	perror("nodemask allocation failed");
    }

    numa_print_bitmask(cpumask);
    numa_print_bitmask(nodemask);
    
    numa_free_nodemask(nodemask);
    numa_free_cpumask(cpumask);

    nodemask = numa_parse_nodestring_all("all");
    numa_print_bitmask(nodemask);
    return 0;
}

/*
 * parse affinity set string from command arg.
 */
int populate_new_affinity_set(struct affy_node** head, const char* arg)
{
    struct affy_node* prev = *head;
    struct affy_node* curr;
    char *dup, *p;
    
    curr = malloc(sizeof(*curr));
    if (!curr) return -1;
    
    curr->next = prev;
    curr->nthreads = 0;
    dup = strdup(arg);
    
    // 1. check for :num_thread option at the end
    p = strchr(dup, ':');
    if (p) {	// found
	char* endptr;
	long val = strtol(p+1, &endptr, 0);
	if (*(p+1) == '\0') goto out_err;
	if (*endptr != '\0') goto out_err;
	if (val <= 0) goto out_err;
	curr->nthreads = (int)val;
	// now chop off ':' and rest
	*p = '\0';
    }
    // 2. parse cpumask & nodemask
    curr->cpumask = numa_parse_cpustring_all(dup);
    if (!curr->cpumask) goto out_err;

    curr->nodemask = numa_nodemask_from_cpumask(curr->cpumask);
    if (!curr->nodemask) goto out_err2;


    // 3. get nthreads if we didnt get it from option
    if (curr->nthreads == 0) {
	curr->nthreads = numa_bitmask_weight(curr->cpumask);
    }

    *head = curr;

    free(dup);

    return 0;

out_err2:
    if (curr->cpumask) numa_free_cpumask(curr->cpumask);
out_err:
    if (dup) free(dup);
    if (curr) free(curr);

    return -1;
}

int alloc_affy_buffers(struct affy_node* head, size_t num_pfn)
{
    char* buf;
    long ret;
    struct affy_node* iter;
    int permissions = PROT_READ;
    nodemask_t mask; 
    unsigned long maxnode = numa_num_possible_nodes(); // in bits

    if (params.ratio < 100) permissions |= PROT_WRITE; 

    for (iter = head; iter != NULL; iter = iter->next) {
	buf = mmap(NULL, num_pfn * PAGE_SIZE, permissions, 
		MAP_PRIVATE | MAP_ANONYMOUS, -1, 0);
	if (buf == MAP_FAILED) {
	    perror("buf mmap failed");
	    return 1;
	}
	nodemask_zero(&mask);
	copy_bitmask_to_nodemask(iter->nodemask, &mask);
	ret = mbind(buf, num_pfn * PAGE_SIZE, MPOL_BIND, &mask.n[0], 
		maxnode, 0);
	if (ret) {
	    perror("buf mbind failed");
	    return 1;
	}
	iter->buf = buf;
    }

    return 0;
}

int free_affy_buffers(struct affy_node* head, size_t num_pfn)
{
    int ret;
    struct affy_node* iter;

    for (iter = head; iter != NULL; iter = iter->next) {
	ret = munmap(iter->buf, num_pfn * PAGE_SIZE);

	if (ret) {
	    perror("munmap failed");
	    return 1;
	}

    }
    return 0;
}

void sys_print_affinitysets(struct affy_node* head)
{
    int thr_id = 1;
    int i = 0;
    int j;
    struct affy_node* iter = head;
    for (iter = params.affy_head; iter != NULL; iter = iter->next) {
	printf("    set %d: nthreads = %d\n", i, iter->nthreads);
	printf("       cpumask = ");
	numa_print_bitmask(iter->cpumask);
	printf("       nodemask = ");
	numa_print_bitmask(iter->nodemask);
	printf("       thread ids = ");
	for (j = 0; j < iter->nthreads; j++) printf("%d ", thr_id++);
	printf("\n");
	i++;
    }
}
#endif
#endif
