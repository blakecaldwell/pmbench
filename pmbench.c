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
#include <memory.h>
#include <stdlib.h>
#include <unistd.h>
#include <stdio.h>
#include <signal.h>
#include <string.h>
#include <math.h>
#include <stdarg.h>
#include <inttypes.h>
#include <fcntl.h>


#ifdef _WIN32
#include <windows.h>
#include "argp.h"
#else 
#include <sys/mman.h>
#include <sys/resource.h>
#include <argp.h>
#endif

#ifdef XALLOC
#include "xalloc.h"
#endif

#ifdef PMB_THREAD
#include <pthread.h>
#endif

#include "system.h"
#include "rdtsc.h"
#include "cpuid.h"
#include "pattern.h"
#include "access.h"

#include "pmbench.h"

/*
 * Program arguments handling
 */
static struct argp_option options[] = {
    { "mapsize", 'm', "MAPSIZE", 0, "Mmap size in MiB" },
    { "setsize", 's', "SETSIZE", 0, "Working set size in MiB" },
    { "access", 'a', "ACCESS", 0, "Specify access method. e.g., touch, histo" },
    { "pattern", 'p', "PATTERN", 0, "Specify PATTERN. e.g, linear, uniform(def), pareto, normal" },
    { "shape", 'e', "SHAPE", 0, "Pattern-specific parameter" },
    { "delay", 'd', "DELAY", 0, "Delay between accesses in clock cycles" },
    { "quiet", 'q', 0, 0, "Don't produce any output until finish" },
    { "cold", 'c', 0, OPTION_ARG_OPTIONAL, "Don't perform warm-up exercise" },
    { "timestamp", 't', "TIMESTAMP", 0, "Specify TIMESTAMP. rdtsc, rdtscp(def), or perfc" },
    { "ratio", 'r', "RATIO", 0, "Percentage read/write ratio (0 = write only, 100 = read only; default 50)" }, //TODO: count # of reads/writes
    { "offset", 'o', "OFFSET", 0, "Specify static page access offset (default random)" },
    { "initialize", 'i', 0, OPTION_ARG_OPTIONAL, "Initialize memory map with garbage data" },
    { "threshold", 'h', "THRESHOLD", 0, "Set the threshold time to trigger the ftrace log" },
    { "wrneedsrd", 'z', 0, OPTION_ARG_OPTIONAL, "Write is preceeded by read on the same memory" },
    { "file", 'f', "FILE", 0, "Filename for XML output" },
#ifdef PMB_THREAD
    { "jobs", 'j', "NUMJOBS", 0, "Number of concurrent jobs (threads)" },
#endif
#ifdef XALLOC
    { "xalloc", 'x', "REALMEMSIZE", 0, "Non-zero REAMMEMSIZE (MiB) enables xalloc. xmmap uses REALMEMSIZE of real memory" },
    { "xalloc_path", 'X', "PATHNAME", 0, "xalloc backend device pathname. default is /dev/mem0" },
#endif
#ifdef PMB_NUMA
    { "affinityset", 'y', "CPUSTR[:THRCNT]", 0, "Affinity set creation for numa system"
    },
#endif
    { 0 }
};

//const char* bd_path = "/mnt/tmpfs/xalloc_blob.img";

// parameter definition moved to pmbench.h
parameters params;


static
__attribute__((cold))
void set_default_params(parameters* p)
{
    p->duration_sec = 120;  // two minutes.. but user must supply duration.
    p->mapsize_mib = 256;
    p->setsize_mib = 128;
    p->access = &histogram_access;
    p->pattern = &uniform_pattern;
    p->shape = 1.00001;
    p->delay = 0;	    // no delay
    p->quiet = 0;
    p->cold = 0;
    p->tsops = &rdtscp_ops;
    p->jobs = 1;
    p->init_garbage = 0;
    p->threshold = 0;
    p->write_needs_read = 0;
#ifdef XALLOC
    p->xalloc_mib = 0;
    p->xalloc_path = "/dev/ram0";
#endif
    p->offset = -1;
    p->get_offset = get_offset_function(-1);
    p->ratio = 50;
    p->xml_path = NULL;
#ifdef PMB_NUMA
    p->affy_head = NULL;
#endif
}

const struct sys_timestamp* get_tsops(void)
{
    return params.tsops;
}

static
__attribute__((cold))
void print_params(const parameters* p)
{
    printf("  duration_sec = %d\n", p->duration_sec);
    printf("  mapsize_mib  = %d\n", p->mapsize_mib);
    printf("  setsize_mib  = %d\n", p->setsize_mib);
    printf("  initialize   = %d\n", p->init_garbage);
    printf("  shape        = %f\n", p->shape);
    printf("  delay        = %d\n", p->delay);
    printf("  quiet        = %d\n", p->quiet);
    printf("  cold         = %d\n", p->cold);
#ifdef PMB_THREAD
    printf("  jobs         = %d\n", p->jobs);
#endif
    printf("  offset       = "); if (p->offset < 0) printf("random\n"); else printf("%d\n", p->offset);
    printf("  ratio        = %d%%\n", p->ratio);
    printf("  threshold    = %d\n", p->threshold);
    printf("  wrneedsrd    = %d\n", p->write_needs_read);
    if (p->pattern && p->pattern->name) {
	printf("  pattern      = %s\n", p->pattern->name);
    }
    if (p->access && p->access->name) {
	printf("  access       = %s\n", p->access->name);
    }
    if (p->tsops && p->tsops->name) {
	printf("  tsops        = %s\n", p->tsops->name);
    }
#ifdef XALLOC
    printf("  xalloc_mib   = %d\n", p->xalloc_mib);
    printf("  xalloc_path  = %s\n", p->xalloc_path);
#endif
#ifdef PMB_NUMA
    printf("  affinityset  = ");
    if (p->affy_head) {
	printf("\n");
	sys_print_affinitysets(p->affy_head);
    } else {
	printf("NULL\n");
    }
#endif
}

/* argp parse callback */
static
__attribute__((cold))
error_t parse_opt(int key, char* arg, struct argp_state* state)
{
    parameters* param = state->input;

#ifdef PMB_NUMA
    static int saw_jobs = 0;
#endif

    switch (key) {
    case 'm':
    	if (arg) param->mapsize_mib = atoi(arg);
    	break;
    case 's':
    	if (arg) param->setsize_mib = atoi(arg);
    	break;
    case 'i':
    	param->init_garbage = 1;
    	break;
#ifdef PMB_THREAD
    case 'j':
#ifdef PMB_NUMA
	saw_jobs = 1;
	if (param->affy_head) {
	    printf("jobs parameter shouldn't be specified if using affinityset\n");
	    return ARGP_ERR_UNKNOWN;
	}
#endif
    	if (arg) param->jobs = atoi(arg);
    	break;
#endif
    case 'p':
	param->pattern = get_pattern_from_name(arg);
	if (!param->pattern) {
		printf("pattern name unrecognized.\n");
		param->pattern = &uniform_pattern;
		return ARGP_ERR_UNKNOWN;
	}
	break;
    case 'a':
	param->access = get_access_from_name(arg);
	if (!param->access) {
	    printf("access name unrecognized.\n");
	    param->access = &histogram_access;
	    return ARGP_ERR_UNKNOWN;
	}
	break;
    case 't':
	param->tsops = get_timestamp_from_name(arg);
	if (!param->tsops) {
		printf("timestamp name unrecognized.\n");
		param->tsops = &rdtsc_ops;
		return ARGP_ERR_UNKNOWN;
	}
	break;
    case 'd':
	if (arg) param->delay = atoi(arg);
	break;
#ifdef XALLOC
    case 'x':
    	if (arg) param->xalloc_mib = atoi(arg);
    	break;
    case 'X':
    	if (arg) param->xalloc_path = strdup(arg);
    	break;
#endif
    case 'e':
	if (arg) param->shape = atof(arg);
	break;
    case 'q':
	param->quiet = 1;
	break;
    case 'c':
	param->cold = 1;
	break;
    case 'r':
    	param->ratio = (arg ? atoi(arg) : 50);
    	if (param->ratio < 0 || param->ratio > 100) { 
	    printf("read/write ratio out of bounds, must be from 0-100.\n");
	    exit(EXIT_FAILURE);
	}
    	break;
    case 'o':
    	param->offset = (arg ? atoi(arg) : -1);
    	if (param->offset > 1023) { 
	   printf("page offset out of bounds, must be from 0-1023.\n"); 
	   exit(EXIT_FAILURE); 
    	}
    	param->get_offset = get_offset_function(param->offset);
    	break;
    case 'h':
	param->threshold = (arg ? atoi(arg) : 0);
	break;
    case 'z':
	param->write_needs_read = 1;
	break;
    case 'f':
    	if (arg) {
	    param->xml_path = strdup(arg);
    	}
    	break;
#ifdef PMB_NUMA
    case 'y':
	if (saw_jobs) {
	    printf("jobs parameter shouldn't be specified if using affinityset\n");
	    printf(".\n");
	}
    	if (!arg) {
	    printf("missing string on affinityset argument.\n");
	    argp_usage(state);
	    exit(EXIT_FAILURE);
	}
	if (populate_new_affinity_set(&param->affy_head, arg)) {
	    printf("affinityset argument syntax error.\n");
	    argp_usage(state);
	    exit(EXIT_FAILURE);
	}
    	break;
#endif
    case ARGP_KEY_NO_ARGS: 
	break;
    case ARGP_KEY_ARG:
	if (state->arg_num >= 1) argp_usage(state);
	param->duration_sec = arg ? atoi(arg) : 1;
	break;
    case ARGP_KEY_END:
    	if (state->arg_num < 1) argp_usage(state);
    	break;
    case ARGP_KEY_INIT:
    	break;
    case ARGP_KEY_FINI:
    	break;
    case ARGP_KEY_SUCCESS:
    	break;
    default:
    	return ARGP_ERR_UNKNOWN;
    }

    return 0;
}

/* using argp_parse */
static
__attribute__((cold))
int params_parsing(int argc, char** argv)
{
    static const char program_doc_str[] = "pmbench - System paging/swapping/memory benchmark";
    static const char args_doc_str[] = "DURATION";

    static struct argp argp = { options, parse_opt, args_doc_str, program_doc_str };
    /* N.B. to deal with Windows dll linkage issue, we set these variables here
     * instead of statical assignment */

    argp_program_version = PMBENCH_VERSION_STR COMPILE_OPT_TAGS;
    argp_program_bug_address = "Jisoo Yang <jisoo.yang@unlv.edu>";

    argp_parse(&argp, argc, argv, 0, 0, &params);
    /* check for arg sanity */
    if (params.duration_sec < 1) {
	printf("invalid parameter: duration must be positive integer\n");
	exit(EXIT_FAILURE);
    }
    if (params.mapsize_mib < params.setsize_mib) {
	printf("invalid parameter combination: mapsize < setsize\n");
	exit(EXIT_FAILURE);
    }
#ifdef PMB_THREAD
    if (params.jobs < 1) {
	printf("invalid parameter combination: jobs less than zero\n");
	exit(EXIT_FAILURE);
    }
#endif
#ifdef PMB_NUMA
    /* set jobs param from threads from affyset*/
    if (params.affy_head) {
	int thr_sum = 0;
	struct affy_node* iter;
	for (iter = params.affy_head; iter != NULL; iter = iter->next) {
	    thr_sum += iter->nthreads;
	}
	params.jobs = thr_sum;
    }
//sys_dump_affinity_set_param();
#endif
    return 0;
}

/*
 * logging - thread safe. N.B. it only makes individual prn call atomic.
 * mingw32 seems to be breaking printf thread atomicity in Windows...
 */
#ifdef PMB_THREAD
pthread_mutex_t lock_prn = PTHREAD_MUTEX_INITIALIZER;
#define prn_lock() pthread_mutex_lock(&lock_prn)
#define prn_unlock() pthread_mutex_unlock(&lock_prn)
#else
#define prn_lock(_l_) do {;} while(0)
#define prn_unlock(_l_) do {;} while(0)
#endif

static inline
int prn(const char* format, ...)
{
    va_list ap;
    int len;

    if (params.quiet) return 0;

    prn_lock();
    va_start(ap, format);
    len = vprintf(format, ap);
    va_end(ap);
    prn_unlock();

    return len;
}


uint32_t freq_khz;

static inline
void reset_result(struct bench_result* presult) {
    memset(presult, 0, sizeof(*presult));
}

/* per thread info */
struct thread_info {
#ifdef PMB_THREAD
    pthread_t thread_id;	// returned by pthread_create
#endif
    int thread_num;	    	// local thread number (1, 2, 3,...)
    char *map;			// memory map base pointer for access
    struct bench_result result;	// per-thread result
};

/* thread-shared for concurrency control and shared variable access */
struct thread_control {
    struct thread_info* tinfo;	// thread info array created
    char *stats;		// histograms base pointer
    int interrupted;		// ctrl-c sets this
#ifdef PMB_THREAD
    pthread_barrier_t barrier;	// barrier for mt
#endif
} control;
 
// ugly hack..
struct bench_result* get_result(int jobid)
{
    return &control.tinfo[jobid].result;
}

static
void print_result(void)
{
   int i;
   for (i = 0; i < params.jobs; i++) {
       struct bench_result* presult = get_result(i);
       float pure_lat = mean_us(bench) - mean_us(numgen);
       printf("Thread %d/%d:\n", i+1, params.jobs);
       printf("Net average page latency (Arith. mean): %0.4f us (%d clks)\n",
	   pure_lat, us_to_clk(pure_lat));
       printf("--- Measurement details ---\n");
       printf("  Page latency during benchmark (inc. gen): %0.4f us (%d clks)\n",
	   mean_us(bench), (int)mean_clk(bench));
       printf("    Total samples count: %"PRIu64"\n", presult->total_bench_count);
       printf("  Pattern generation overhead per drawing : %0.4f us (%d clks)\n",
	   mean_us(numgen), (int)mean_clk(numgen));
       printf("    Total samples count: %"PRIu64"\n", presult->total_numgen_count);
       if (params.cold) continue;
       printf("  Page latency during warmup              : %0.4f us (%d clks)\n",
	   mean_us(warmup), (int)mean_clk(warmup));
       printf("    Total samples count: %"PRIu64"\n", presult->total_warmup_count);
   }
}

sys_mem_item mem_info_before_warmup;// stores mem info right before warmup/exercise
sys_mem_item mem_info_before_run;   // stores mem info before exercise, after warmup
sys_mem_item mem_info_middle_run;   // stores mem info at the halfway of exercise
sys_mem_item mem_info_after_run;    // stores mem info right after exercise
sys_mem_item mem_info_after_unmap;  // stores mem info after unmapping (freeing memory) 


void
__attribute__((cold))
print_con_report(char* buf, const parameters* p)
{
    printf("\n------------- Benchmark signature -------------\n");
    //signature
    //pmbench_info
    sys_print_pmbench_info();

    //os_info
    sys_print_hostname(); //you must call this for any of the below get functions to work

    //version_info
#ifdef _WIN32
    printf("OS/kernel type : Windows %s version %d.%d (build %d)\n", 
	    sys_get_cpu_arch(), 
	    sys_get_os_version_value(1), 
	    sys_get_os_version_value(2), 
	    sys_get_os_version_value(3));
#else
    printf("OS/kernel type : %s %s %s\n", 
	    sys_get_os_version_string(0), 
	    sys_get_os_version_string(4), 
	    sys_get_cpu_arch());
#endif

    //time_info
    sys_print_time_info();

    //uuid
    sys_print_uuid();

    //params
    printf("Parameters used:\n");
    print_params(p);
    if (control.interrupted) {
	printf("\nNote: User interruption ended the benchmark earlier than scheduled.\n");
    }

    //machine_info
    printf("\n------------- Machine information -------------\n");
    {
	char modelstr[48];
	if (__cpuid_obtain_brand_string(modelstr)) {
	    printf("CPU model name: %s\n", modelstr);
	} else {
	    printf("CPU model string unsupported.\n");
	}
    }

    //freq_khz
    printf("rdtsc/perfc frequency: %u K cycles per second\n", freq_khz);   		

    //tlb_info
    printf(" -- TLB info --\n");
    print_tlb_info();
    
    //cache_info
    printf(" -- Cache info --\n");
    print_cache_info();

    //result
    printf("\n----------- Average access latency ------------\n");
    print_result();
    
    //statistics
    printf("\n----------------- Statistics ------------------\n");
    p->access->report(buf, p->ratio);
    
    //sys_mem_info
    printf("\n---------- System memory information ----------\n");
    sys_stat_mem_print_header();
    if (params.cold) {
	printf("pre-run   :"); sys_stat_mem_print(&mem_info_before_warmup);//1
	printf("  (delta) :");
	if (mem_info_middle_run.recorded) {
	    sys_stat_mem_print_delta(&mem_info_before_warmup, &mem_info_middle_run);//2
	    printf("mid-run   :"); sys_stat_mem_print(&mem_info_middle_run);	//3
	    printf("  (delta) :"); sys_stat_mem_print_delta(&mem_info_middle_run, &mem_info_after_run);	//4
	}
	else sys_stat_mem_print_delta(&mem_info_before_warmup, &mem_info_after_run); //5
    } else {
	printf("pre-warmup:"); sys_stat_mem_print(&mem_info_before_warmup);//6
	printf("  (delta) :"); sys_stat_mem_print_delta(&mem_info_before_warmup, &mem_info_before_run);	//7
	printf("pre-run   :"); sys_stat_mem_print(&mem_info_before_run);	//8
	printf("  (delta) :");
	if (mem_info_middle_run.recorded) {
	    sys_stat_mem_print_delta(&mem_info_before_run, &mem_info_middle_run);//9
	    printf("mid-run   :"); sys_stat_mem_print(&mem_info_middle_run);	//10
	    printf("  (delta) :"); sys_stat_mem_print_delta(&mem_info_middle_run, &mem_info_after_run);	//11
	}
	else sys_stat_mem_print_delta(&mem_info_before_run, &mem_info_after_run);//12
    }
    printf("post-run  :"); sys_stat_mem_print(&mem_info_after_run);		//13
}

#define MAX_ALARM 4
typedef void (*alarm_fn)(uint64_t now, void* param);

typedef struct alarm_data {
    uint64_t time;
    alarm_fn callback;
    void* data;		    // user private data
} alarm_data;

alarm_data alarms[MAX_ALARM];

static
void _code alarm_arm(int slot, uint64_t time, alarm_fn callback, void* data)
{
    if (slot >= MAX_ALARM || slot < 0) exit(EXIT_FAILURE);
    alarms[slot].time = time;
    alarms[slot].callback = callback;
    alarms[slot].data = data;
}

static
void _code __attribute__((unused)) alarm_clear(int slot)
{
    if (slot >= MAX_ALARM || slot < 0) exit(EXIT_FAILURE);
    alarms[slot].time = 0;
    alarms[slot].callback = NULL;
}

static
void _code alarm_check(uint64_t now)
{
    int i;
    for (i = 0; i < MAX_ALARM; ++i) {
	if (alarms[i].time == 0ull) continue;
	if (alarms[i].time <= now) {
	    // time is cleared first to allow callback re-register at the same slot
	    alarms[i].time = 0ull;
	    alarms[i].callback(now, alarms[i].data);
	}
    }
}

sys_mem_ctx mem_ctx;

/*
 * re-arm alarm triggering every 1 sec.
 * This is left here for alarm sample code
 */
#ifdef __GNUC__
static __attribute__((used))
#endif
void report_stat(uint64_t now, void* param)
{
    sys_mem_ctx* mem_ctx = (sys_mem_ctx*)param;
    sys_mem_item mem_info;

    sys_stat_mem_update(mem_ctx, &mem_info);
    sys_stat_mem_print(&mem_info);
    alarm_arm(0, now + 1 * freq_khz * 1000, report_stat, param);
}

/*
 * one shot memory snapshot
 */
static
void mem_info_oneshot(uint64_t now, void* param)
{
    sys_mem_ctx* mem_ctx = (sys_mem_ctx*)param;

    sys_stat_mem_update(mem_ctx, &mem_info_middle_run);
}

static void* main_bm_thread(void* arg);


#define TS_WARMUP_DONE (1)
#define TS_MAIN_BM_START (2)
static inline
void thread_sync(int syncpoint) {
#ifdef PMB_THREAD
    pthread_barrier_wait(&control.barrier);
#endif
    return;
}

/*
 * access address = (base address of map + (page number << 12) + (10 bit random number) * sizeof(u32) )
 */
static inline 
uint32_t* calc_address(char *buf, size_t pfn) {
    return (uint32_t*)(buf + (pfn << PAGE_SHIFT));
}

/**
 * - main benchmark entry point
 *
 * NB. We want this function and the functions that this function calls to 
 * be confined in a single page, so that there is just one ITLB needed 
 * during the benchmark. We could further compact the code in order to 
 * minimize cache-impact, but that's work to be done.
 */
static void* main_bm_thread(void* arg) _code;
static
__attribute__((aligned(4096)))
void* main_bm_thread(void* arg)
{
    struct thread_info *tinfo = (struct thread_info*)arg;
    int do_memstat = (tinfo->thread_num == 1);

    char* buf = tinfo->map;
    char* stats = control.stats + ((tinfo->thread_num - 1) * 4096); //worker thread numbers start at 1

    struct bench_result* presult = &tinfo->result;

    const parameters* p = &params;
    pattern_generator* pattern = p->pattern;
    access_fn_set* access = p->access;
    uint64_t rand_ctx_offset = (p->offset < 0 ? (uint64_t)(tinfo->thread_num + 7) : (uint64_t)p->offset);
    uint64_t rand_ctx_action = (uint64_t)(tinfo->thread_num + 50);
    struct sys_timestamp* tsops = p->tsops;
    struct stopwatch sw;
    int i;
    uint64_t done_tsc, now;

    uint32_t* a_addr;
    int is_write;   // 0: read, 1: write, 2: write after read
    uint32_t latency_ns;

    /* we upconvert ratio to 0-1023 scale to avoid modular op*/
    int rat_scaled = ((p->ratio)*1024)/100;

    /* note on data type for page count:
     * size_t and ssize_t types are used when consistent integer
     * width is needed across platforms.
     * 'long' is 64bit on 64bit Linux but 32bit on 64bit Windows so
     * using long for holding page count doesn't work. On the other hand
     * using long long type makes life difficult when compiling 32bit.
     */

    size_t num_pages = p->setsize_mib * 256;
    size_t iter_warmup;
    int iter_patternlap = num_pages; // number of draw equal to number of pages
    void* ctx = pattern->alloc_pattern(num_pages, p->shape, tinfo->thread_num);

    prn("[%d] num_pages: %ld (%ld MiB), shape: %0.4f\n", tinfo->thread_num, num_pages, num_pages/256, p->shape);
    sw_reset(&sw, tsops);

    /* do measure pattern generation overhead */

    sw_start(&sw);
    for (i = 0; i < iter_patternlap; ++i) {
	pattern->get_next(ctx);
    }

    sw_stop(&sw);

    presult->total_numgen_clock = sw.elapsed_sum;
    presult->total_numgen_count = iter_patternlap;

    prn("[%d] Pattern generation overhead: %0.4f usec per drawing\n", tinfo->thread_num, (float)sw_get_usec(&sw)/iter_patternlap); // convert msec to usec
    sw_reset(&sw, tsops);

    /* take memory information snapshot */
    if (do_memstat) sys_stat_mem_update(&mem_ctx, &mem_info_before_warmup);

    /* do warmup */
    if (!p->cold) {
	iter_warmup = pattern->get_warmup_run ?
	    pattern->get_warmup_run(ctx) : num_pages;
	    
	prn("[%d] Performing %ld page accesses for warmup\n",
		tinfo->thread_num, iter_warmup);
	sw_start(&sw);
	for (i = 0; i < iter_warmup; ++i) {
	    a_addr = calc_address(buf, pattern->get_next(ctx));
	    a_addr += p->get_offset(&rand_ctx_offset);
	    is_write = (roll_dice(&rand_ctx_action) % 1024) < rat_scaled ? 0 : 1;
	    if (is_write && p->write_needs_read) is_write = 2;

	    access->exercise(a_addr, is_write);
	    // don't need to record, don't need to mark long lats

//	    if (p->delay > 10) sys_delay(p->delay); // no delay in warmup..
	}
	sw_stop(&sw);

	presult->total_warmup_clock = sw.elapsed_sum;
	presult->total_warmup_count = iter_warmup;

	prn("[%d] Warmup done - took %d us\n", tinfo->thread_num, sw_get_usec(&sw));
	sw_reset(&sw, tsops);

	if (do_memstat) sys_stat_mem_update(&mem_ctx, &mem_info_before_run);
    }

    //out_warmup_interrupted:
    thread_sync(TS_WARMUP_DONE);
    /* main thread collects warmup stats between the two sync points */
    thread_sync(TS_MAIN_BM_START);
    prn("[%d] Starting main benchmark\n", tinfo->thread_num);

    done_tsc = (uint64_t)p->duration_sec * freq_khz * 1000;
    if (do_memstat) alarm_arm(tinfo->thread_num - 1, tsops->timestamp() + (done_tsc / 2), mem_info_oneshot, &mem_ctx);
    done_tsc += sw_start(&sw);
	
    alarm_check(now);
    for (i = 0; i < num_pages; ++i) {
	a_addr = calc_address(buf, pattern->get_next(ctx));
	a_addr += p->get_offset(&rand_ctx_offset);
	is_write = (roll_dice(&rand_ctx_action) % 1024) < rat_scaled ? 0 : 1;
	if (is_write && p->write_needs_read) is_write = 2;

	latency_ns = access->exercise(a_addr, is_write);

	access->record(stats, latency_ns, is_write);
#ifndef _WIN32
	if (params.threshold > 0) mark_long_latency(latency_ns);
#endif
	if (p->delay > 10) sys_delay(p->delay);
    }
    sw_stop(&sw);

    if (do_memstat) sys_stat_mem_update(&mem_ctx, &mem_info_after_run);

    presult->total_bench_clock = sw.elapsed_sum;
    presult->total_bench_count = num_pages;

    prn("[%d] Benchmark done - took %0.3f sec for %d page access\n"
        "  (Average %0.3f usec per page access)\n", tinfo->thread_num,
	(float)sw_get_usec(&sw)/1000000.0f, num_pages,
	(float)sw_get_usec(&sw)/(num_pages));

    pattern->free_pattern(ctx);

    return NULL;
}


#define handle_error_en(en, msg) \
    do { errno = en; perror(msg); exit(EXIT_FAILURE); } while (0)

#define handle_error(msg) \
    do { perror(msg); exit(EXIT_FAILURE); } while (0)

#ifdef PMB_THREAD
/*
 * For multi-threaded bm, we have 1 control thread and n worker threads.
 * The control thread (perform_benchmark_mt) does not participate in 
 * measurement - it just coordinates start/finish of worker threads.
 * Worker threads execute main_bm_thread() to perform actual work.
 */
void perform_benchmark_mt(char *buf, char *stats) _code;
void 
//__attribute__((aligned(4096)))
perform_benchmark_mt(char *buf, char *stats)
{
    const int num_threads = params.jobs;
    int s, i;
    struct thread_info *tinfo;
    pthread_attr_t attr;
    void *res;
#ifdef PMB_NUMA
    struct affy_node* iter;
#endif

    pthread_barrier_init(&control.barrier, NULL, num_threads + 1);
    control.stats = stats;

    s = pthread_attr_init(&attr);
    if (s != 0) handle_error_en(s, "pthread_attr_init");

    s = pthread_attr_setstacksize(&attr, 0x100000); /* 1MiB */
    if (s != 0) handle_error_en(s, "pthread_attr_setstacksize");

    tinfo = calloc(num_threads, sizeof(struct thread_info));
    if (tinfo == NULL) handle_error("calloc");

    control.tinfo = tinfo;

#ifdef PMB_NUMA
    if (params.affy_head) {
	i = 0;
	for (iter = params.affy_head; iter != NULL; iter = iter->next) {
	    int j;
	    cpu_set_t cpuset;
	    sys_numa_cpuset_from_cpumask(&cpuset, iter->cpumask);
	    s = pthread_attr_setaffinity_np(&attr, sizeof(cpu_set_t), &cpuset);
	    if (s != 0) handle_error_en(s, "pthread_attr_setaffinity");

	    for (j = 0; j < iter->nthreads; j++) {
		tinfo[i].thread_num = i + 1;
		tinfo[i].map = iter->buf;
		reset_result(&tinfo[i].result);

		s = pthread_create(&tinfo[i].thread_id, &attr,
				&main_bm_thread, &tinfo[i]);
		if (s != 0) handle_error_en(s, "pthread_create");
		i++;
	    }
	}
    } else 
#endif
    for (i = 0; i < num_threads; i++) {
    	tinfo[i].thread_num = i + 1;
    	reset_result(&tinfo[i].result);
	tinfo[i].map = buf;

    	s = pthread_create(&tinfo[i].thread_id, &attr,
    			&main_bm_thread, &tinfo[i]);
    	if (s != 0) handle_error_en(s, "pthread_create");
    }

    s = pthread_attr_destroy(&attr);
    if (s != 0) handle_error_en(s, "pthread_attr_destroy");

    /* sync on warmup finish */
    thread_sync(TS_WARMUP_DONE);

    /* check again for ctrl-c interruption */
    if (control.interrupted) {
	// the benchmark is interrupted during warmup, we just bail the program..
	prn("Benchmark terminated during warmup - report will not be generated\n");
	exit(EXIT_FAILURE);
    }

    // release the hounds - synchronize all threads to start main bm
    thread_sync(TS_MAIN_BM_START);

    /* join workers to finish */
    for (i = 0; i < num_threads; i++) {
	s = pthread_join(tinfo[i].thread_id, &res);
	if (s != 0) handle_error_en(s, "pthread_join");
    }
    prn("All threads joined\n");

    // finish collapses per-thread stats into one
    if (num_threads > 1) {
	params.access->finish(control.stats, num_threads);
    }

    if (control.interrupted) { 
	prn("Benchmark interrupted during run - partial report will be generated\n"); 
    }
}
#else 
extern void perform_benchmark_st(char *buf, char *stats) _code;
void
perform_benchmark_st(char *buf, char *stats)
{
    struct thread_info* tinfo;

    tinfo = calloc(1, sizeof(struct thread_info));
    if (tinfo == NULL) exit(EXIT_FAILURE);
    tinfo->thread_num = 1;
    tinfo->map = buf;
    reset_result(&tinfo->result);

    control.tinfo = tinfo;
    control.stats = stats;
    control.interrupted = 0;

    main_bm_thread((void*)tinfo);
}
#endif

static
void conclude_benchmark_early(void)
{
	prn("benchmark interrupted..\n");
   control.interrupted = 1;
   return;
}

#if _WIN32
static
BOOL ConsoleCtrlHandler(DWORD fdwCtrlType)
{
    if (fdwCtrlType != CTRL_C_EVENT) return FALSE;

    conclude_benchmark_early();
    return TRUE;
}
#else
static
void sigint_sigaction_entry(int signum, siginfo_t* info, void* ptr)
{
    int errno_saved = errno;

    if (info->si_signo != SIGINT) {
		prn("wrong signal??\n");
		goto out;
    }
    conclude_benchmark_early();

    // revert back to default disposition
	signal(signum, SIG_DFL);

	errno = errno_saved;
	return;

out:
	// perform default action.
	signal(signum, SIG_DFL);
	raise(signum);
}
#endif

#if _WIN32
static
int install_ctrlc_handler(void)
{
    BOOL ret;
    ret = SetConsoleCtrlHandler((PHANDLER_ROUTINE)ConsoleCtrlHandler, TRUE);

    return ret;
}
static
int remove_ctrlc_handler(void)
{
    BOOL ret;
    ret = SetConsoleCtrlHandler((PHANDLER_ROUTINE)ConsoleCtrlHandler, FALSE);

    return ret;
}
#else
static
int install_ctrlc_handler(void)
{
    int ret;

    struct sigaction sa, sa_saved;
    sigemptyset(&sa.sa_mask);

    sa.sa_sigaction = sigint_sigaction_entry;
    sa.sa_flags = SA_SIGINFO;

    ret = sigaction(SIGINT, &sa, &sa_saved);

    if (ret == -1) {
	prn("%s: sigint handler installation failed\n", __func__);
	perror("sigaction call failed\n");
	exit(EXIT_FAILURE);
    }
    return ret;
}

/*
 * set SIGINT disposition to default, which is to terminate
 */
static
int remove_ctrlc_handler(void)
{
    int ret = 0;
    struct sigaction sa;
    sigemptyset(&sa.sa_mask);

    sa.sa_sigaction = (void*)SIG_DFL;
    ret = sigaction(SIGINT, &sa, NULL);

    if (ret == -1) {
    	prn("%s: sigint handler removal failed\n", __func__);
    	perror("sigaction call failed\n");
		exit(EXIT_FAILURE);
    }
    return ret;
}
#endif

#ifdef _WIN32
void disable_core_dump(void)
{
    return; // what's the equivalent?
}
#else
void disable_core_dump(void)
{
    struct rlimit rlim = {.rlim_cur = 0, .rlim_max = 0 };
    int res;

    res = setrlimit(RLIMIT_CORE, &rlim);
    if (res != 0) {
    	perror("failed to set core dump size to zero\n");
    }
}
#endif

extern void print_xml_report(char* buf, const parameters* p, int is_interrupted);
extern void print_xml_report_post_unmap(const char* path);

/*
 * N.B. mingw32 intercepts WinMain and prepares argc and argv
 */
int main(int argc, char** argv)
{
    size_t map_num_pfn; 
    int ret;

    char *buf, *stats;
#ifdef XALLOC
    printf("Sorry, this version does not support XALLOC!\n");
    return 1;
    struct xalloc* ctx = 0; 
#endif
    set_default_params(&params);
    params_parsing(argc, argv);
    disable_core_dump();

//test_parse_numa_option();

    if (!is_tsc_invariant()) {
	prn("WARNING: CPU does not support constant-rate rdtsc. Results obtained via rdtsc(p) may be inaccurate!\n");
    }
    if ((params.tsops == &rdtscp_ops) && (!is_rdtscp_available())) {
    	prn("INFO: specified rdtscp, which is unsupported by the CPU. Using rdtsc instead.\n");
    	params.tsops = &rdtsc_ops;
    }
#ifdef WIN32
    {
    	SYSTEM_INFO sysinfo;
    	GetNativeSystemInfo(&sysinfo);
    	if (sysinfo.dwPageSize != 4096) {
	    prn("ERROR: system page size is not 4K.\n");
	    return 1;
    	}
    }
    perfc_ops.init_base_freq(&perfc_ops);
#else
    if (sysconf(_SC_PAGESIZE) != 4096) {
	prn("ERROR: system page size is not 4K.\n");
    	return 1;
    }
#endif
    rdtsc_ops.init_base_freq(&rdtsc_ops);
    rdtscp_ops.init_base_freq(&rdtscp_ops);

    freq_khz = params.tsops->base_freq_khz;

    map_num_pfn = params.mapsize_mib * 256;

#ifdef XALLOC
    if (params.xalloc_mib) {
    	ctx = xalloc_get_context();
    	xalloc_lib_set_log_file(STDERR_FILENO);
    	xalloc_lib_log_info();
    	//xalloc_lib_log_debug();

    	xalloc_init_context(ctx);

    	if (xalloc_attach_device(ctx, params.xalloc_path)) {
	    prn("attaching device failed: %s\n", params.xalloc_path);
	    return 1;
    	}
    	/* xmmap memory size in # of 4K pages, not byte size */
    	size_t p_pgcount = params.xalloc_mib << (20 - PAGE_SHIFT);
    	if (p_pgcount > map_num_pfn) p_pgcount = map_num_pfn;

    	buf = xmmap(ctx, map_num_pfn, p_pgcount, XALLOC_MAP_ATTR_READWRITE);

    	if (buf == XMMAP_FAILED) {
	    perror("xmmap failed");
	    return 1;
    	}

    	// xmmap returns memory unscrubbed. zero out the memory.
    	memset(buf, 0, map_num_pfn << 12);
    } else
#endif
    {
#ifdef _WIN32
	buf = VirtualAlloc(NULL, map_num_pfn * PAGE_SIZE, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
	if (buf == NULL) {
	    ret = GetLastError();
	    prn("VirtualAlloc failed. Error:%d\n", ret);
	    prn("sizeof(map_num_pfn):%d, map_num_pfn:%ld, map_num_pfn*PAGE_SIZE:%"PRIu64"\n", sizeof(map_num_pfn), map_num_pfn, map_num_pfn * PAGE_SIZE);
	    return 1;
	}

	stats = VirtualAlloc(NULL, PAGE_SIZE * params.jobs, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
	if (stats == NULL) {
	    ret = GetLastError();
	    prn("stats VirtualAlloc failed. Error:%d\n", ret);
	    return 1;
	}
#else
#ifdef PMB_NUMA
	if (params.affy_head) {
	    long r;
	    ret = alloc_affy_buffers(params.affy_head, map_num_pfn);
	    if (ret) return 1;

	    // memory for histogram. 
	    // 1 page per thread; read and write statistics get 2k each
	    stats = mmap(NULL, (size_t)(PAGE_SIZE * params.jobs), 
		    PROT_READ | PROT_WRITE, MAP_PRIVATE | MAP_ANONYMOUS, -1, 0); 
	    if (stats == MAP_FAILED) {
		perror("stats mmap failed");
		return 1;
	    }
//TODO: check for kernel >= 3.8 for MPOL_LOCAL kernel support
#ifndef MPOL_LOCAL
#define MPOL_LOCAL 4
#endif
	    r = mbind(stats, PAGE_SIZE * params.jobs, MPOL_LOCAL, NULL, 0, 0);
	    if (r) {
		perror("stats mbind() failed");
		return 1;
	    }
	    buf = NULL;
	} else 
#endif
	{
	    int permissions = PROT_READ;
	    if (params.ratio < 100 || params.init_garbage) permissions |= PROT_WRITE;

	    buf = mmap(NULL, map_num_pfn * PAGE_SIZE, permissions, 
		    MAP_PRIVATE | MAP_ANONYMOUS, -1, 0);
	    if (buf == MAP_FAILED) {
		perror("buf mmap failed");
		return 1;
	    }

	    // memory for histogram. 
	    // 1 page per thread; read and write statistics get 2k each
	    stats = mmap(NULL, (size_t)(PAGE_SIZE * params.jobs), 
		    PROT_READ | PROT_WRITE, MAP_PRIVATE | MAP_ANONYMOUS, -1, 0); 
	    if (stats == MAP_FAILED) {
		perror("stats mmap failed");
		return 1;
	    }
	}
#endif
	//XXX fill in garbage 
	//PMB_NUMA - main thread shouldn't be touching the map..
	//FIXME .. for now don't allow init when affinityset is set
	if (params.init_garbage) {
	    if (buf == NULL) { 
		prn("Fixme: can't init the map when affinityset is set. proceeding without initializing");
	    } else {
		prn("Initializing memory map...\n");
		int i, j;
		uint64_t state = 0x0ddfadedbeefd00d;
		uint32_t val;
		struct stopwatch sw_init;
		sw_reset(&sw_init, params.tsops);
		sw_start(&sw_init);
		for (i = 0; i < map_num_pfn; i++) {
		    for (j = 0; j < PAGE_SIZE; j+=4) {
			state = state * 6364136223846793005ull + 1442695040888963407ull;
			val = (uint32_t)(state >> 33);
			buf[i*PAGE_SIZE + j] = val;
		    }
		}
		sw_stop(&sw_init);
		prn("Initialization took %0.4f ms\n", ((float)sw_get_usec(&sw_init))/1000.0);
		prn("Dropping caches...\n");
		static const char command[] = "/opt/drop_caches.sh";
		int rc = system(command);
		if (rc != 0) {
		    prn("drop_caches.sh returned %d\n", rc);
		}
	    }
	}
#ifdef _WIN32
	else prn("WARNING: uninitialized memory causes side effect on" 
		" OS with memory compression and deduplication.\n");
#endif
    }
    //debug_verify_distributions();
    sys_stat_mem_init(&mem_ctx);
    control.interrupted = 0;
    install_ctrlc_handler();
#ifndef _WIN32
    trace_marker_init();
#endif

#ifdef PMB_THREAD
    perform_benchmark_mt(buf, stats);
#else
    perform_benchmark_st(buf, stats);
#endif

    print_con_report(stats, &params);

    if (params.xml_path) print_xml_report(stats, &params, control.interrupted);

    free(control.tinfo);

#ifdef _WIN32
#ifdef XALLOC
    if (params.xalloc_mib) {
	ret = xunmap(ctx);
	if (ret) {
	    perror("xunmap failed");
	    goto report_no_unmap;
	}
    } else
#endif
    {
	// VirtualFree requires dwSize to be 0 when memory region is released
	ret = VirtualFree(buf, 0, MEM_RELEASE);	
	if (!ret) {
	    ret = GetLastError();
	    prn("VirtualFree failed. Error:%d", ret);
	    goto report_no_unmap;
	}
	ret = VirtualFree(stats, 0, MEM_RELEASE);
	if (!ret) {	
	    ret = GetLastError();
	    prn("VirtualFree stats failed. Error:%d", ret);
	    goto report_no_unmap;
	}
    }
#else
#ifdef XALLOC
    if (params.xalloc_mib) {
	ret = xunmap(ctx);
	if (ret) {
	    perror("xunmap failed");
	    goto report_no_unmap;
	}
    } else
#endif
    {
#ifdef PMB_NUMA
	if (params.affy_head) {
	    ret = free_affy_buffers(params.affy_head, map_num_pfn);
	    if (ret) {
		perror("free_affy_buffers failed");
		goto report_no_unmap;
	    }
	} else 
#endif
	{
	    ret = munmap(buf, map_num_pfn * PAGE_SIZE);

	    if (ret) {
		perror("munmap failed");
		goto report_no_unmap;
	    }
	}

	ret = munmap(stats, (size_t)(PAGE_SIZE * params.jobs));
	if (ret) {
	    perror("stats munmap failed");
	    goto report_no_unmap;
	}
    }
#endif
    sys_stat_mem_update(&mem_ctx, &mem_info_after_unmap);
    printf("  (delta) :"); sys_stat_mem_print_delta(&mem_info_after_run, &mem_info_after_unmap);
    printf("post unmap:"); sys_stat_mem_print(&mem_info_after_unmap);

    if (params.xml_path) print_xml_report_post_unmap(params.xml_path);

    sys_stat_mem_exit(&mem_ctx);

#ifdef XALLOC
    if (params.xalloc_mib) {
	if (xalloc_detach_device(ctx)) prn("detach_device() failed\n");
	if (xalloc_destroy_context(ctx)) prn("destroy_context() failed\n");
    }
#endif

#ifndef _WIN32
    trace_marker_exit();
#endif
    remove_ctrlc_handler();

    return 0;

report_no_unmap:

#ifdef XALLOC
    if (params.xalloc_mib) {
	if (xalloc_detach_device(ctx)) prn("detach_device() failed\n");
	if (xalloc_destroy_context(ctx)) prn("destroy_context() failed\n");
    }
#endif
    sys_stat_mem_exit(&mem_ctx);
    return 1;
}
