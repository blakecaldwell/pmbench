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

/*  Writen by: Jisoo Yang <jisoo.yang@intel.com>  */

#define _GNU_SOURCE
#include <memory.h>

#include <stdlib.h>
#include <stdio.h>
#include <signal.h>
#include <string.h>
#include <math.h>
#include <stdarg.h>
#include <inttypes.h>

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
#include <sys/stat.h>
#include <semaphore.h>
#include <pthread.h>
#endif

#include "system.h"

#include "rdtsc.h"
#include "cpuid.h"
#include "pattern.h"
#include "access.h"


#define PAGE_SHIFT (12)
#define PAGE_SIZE (1<<PAGE_SHIFT)

#ifdef _WIN32
#define OPT_TAG_OS " Windows"
#else
#define OPT_TAG_OS " Linux"
#endif
#ifdef __x86_64__
#define OPT_TAG_BITS " 64bit"
#else
#define OPT_TAG_BITS " 32bit"
#endif
#ifdef PMB_THREAD
#define OPT_TAG_PMB_THREAD " +mt"
#else
#define OPT_TAG_PMB_THREAD " -mt"
#endif
#ifdef XALLOC
#define OPT_TAG_XALLOC " +xa"
#else
#define OPT_TAG_XALLOC " -xa"
#endif

#define COMPILE_OPT_TAGS  OPT_TAG_OS OPT_TAG_BITS OPT_TAG_PMB_THREAD OPT_TAG_XALLOC

/*
 * Program arguments handling
 */
static struct argp_option options[] = {
    { "mapsize", 'm', "MAPSIZE", 0, "Mmap size in MiB" },
    { "setsize", 's', "SETSIZE", 0, "Working set size in MiB" },
    { "access", 'a', "ACCESS", 0, "Specify access method. e.g., touch, histo" },
    { "pattern", 'p', "PATTERN", 0, "Specify PATTERN. e.g, linear, uniform(def), pareto, normal" },
    { "shape", 'e', "SHAPE", 0, "Pattern-specific parameter" },
    { "quiet", 'q', 0, 0, "Don't produce any output until finish" },
    { "cold", 'c', 0, OPTION_ARG_OPTIONAL, "Don't perform warm-up exercise" },
    { "timestamp", 't', "TIMESTAMP", 0, "Specify TIMESTAMP. rdtsc(def), rdtscp, or perfc" },
#ifdef PMB_THREAD
    { "jobs", 'j', "NUMJOBS", 0, "Number of concurrent jobs (threads)" },
#endif
#ifdef XALLOC
    { "xalloc", 'x', "REALMEMSIZE", 0, "Non-zero REAMMEMSIZE (MiB) enables xalloc. xmmap uses REALMEMSIZE of real memory" },
    { "xalloc_path", 'X', "PATHNAME", 0, "xalloc backend device pathname. default is /dev/mem0" },
#endif
    { 0 }
};

//const char* bd_path = "/mnt/tmpfs/xalloc_blob.img";
/* 
 * No lock needed for threads access to params.
 * (once set at program start, parameters are only read until program exit)
 */ 
typedef struct parameters {
    int duration_sec;	    // benchmark duration in seconds (excluding warmup)
    int mapsize_mib;	    // anonymous mmap size in megabytes
    int setsize_mib;	    // 'working set' size in megabytes (depends on pattern)
    access_fn_set* access;  // access method (touch or histo)
    pattern_generator* pattern;	// benchmark pattern
    double shape;	    // 'shape' parameter to use for pattern
    int quiet;		    // no output until done
    int cold;		    // don't perform warm up exercise before benchmark
    struct sys_timestamp* tsops; // timestamp ops (rdtsc_ops or perfc_ops)
    int jobs;		    // number of worker threads (only for linux for now)
    int xalloc_mib;	    // positive xalloc_mib indicates we use xalloc instead of mmap
    char* xalloc_path;	    // xalloc backend file pathname
} parameters;

static parameters params;

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
    p->quiet = 0;
    p->cold = 0;
    p->tsops = &rdtsc_ops;
    p->jobs = 1;
    p->xalloc_mib = 0;
    p->xalloc_path = "/dev/ram0";
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
    printf("  shape        = %f\n", p->shape);
    printf("  quiet        = %d\n", p->quiet);
    printf("  cold         = %d\n", p->cold);
    printf("  jobs         = %d\n", p->jobs);
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
}

/* argp parse callback */
static
__attribute__((cold))
error_t parse_opt(int key, char* arg, struct argp_state* state)
{
    parameters* param = state->input;

    switch (key) {
    case 'm':
	if (arg) param->mapsize_mib = atoi(arg);
	break;
    case 's':
	if (arg) param->setsize_mib = atoi(arg);
	break;
#ifdef PMB_THREAD
    case 'j':
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
    case ARGP_KEY_NO_ARGS:
	break;
    case ARGP_KEY_ARG:
	if (state->arg_num >= 1) argp_usage(state);
	param->duration_sec = arg ? atoi(arg) : 0;
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
    static const char program_doc_str[] = "pmbench - System Paging/Swapping/Memory Benchmark";
    static const char args_doc_str[] = "DURATION";

    static struct argp argp = { options, parse_opt, args_doc_str, program_doc_str };
    /* N.B. to deal with Windows dll linkage issue, we set these variables here
     * instead of statical assignment */
    argp_program_version = "pmbench 0.7" COMPILE_OPT_TAGS;
    argp_program_bug_address = "Jisoo Yang <jisoo.yang@intel.com>";

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
    if (params.jobs < 1) {
	printf("invalid parameter combination: jobs less than zero\n");
	exit(EXIT_FAILURE);
    }
    return 0;
}

/*
 * logging - thread safe. N.B. it only makes individual prn call atomic.
 * mingw32 seems to be breaking printf thread atomicity in Windows...
 */
#ifdef PMB_THREAD
pthread_mutex_t lock_prn = PTHREAD_MUTEX_INITIALIZER;
#endif
static inline
int prn(const char* format, ...)
{
    va_list ap;
    int len;

    if (params.quiet) return 0;

#ifdef PMB_THREAD
    pthread_mutex_lock(&lock_prn);
#endif
    va_start(ap, format);
    len = vprintf(format, ap);
    va_end(ap);
#ifdef PMB_THREAD
    pthread_mutex_unlock(&lock_prn);
#endif

    return len;
}


unsigned freq_khz;

/* benchmark result processing */
struct bench_result {
    uint64_t total_bench_clock;
    uint64_t total_bench_count;
    uint64_t total_warmup_clock;
    uint64_t total_warmup_count;
    uint64_t total_numgen_clock;	// pattern generation overhead
    uint64_t total_numgen_count;
    int stat_major_fault_clock;
    int stat_minor_fault_clock;
};

/* mean_us must do float conversion first to avoid truncation error */
#define mean_us(name) \
(((float)presult->total_##name##_clock / presult->total_##name##_count) * 1000 /freq_khz)

#define mean_clk(name) \
(presult->total_##name##_clock / presult->total_##name##_count)

static inline
float clk_to_us(uint64_t clk) {
    return (float)((clk * 1000) / freq_khz);
}

static inline
int us_to_clk(float us) {
    return (int)(us * freq_khz / 1000);
}

/* this is for single thread version */
static struct bench_result result;

static
void reset_result(struct bench_result* presult)
{
    memset(presult, 0, sizeof(*presult));
}

#ifdef PMB_THREAD
/* 
 * Barrier (pthread_barrier_wait()) or semaphore may be more convenient.
 * we just stick to using condition variable here for win32 port sanity..
 */

/* per thread info */
struct thread_info {
    pthread_t thread_id;	    // returned by pthread_create
    int thread_num;		    // local thread sequence number (1, 2, 3,...)
    pthread_mutex_t lock_gate;	    // control warmup finish
    struct bench_result result;	    // per-thread result
};

/* thread-shared for concurrency control and shared variable access */
struct thread_control {
    struct thread_info* tinfo;	    // thread info array created
    int num_warmup;		    // protected by lock_warmup
//    pthread_barrier_t barr_warmup;  // 
    pthread_mutex_t lock_warmup;    // 
    pthread_cond_t cond_warmup_done;// 
    char* buf;			    // memory map base pointer for access
    int interrupted;		    // ctrl-c sets this
} control;

static inline
struct bench_result* get_per_thread_result(int i) {
    if (params.jobs == 1) return &result;
    else return &control.tinfo[i].result;
}
#else
struct thread_info {
    int thread_num;
};

struct thread_control {
    char* buf;
    int interrupted;
} control;

static inline
struct bench_result* get_per_thread_result(int i) {
    return &result;
}
#endif

static
void print_result(void)
{
    int i;
    for (i = 0; i < params.jobs; i++) {
	struct bench_result* presult = get_per_thread_result(i);
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
print_report(char* buf, const parameters* p)
{
    printf("\n------------- Benchmark signature -------------\n");
    sys_print_pmbench_info();
    sys_print_os_info();
    sys_print_time_info();
    sys_print_uuid();
    printf("Parameters used:\n");
    print_params(p);
    if (control.interrupted) {
	printf("\nNote: User interruption ended the benchmark earlier than scheduled.\n");
    }

    printf("\n------------- Machine information -------------\n");
    {
	char modelstr[48];
	if (__cpuid_obtain_model_string(modelstr)) {
	    printf("CPU model name: %s\n", modelstr);
	} else {
	    printf("CPU model string unsupported.\n");
	}
    }
    printf("rdtsc/perfc frequency: %u K cycles per second\n", freq_khz);

    printf(" -- TLB info --\n");
    print_tlb_info();
    printf(" -- Cache info --\n");
    print_cache_info();
    printf("\n----------- Average access latency ------------\n");
    print_result();
    printf("\n----------------- Statistics ------------------\n");
    p->access->report(buf);
    printf("\n---------- System memory information ----------\n");
    sys_stat_mem_print_header();
    if (params.cold) {
	printf("pre-run   :");
	sys_stat_mem_print(&mem_info_before_warmup);
	printf("  (delta) :");
	if (mem_info_middle_run.recorded) {
	    sys_stat_mem_print_delta(&mem_info_before_warmup, &mem_info_middle_run);
	    printf("mid-run   :");
	    sys_stat_mem_print(&mem_info_middle_run);
	    printf("  (delta) :");
	    sys_stat_mem_print_delta(&mem_info_middle_run, &mem_info_after_run);
	} else {
	    sys_stat_mem_print_delta(&mem_info_before_warmup, &mem_info_after_run);
	}
    } else {
	printf("pre-warmup:");
	sys_stat_mem_print(&mem_info_before_warmup);
	printf("  (delta) :");
	sys_stat_mem_print_delta(&mem_info_before_warmup, &mem_info_before_run);
	printf("pre-run   :");
	sys_stat_mem_print(&mem_info_before_run);
	printf("  (delta) :");
	if (mem_info_middle_run.recorded) {
	    sys_stat_mem_print_delta(&mem_info_before_run, &mem_info_middle_run);
	    printf("mid-run   :");
	    sys_stat_mem_print(&mem_info_middle_run);
	    printf("  (delta) :");
	    sys_stat_mem_print_delta(&mem_info_middle_run, &mem_info_after_run);
	} else {
	    sys_stat_mem_print_delta(&mem_info_before_run, &mem_info_after_run);
	}
    }
    printf("post-run  :");
    sys_stat_mem_print(&mem_info_after_run);
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

extern void perform_benchmark_st(char* buf) _code;
void
perform_benchmark_st(char* buf) 
{
    struct thread_info info_single = {
	.thread_num = 1,
    };
    control.buf = buf;
    main_bm_thread((void*)&info_single);
    params.access->finish(buf, params.setsize_mib * 256);
}

#ifdef PMB_THREAD
static
void 
_code
thread_sync_warmup(struct thread_info* tinfo)
{
    pthread_mutex_lock(&control.lock_warmup);
    control.num_warmup--;
    pthread_mutex_unlock(&control.lock_warmup);
    pthread_cond_signal(&control.cond_warmup_done);

    pthread_mutex_lock(&tinfo->lock_gate);
}
#else
#define thread_sync_warmup(_p_) do {;} while(0)
#endif

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

    char* buf = control.buf;

#ifdef PMB_THREAD
    struct bench_result* presult = (params.jobs == 1) ? &result: &tinfo->result;
#else
    struct bench_result* presult = &result;
#endif

    const parameters* p = &params;
    pattern_generator* pattern = p->pattern;
    access_fn_set* access = p->access;
    struct sys_timestamp* tsops = p->tsops;
    struct stopwatch sw;
    int i, tenk;
    uint64_t done_tsc, now;

    /* note on data type for page count:
     * size_t and ssize_t types are used when consistent integer
     * width is needed across platforms.
     * 'long' is 64bit on 64bit Linux but 32bit on 64bit Windows so
     * using long for holding page count doesn't work. Onthe other hand
     * using long long type makes life difficult when compiling 32bit.
     */

    size_t num_pages = p->setsize_mib * 256;
    size_t iter_warmup;
    int iter_patternlap = 1000000; // draw 1000000
    void* ctx = pattern->alloc_pattern(num_pages, p->shape, tinfo->thread_num);

    prn("[%d] Thread created\n", tinfo->thread_num);

    prn("[%d] num_pages: %ld (%ld MiB), shape: %0.4f\n", tinfo->thread_num,
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

    prn("[%d] Pattern generation overhead: %0.4f usec per drawing\n", tinfo->thread_num,
	    (float)sw_get_usec(&sw)/iter_patternlap); // convert msec to usec
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
	    access->warmup(buf, pattern->get_next(ctx));
	    if (control.interrupted) goto out_warmup_interrupted;
	}
	sw_stop(&sw);

	presult->total_warmup_clock = sw.elapsed_sum;
	presult->total_warmup_count = iter_warmup;

	prn("[%d] Warmup done - took %d us\n", tinfo->thread_num, sw_get_usec(&sw));
	sw_reset(&sw, tsops);

	if (do_memstat) sys_stat_mem_update(&mem_ctx, &mem_info_before_run);
    }

out_warmup_interrupted:

    thread_sync_warmup(tinfo);
    prn("[%d] Starting main benchmark\n", tinfo->thread_num);

    /* do benchmark */
    tenk = 0;

    done_tsc = (uint64_t)p->duration_sec * freq_khz * 1000; 
    if (do_memstat) alarm_arm(tinfo->thread_num - 1, tsops->timestamp() + (done_tsc / 2), mem_info_oneshot, &mem_ctx);
    done_tsc += sw_start(&sw);

    while ((now = tsops->timestamp()) < done_tsc) {
	alarm_check(now);
	for (i = 0; i < 10000; ++i) {
	    access->exercise(buf, pattern->get_next(ctx));
	}
	tenk++;
	if (control.interrupted) break;
    }

    sw_stop(&sw);
    
    if (do_memstat) sys_stat_mem_update(&mem_ctx, &mem_info_after_run);

    presult->total_bench_clock = sw.elapsed_sum;
    presult->total_bench_count = tenk * 10000;
    
    prn("[%d] Benchmark done - took %0.3f sec for %d page access\n"
        "  (Average %0.3f usec per page access)\n", tinfo->thread_num,
	(float)sw_get_usec(&sw)/1000000.0f, tenk*10000,
	(float)sw_get_usec(&sw)/(tenk*10000));
    pattern->free_pattern(ctx);

    return NULL;
}

#ifdef PMB_THREAD

#define handle_error_en(en, msg) \
    do { errno = en; perror(msg); exit(EXIT_FAILURE); } while (0)

#define handle_error(msg) \
    do { perror(msg); exit(EXIT_FAILURE); } while (0)

void perform_benchmark_mt(char* buf) _code;
void 
//__attribute__((aligned(4096)))
perform_benchmark_mt(char* buf) 
{
    const int num_threads = params.jobs;

    int s, i;
    struct thread_info *tinfo;
    pthread_attr_t attr;
    void *res;

    control.num_warmup = num_threads;
    pthread_mutex_init(&control.lock_warmup, NULL);
    pthread_cond_init(&control.cond_warmup_done, NULL);
    control.buf = buf;

    s = pthread_attr_init(&attr);
    if (s != 0) handle_error_en(s, "pthread_attr_init");

    s = pthread_attr_setstacksize(&attr, 0x100000); /* 1MiB */
    if (s != 0) handle_error_en(s, "pthread_attr_setstacksize");

    tinfo = calloc(num_threads, sizeof(struct thread_info));
    if (tinfo == NULL) handle_error("calloc");

    control.tinfo = tinfo;

    for (i = 0; i < num_threads; i++) {
	tinfo[i].thread_num = i + 1;
	pthread_mutex_init(&tinfo[i].lock_gate, NULL);
	reset_result(&tinfo[i].result);
	pthread_mutex_lock(&tinfo[i].lock_gate);

	s = pthread_create(&tinfo[i].thread_id, &attr,
		&main_bm_thread, &tinfo[i]);
	if (s != 0) handle_error_en(s, "pthread_create");
    }
    s = pthread_attr_destroy(&attr);
    if (s != 0) handle_error_en(s, "pthread_attr_destroy");

    /* gate control for warmup finish */
    pthread_mutex_lock(&control.lock_warmup);
    while (control.num_warmup > 0) {
	pthread_cond_wait(&control.cond_warmup_done, &control.lock_warmup);
    }
    pthread_mutex_unlock(&control.lock_warmup);

    /* check again for ctrl-c interruption */
    if (control.interrupted) {
	// the benchmark is interrupted during warmup, we just bail the program..
	prn("Benchmark terminated during warmup - report will not be generated\n");
	exit(EXIT_FAILURE);
    }
     
    /* release the hounds */
    for (i = 0; i < num_threads; i++) {
	pthread_mutex_unlock(&tinfo[i].lock_gate);
    }

    /* join workers to finish */
    for (i = 0; i < num_threads; i++) {
	s = pthread_join(tinfo[i].thread_id, &res);
	if (s != 0) handle_error_en(s, "pthread_join");
    }
    prn("All threads joined\n");

    if (control.interrupted) {
	prn("Benchmark interrupted during run - partial report will be generated\n");
    }

    params.access->finish(buf, params.setsize_mib * 256);
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

/*
 * N.B. mingw32 intercepts WinMain and prepares argc and argv
 */
int main(int argc, char** argv)
{
    size_t map_num_pfn; 
    int ret;
    char* buf;

#ifdef XALLOC
    struct xalloc* ctx = 0; 
#endif

    set_default_params(&params);
    
    params_parsing(argc, argv);

    reset_result(&result);

    /*
     * Want to aviod dropping a huge core file when pmbench crashes.
     * Comment out below if pmbench needs debugging.
     */
    disable_core_dump(); 
    //buf = (char*)((1ull); buf[0]= 1;// force crash
    
    if (!is_tsc_invariant()) {
	prn("WARNING: CPU do not support constant-rate rdtsc. Results obtained via rdtsc(p) may be inaccurate!\n");
    }
    if ((params.tsops == &rdtscp_ops) && (!is_rdtscp_available())) {
	prn("INFO: specified rdtscp, which is unsupport by the CPU. Using rdtsc instead.\n");
	params.tsops = &rdtsc_ops;
    }
    
    rdtsc_ops.init_base_freq(&rdtsc_ops);
    rdtscp_ops.init_base_freq(&rdtscp_ops);
    perfc_ops.init_base_freq(&perfc_ops);

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
    }
#endif

#ifdef _WIN32
#ifdef XALLOC
    if (params.xalloc_mib) {
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
    } else {
#endif
	buf = VirtualAlloc(NULL, map_num_pfn * PAGE_SIZE, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
	if (buf == NULL) {
	    ret = GetLastError();
	    prn("VirtualAlloc failed. Error:%d\n", ret);
	    prn("sizeof(map_num_pfn):%d, map_num_pfn:%ld, map_num_pfn*PAGE_SIZE:%"PRIu64"\n", 
		sizeof(map_num_pfn), map_num_pfn, map_num_pfn * PAGE_SIZE);
	    return 1;
	}
#ifdef XALLOC
    }
#endif
#else
#ifdef XALLOC
    if (params.xalloc_mib) {
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
    } else {
#endif
	buf = mmap(NULL, map_num_pfn * PAGE_SIZE, PROT_READ | PROT_WRITE, 
		    MAP_PRIVATE | MAP_ANONYMOUS, -1, 0);

	if (buf == MAP_FAILED) {
	    perror("mmap failed");
	    return 1;
	}
#ifdef XALLOC
    }
#endif
#endif

//debug_verify_distributions();
    sys_stat_mem_init(&mem_ctx);
   
    control.interrupted = 0;
    install_ctrlc_handler();

#ifdef PMB_THREAD
    perform_benchmark_mt(buf);
    print_report(buf, &params);
    free(control.tinfo);
#else
    perform_benchmark_st(buf);
    print_report(buf, &params);
#endif


#ifdef _WIN32
#ifdef XALLOC
    if (params.xalloc_mib) {
	ret = xunmap(ctx);
	if (ret) {
	    perror("xunmap failed");
	    goto report_no_unmap;
	}
    } else {
#endif
	// VirtualFree requires dwSize to be 0 when memory region is released
	ret = VirtualFree(buf, 0, MEM_RELEASE);
	if (!ret) {
	    ret = GetLastError();
	    prn("VirtualFree failed. Error:%d", ret);
	    goto report_no_unmap;
	}
#ifdef XALLOC
    }
#endif
#else
#ifdef XALLOC
    if (params.xalloc_mib) {
	ret = xunmap(ctx);
	if (ret) {
	    perror("xunmap failed");
	    goto report_no_unmap;
	}
    } else {
#endif
	ret = munmap(buf, map_num_pfn * PAGE_SIZE);

	if (ret) {
	    perror("munmap failed");
	    goto report_no_unmap;
	}
#ifdef XALLOC
    }
#endif
#endif

    sys_stat_mem_update(&mem_ctx, &mem_info_after_unmap);

    printf("  (delta) :");
    sys_stat_mem_print_delta(&mem_info_after_run, &mem_info_after_unmap);
    printf("post unmap:");
    sys_stat_mem_print(&mem_info_after_unmap);
    sys_stat_mem_exit(&mem_ctx);

#ifdef XALLOC
    if (params.xalloc_mib) {
	if (xalloc_detach_device(ctx)) prn("detach_device() failed\n");

	if (xalloc_destroy_context(ctx)) prn("destroy_context() failed\n");
    }
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
