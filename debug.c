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

/*
 * this file contains debug/test code which is only compiled in when testing
 * also contains old code
 */
#define _GNU_SOURCE
#include <memory.h>

/* microsoft compiler / gcc for windows defines this */
#ifdef _WIN32
#include <io.h>
#else 
#include <sys/mman.h>
#include <unistd.h>
#endif

#include <stdlib.h>
#include <stdio.h>
#include <string.h>
#include <math.h>

#ifdef _WIN32
#include <windows.h>
#endif

#ifdef _WIN32
//#include <stdint.h>
//#include <x86intrin.h>
#endif


#include "system.h"

#include "rdtsc.h"
#include "cpuid.h"
#include "pattern.h"
#include "access.h"

/* TODO: make this varag C function? */
#define prn(fmt, ...) \
do {\
    if (!params.quiet) printf(fmt, ## __VA_ARGS__); \
} while (0)

unsigned freq_khz;

/* total memory size in megabytes */
//static long param_size_mib = 1024;
//static int param_randomized = 1;

#define PAGE_SHIFT (12)
#define PAGE_SIZE (1<<PAGE_SHIFT)

#ifdef _XXWIN32
double log1p(double x)
{
    double u = 1.+x;
    if (u == 1.)
	return x;
    else
	return log(u)*x/(u-1.);
}


double expm1(double x)
{
    if (fabs(x) < 1e-5)
	return x + 0.5*x*x;
    else
	return exp(x) - 1.0;
}
#endif


#if 0
#define SYS_RAND_MAX RAND_MAX
static inline
long int sys_random(void) {
    return random();
}

/* per thread state */
#define PMBENCH_SYS_RANDOM_STATE_SIZE 128
struct sys_random_state {
    struct random_data buf;
    char state[PMBENCH_SYS_RANDOM_STATE_SIZE];
};

static inline 
void sys_random_init(struct sys_random_state* s, unsigned int seed) {
    initstate_r(seed, s->state, PMBENCH_SYS_RANDOM_STATE_SIZE, &s->buf);
    setstate_r(s->state, &s->buf);
}

static inline
long int sys_random_r(struct sys_random_state* s) {
    int32_t result;
    random_r(&s->buf, &result);
    return (long int)result;
}
#endif

#define NUM_PATTERNS 5
struct pattern_s {
    pattern_generator* pattern;
    long size;
    fp_t param1;
    int seed;
};

struct pattern_s tvs[NUM_PATTERNS + 1] = {
    { 
	.pattern = &linear_pattern,
	.size = 1024,
	.param1 = 1,
	.seed = 0,
    }, { 
	.pattern = &uniform_pattern,
	.size = 1024,
	.param1 = 0,
	.seed = 0,
    }, { 
	.pattern = &normal_pattern,
	.size = 200,
	.param1 = 10,
	.seed = 0,
    }, { 
	.pattern = &pareto_pattern,
	.size = 1024 * 256,
	.param1 = 0.060,
	.seed = 0,
    }, { 
	.pattern = &zipf_pattern,
	.size = 1024 * 256,
	.param1 = 1.00001,
	.seed = 0,
    }, {
	0,
    }
};


/**
 * verify_distributions - 
 */
void debug_verify_distributions(void)
{
    int num_pages;
    long i;
    int* count;
    struct stopwatch sw;
    static const int total = 10000000;
    int tv;

    void* ctx;
    long idx;

    for (tv = 0; tv < NUM_PATTERNS; ++tv) {
	num_pages = tvs[tv].size;
	count = calloc(num_pages, sizeof(int));
	if (!count) {
	    printf("calloc failed.. bailing out\n");
	    exit(1);
	}
	ctx = tvs[tv].pattern->alloc_pattern(num_pages, 
		tvs[tv].param1, tvs[tv].seed);

	i = total;
	printf("%s time measuring..\n", tvs[tv].pattern->name);
	sw_reset(&sw, &rdtsc_ops);
	sw_start(&sw);

	while (i--) {
	    idx = tvs[tv].pattern->get_next(ctx);
	    if (idx >= num_pages) printf("???\n");
	    count[idx] += 1;
	}
	sw_stop(&sw);
	/* print out first hundreds */
	for (i = 0; i < 100; ++i) {
	    printf("%d ", count[i]);
	}
	printf("\n-------------------------------------------------\n");
	/* print out last hundreds */
	for (i = 0; i < 100; ++i) {
	    printf("%d ", count[num_pages - 100 + i]);
	}
	printf("\ntime taken: %u usec for %d samples\n\n", sw_get_usec(&sw), total);
	tvs[tv].pattern->free_pattern(ctx);
	free(count);
    }
}
//	/* print out the observed a */
//	for (i = 1; i < 100; ++i) {
//	    float rp = (float)count[i];
//	    float rq = (float)count[i+3];
//	    float np = (float)(i);
//	    float nq = (float)(i+3);
//	    float a = (log(rp/rq)/log(nq/np));
//	    printf("%1.3f ", a);
//	}


/************* RDTSC based stopwatch ****************/

typedef struct stopwatch_rdtsc {
    unsigned long long start_tsc;
    unsigned long long elapsed_sum;
} stopwatch_rdtsc; 

static inline
void sw_reset_rdtsc(stopwatch_rdtsc* sw) {
    sw->start_tsc = sw->elapsed_sum = 0ull;
}

static inline
unsigned long long sw_start_rdtsc(stopwatch_rdtsc* sw) {
    sys_barrier();
    sw->start_tsc = rdtsc();
    sys_barrier();
    return sw->start_tsc;
}

static inline
unsigned long long sw_stop_rdtsc(stopwatch_rdtsc* sw) {
    unsigned long long now;
    sys_barrier();
    now = rdtsc();
    sys_barrier();
    sw->elapsed_sum += (now - sw->start_tsc);
    return now;
}

extern unsigned get_cycle_freq(void);


/*********************************************************************/
/*
static
unsigned measure_rdtsc_frequency_old(void)
{
    stopwatch_rdtsc sw;
    unsigned long long y1, y2, offset;
    unsigned ret, rdtsc_freq_measured;
    static const int x1 = 128, x2 = 256; // ~ms

    sw_reset_rdtsc(&sw);
    sw_start_rdtsc(&sw);
    ret = usleep(x1*1024);
    sw_stop_rdtsc(&sw);
    if (ret) {
	printf("CPU frequency detection failed (sleep) Bailing out\n");
	return 0;
    }
    y1 = sw.elapsed_sum;
    rdtsc_freq_measured = (unsigned)((y1*1000ull)/(x1*1024));
    sw_reset_rdtsc(&sw);
    sw_start_rdtsc(&sw);
    ret = usleep(x2*1024);
    sw_stop_rdtsc(&sw);
    if (ret) {
	printf("CPU frequency detection failed (sleep) Bailing out\n");
	return 0;
    }
    y2 = sw.elapsed_sum;

    offset = ((y1*x2) - (y2*x1))/(x2-x1);
    rdtsc_freq_measured = (unsigned)(((y1 - offset)*1000ull)/(x1*1024));
//printf("rdtsc_freq_measured: %d, offset: %lld\n", rdtsc_freq_measured, offset);

    return rdtsc_freq_measured;
}
*/

/* histogram old version */
/*
 * bucket[0] :=  [0, 255) nsecs
 * bucket[1] :=  [256, 512) = [1 << 8, 1 << 9)
 * bucket[2] :=  [512, 1024) = [1 << 9, 1 << 10)
 * bucket[3] :=  [1024, 2048) = [1 << 10, 1 << 11) (~1-2 usec)
 * ...
 * bucket[13] := [2^20, 2^21) = [1 << 20, 1 << 21) (~1-2 msec)
 * bucket[14] :=  [2^21, 2^22) = [1 << 21, 1 << 22)
 * bucket[15] :=  [2^22, inf)
 */

/* 64 bytes - one cache line */
struct histogram_log2 {
    unsigned bucket[16];
} __attribute__((packed));

static inline
int get_log2_bucket_index(unsigned elapsed_nsec) {
    if (elapsed_nsec < (1 << 8)) return 0;
    if (elapsed_nsec >= (1 << 22)) return 15;
    return ilog2(elapsed_nsec) - 7;
}

/* 
 * Use the first 64 bytes for keeping latency histogram
 * N.B. don't need to init scrub histogram since anynomous mmapping 
 * zero-inits memory.
 */
/*
static
int access_histogram_old(char* buf, size_t pfn)
{
    struct stopwatch sw;
    static int _val_sink = 0;
    struct histogram_log2* histo;
    int* ptr;

    sw_reset(&sw, &rdtsc_ops);
    sw_start(&sw);
    ptr = (int*)(buf + (pfn << PAGE_SHIFT) + 128);
    _val_sink += *ptr;
    *ptr = _val_sink;
    sw_stop(&sw);
    histo = (struct histogram_log2*)(buf + (pfn << PAGE_SHIFT));
    histo->bucket[get_log2_bucket_index(sw_get_nsec(&sw))] += 1;
    return _val_sink;
}
*/

struct histo_result {
    struct histogram_log2 sum;
    unsigned __dummy; 
} __attribute__((packed));

/*
 * we use buf[2048-4097] for storing result.
 */
/*
static
int finish_histogram_old(char* buf, size_t num_pages)
{
    struct histogram_log2* histo;
    struct histo_result* result = (struct histo_result*)(buf + 2048);
    size_t i;
    int j;
    
    for (i = 0; i < num_pages; ++i) {
	histo = (struct histogram_log2*)(buf + (i << PAGE_SHIFT));
	for (j = 0; j < 16; ++j) {
	    result->sum.bucket[j] += histo->bucket[j];
	}
    }
    
    return 1;
}
*/

void dump_histogram_log2(const struct histogram_log2* bin)
{
    int i;
    printf("[0, 2^8) nsec : %d\n", bin->bucket[0]);
    for (i = 1; i < 15; ++i) {
	printf("[2^%d, 2^%d) nsec : %d\n", i + 7, i + 8, bin->bucket[i]);
    }
    printf("[2^22, inf) nsec : %d\n", bin->bucket[15]);
}

/*
static
void histogram_report_old(char* buf)
{
    struct histo_result* result = (struct histo_result*)(buf + 2048);
    printf("# Latency frequency histogram\n");
    dump_histogram_log2(&result->sum);
    
    //printf("[0, 2^8) nsec : %d\n", result->sum.bucket[0]);
    //for (i = 1; i < 15; ++i) {
	//printf("[2^%d, 2^%d) nsec : %d\n", i + 7, i + 8, result->sum.bucket[i]);
    //}
    //printf("[2^22, inf) nsec : %d\n", result->sum.bucket[15]);
    //printf("%s: Nothing to report\n", __func__);
    
    return;
}
*/

void
test_histo_calculation(void)
{
    unsigned test_vector[8] = { 1, 255, 256, 512, 1024, 2047, 4096, 8192 };
    struct histogram_log2 bin;
    int i;
    memset(&bin, 0, sizeof(bin));
    for (i = 0; i < 8; ++i) {
	bin.bucket[get_log2_bucket_index(test_vector[i])]++;
    }
    dump_histogram_log2(&bin);
}

//extern int touch_only(char* buf, size_t pfn);

/*access_fn_set histogram_access_old = {
    touch_only,
    access_histogram_old,
    finish_histogram_old,
    histogram_report_old,
    "histo_old",
    "Touch and keep latency histogram (old format)"
};*/


#ifdef _WIN32
void test_stat_mem(void)
{
    sys_mem_ctx ctx;
    sys_mem_item info;

    int ret;
    ret = sys_stat_mem_update(&ctx, &info);
    if (ret) {
	perror("sys_stat_mem_init:");
	exit(ret);
    }
    //sys_stat_mem_print(&info);
}


int pread(unsigned int fd, char *buf, size_t count, int offset)
{
    if (_lseek(fd, offset, SEEK_SET) != offset) {
        return -1;
    }
    return read(fd, buf, count);
}

#else
void test_stat_mem(void)
{
    sys_mem_ctx ctx;
    sys_mem_item info;
    int ret;
    ret = sys_stat_mem_init(&ctx);
    if (ret) {
	perror("sys_stat_mem_init:");
	exit(ret);
    }
    ret = sys_stat_mem_update(&ctx, &info);
    if (ret) {
	perror("sys_stat_mem_init:");
	exit(ret);
    }
    ret = sys_stat_mem_exit(&ctx);
    if (ret) {
	perror("sys_stat_mem_init:");
	exit(ret);
    }
    //sys_stat_mem_print(&info);
}
#endif

#if 0
//Old stuffs
/*
 * benchmark functions.
 *
 * There are two important questions to answer when designing randomized benchmark.
 * Q1: How long should we run the benchmark?
 * Q2: At what point should we start measure the time?
 *
 * Answers should depend on the probabiliy distribution function. This is 
 * because we need to find out the point at which the system reaches the 
 * 'steady-state', from which we need to start measuring performance. 
 * This reqires excercising the system, and the amount of exercise depends 
 * on probability distribution function.
 */

/**
 * perform_bench_uniform - randomly access pages with uniform distribution
 *	@buf: base address of memory to access
 *	@num_pages: total number of pages in the buffer memory
 * 
 * NB. For Q1, We choose the expected number of trials to touch all pages.
 * The solution to the Coupon Collector's Problem says that the expected
 * trials is ~ n*ln(n) + n*0.5772 + 0.5 where n is the number of pages.
 * Since this doesn't have to be exact, we use ln(n) = ln(2)*log_2(n),
 * and use log_2(n) ~<= first msb bit pos(n) + 1.
 * We avoid using floating point by 
 *   ln(2) ~= 0.6931 = (0.6931 * 65536) / 65536 ~= 45426 / 2^16 
 * As to Q2, we somewhat arbitrarilly use 2*n as the the warm-up period.
 * (Although not every pages, 2*n trials will touch significantly 
 * large amount of pages)
 */
void perform_bench_uniform(char* buf, int num_pages)
{
    long iter_warmup;
    long iter_full;
    long target_pfn;
    struct stopwatch sw;
    
    iter_warmup = num_pages * 2;
    iter_full = ((long)num_pages * __my_flsl(num_pages) * 45426) >> 16;
    printf("performing %ld iter for warmup, %ld iter for bench.\n", 
	    iter_warmup, iter_full);
    sw_reset(&sw, &rdtsc_ops);
    sw_start(&sw);
    while (iter_warmup--) {
	target_pfn = rand() % num_pages;
	
	access_fn(buf, target_pfn);  
    }
    sw_stop(&sw);
    printf("Warmup time taken: %u usec\n", sw_get_usec(&sw));
    printf("Start measuring bench.\n");
    sw_reset(&sw, &rdtsc_ops);
    sw_start(&sw);
    while (iter_full--) {
	target_pfn = rand() % num_pages;
	
	access_fn(buf, target_pfn);  
    }
    sw_stop(&sw);
    printf("Benchmark time taken: %u usec\n", sw_get_usec(&sw));
}

long get_normal(int stdev)
{
    static const int ORDER = 12;
    long n = stdev * ORDER;
    long sum = 0;
    int i;
    for (i = 0; i < ORDER; ++i) {
	sum += rand() % n;
    }
    return sum / ORDER;
}

/**
 * perform_bench_normal - randomly access pages with normal distribution
 *	@buf: base address of memory to access
 *	@num_pages: total number of pages in the buffer memory
 *	@one_stdev: page numbers that constitute 1 stdev.
 * NB. We probably want to use a randomized permutation of the page
 * frame number to access the final page.
 * num_pages must be equal to or greater than 12*one_stdev.
 */
void perform_bench_normal(char* buf, int num_pages, int one_stdev)
{
    long iter_warmup;
    long iter_full;
    long target_pfn;
    struct stopwatch sw;
    
    iter_warmup = num_pages;
    iter_full = (long)num_pages * 4;
    printf("perform_bench_normal - num_pages: %d (%d MiB), one_stdev: %d (%d MiB)\n",
	    num_pages, num_pages/256, one_stdev, one_stdev/256);
    printf("performing %ld iter for warmup, %ld iter for bench.\n", 
	    iter_warmup, iter_full);
    sw_reset(&sw, &rdtsc_ops);
    sw_start(&sw);
    while (iter_warmup--) {
	target_pfn = get_normal(one_stdev);
	
	access_fn(buf, target_pfn);  
    }
    sw_stop(&sw);
    printf("Warmup time taken: %u usec\n", sw_get_usec(&sw));
    printf("Start measuring bench.\n");
    sw_reset(&sw, &rdtsc_ops);
    sw_start(&sw);
    while (iter_full--) {
	target_pfn = get_normal(one_stdev);
	
	access_fn(buf, target_pfn);  
    }
    sw_stop(&sw);
    printf("Benchmark time taken: %u usec\n", sw_get_usec(&sw));
}

void*
__attribute__((aligned(4096)))
main_bm_thread_single(char* buf)
{
    struct bench_result* presult = &result;

    const parameters* p = &params;
    pattern_generator* pattern = p->pattern;
    access_fn_set* access = p->access;
    struct sys_timestamp* tsops = p->tsops;
    struct stopwatch sw;
    int i, tenk;
    unsigned long long done_tsc, now;


    long num_pages = p->setsize_mib * 256;
    long iter_warmup;
    long iter_patternlap = 1000000; // draw 1000000
    void* ctx = pattern->alloc_pattern(num_pages, p->shape, 0.1 /* dummy param */);

    printf("Thread created: %d\n", tinfo->thread_num);

    prn("num_pages: %ld (%ld MiB), shape: %0.4f\n",
	    num_pages, num_pages/256, p->shape);

    sw_reset(&sw, tsops);

    /* do measure pattern generation overhead */
    sw_start(&sw);
    for (i = 0; i < iter_patternlap; ++i) {
	pattern->get_next(ctx);
    }
    sw_stop(&sw);

    presult->total_numgen_clock = sw.elapsed_sum; 
    presult->total_numgen_count = iter_patternlap;

    prn("Pattern generation overhead: %0.4f usec per drawing\n", 
	    (float)sw_get_usec(&sw)/iter_patternlap); // convert msec to usec
    sw_reset(&sw, tsops);

    /* take memory information snapshot */
    sys_stat_mem_update(&mem_ctx, &mem_info_before_warmup);

    /* do warmup */
    if (p->cold) goto do_bench;
    iter_warmup = pattern->get_warmup_run ? 
		pattern->get_warmup_run(ctx) : num_pages;
    prn("performing %ld page accesses for warmup\n", iter_warmup);
    sw_start(&sw);
    for (i = 0; i < iter_warmup; ++i) {
	access->warmup(buf, pattern->get_next(ctx));
    }
    sw_stop(&sw);

    presult->total_warmup_clock = sw.elapsed_sum;
    presult->total_warmup_count = iter_warmup;

    prn("Warmup time taken: %d usec\n", sw_get_usec(&sw));
    prn("Benchmark starts\n");
    sw_reset(&sw, tsops);

    sys_stat_mem_update(&mem_ctx, &mem_info_before_run);

do_bench:
    /* do benchmark */
    tenk = 0;

    done_tsc = (unsigned long long)p->duration_sec * freq_khz * 1000; 
    alarm_arm(0, tsops->timestamp() + (done_tsc / 2), mem_info_oneshot, &mem_ctx);
    done_tsc += sw_start(&sw);

    while ((now = tsops->timestamp()) < done_tsc) {
	alarm_check(now);
	for (i = 0; i < 10000; ++i) {
	    access->exercise(buf, pattern->get_next(ctx));
	}
	tenk++;
    }

    sw_stop(&sw);
    
    sys_stat_mem_update(&mem_ctx, &mem_info_after_run);

    presult->total_bench_clock = sw.elapsed_sum;
    presult->total_bench_count = tenk * 10000;
    
    prn("Benchmark done. It took %0.3f sec for %d page access\n", 
	    (float)sw_get_usec(&sw)/1000000.0f, tenk*10000);
    prn("  (Average %0.3f usec per page access)\n", (float)sw_get_usec(&sw)/(tenk*10000));

    access->finish(buf, num_pages);
    pattern->free_pattern(ctx);

    return NULL;
}


int access_histogram__nonatomic(char* buf, long pfn)
{
    struct stopwatch sw;
    static int _val_sink = 0;
    struct histogram_log2_sub* histo;
    int* ptr;
    int index;
    unsigned elapsed_nsec;

    sw_reset(&sw, get_tsops());
    sw_start(&sw);
    ptr = (int*)(buf + (pfn << PAGE_SHIFT) + (2048 - 64));
    _val_sink += *ptr;
    *ptr = _val_sink;
    sw_stop(&sw);
    histo = (struct histogram_log2_sub*)(buf + (pfn << PAGE_SHIFT));
    elapsed_nsec = sw_get_nsec(&sw);
    if (elapsed_nsec < (1 << 8)) {
	histo->bucket_sub[0].quad[0] += 1;
	return _val_sink;
    }
    index = ilog2(elapsed_nsec) - 7;
    if (index < 16) {
	int sub_index = (~(1u << (index + 7)) & elapsed_nsec) >> (index + 7 - 4);
	histo->bucket_sub[index].quad[sub_index] += 1;
	return _val_sink;
    }
    if (index >= 16+7) {
	histo->bucket_long[7] += 1;
	return _val_sink;
    }
    histo->bucket_long[index - 16] += 1;
    return _val_sink;
}

#endif
