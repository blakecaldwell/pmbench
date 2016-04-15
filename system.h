#ifndef __SYSTEM_H__
#define __SYSTEM_H__ 
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

#ifdef _WIN32

#ifndef __MINGW32__
#error "Unsupported compiler"
#endif

#define _GNU_SOURCE
#include <fcntl.h>
#include <stdlib.h>
#include <stdio.h>
#include <string.h>
#include <windows.h>

#include <unistd.h>
#include "rdtsc.h"

#else

#define _GNU_SOURCE
#include <fcntl.h>
#include <unistd.h>
#include <stdlib.h>
#include <stdio.h>
#include <string.h>

#include "rdtsc.h"

#endif // !_WIN32

#define sys_barrier() asm volatile ("":::"memory")

//#define __hot__ __attribute__((section("pmbench_critical")))
#define _code __attribute__((section(".pmbench_code_page")))

/* TODO: inline random() and other C functions used in generator 
 * currently they are called via plt table
 * */

#define PAGE_SHIFT (12)
#define PAGE_SIZE (1<<PAGE_SHIFT)

/* find last (MSB) set bit. e.g., flsl(0) = 0, flsl(1) = 1, 
 * flsl(2) = 2, flsl(3) = 2, flsl(4) = 3, flsl(5) = 3,
 * flsl(15) = 4, flsl(16) = 5, flsl(17) = 5 ... */
#if defined(_WIN32) && defined(__MSVC__)
static inline int __my_flsl(unsigned x)
{
    unsigned r = 0;

    while (x >>= 1) r++;

    return r;
}
#else
static inline int __my_flsl(unsigned x)
{
    int ret;
    asm volatile (
	"bsr %1, %0\n"	    // bit scan to right
	"cmovz %2, %0\n"    // conditional move when source is not zero
	: "=&r"(ret) : "rm" (x), "rm" (-1));
    return ret + 1;
}
#endif

static inline int ilog2(unsigned x) {
    return __my_flsl(x) - 1;
}

static inline void __pause(void) {
    asm volatile ( "pause\n" );
}

/*
 * returns 0 if it didn't have to enter pause loop
 * otherwise returns number of pause loop iterations
 */
static inline
unsigned long long sys_delay(int min_clk)
{
    unsigned long long count = 0;
    unsigned long long past = rdtsc();

    while (1) {
	if ((rdtsc() - past) > min_clk) return count;
	count++;
	__pause();
    }
}

extern int is_rdtscp_available(void);
extern int is_tsc_invariant(void);
extern int __cpuid_obtain_model_string(char* buf);

/*
 * Ok. the only reason this is locally implemented is
 * to get away with an linker error on FC13 when libc is statically linked.
 * We don't care about the performance of strncmp.
 */
static inline 
int my_strncmp(const char* a, const char* b, size_t n)
{
    while (*a == *b) {
	if (--n == 0) return 0;
	if (*a == 0) return 0;
	a++;
	b++;
    }

    return (*a - *b);
}

/*
 * usage:
 * stopwatch sw;
 * sw_reset(&sw, &ops);
 * sw_start(&sw);
 * perform_bm1();
 * sw_stop(&sw);
 * do_something_you_dont_want_to_measure();
 * sw_start(&sw);
 * perform_bm2();
 * sw_stop(&sw);
 * printf("bm1+bm2 took total %u nsec\n", sw_get_usec(&sw));
 */

struct stopwatch;

struct sys_timestamp {
    unsigned long long (*timestamp)(void);
    int (*init_base_freq)(struct sys_timestamp* sts);
    unsigned base_freq_khz;
    const char* name;
};

struct stopwatch {
    unsigned long long start_tsc;
    unsigned long long elapsed_sum;
    const struct sys_timestamp* ops;
}; 

static inline 
void sw_reset(struct stopwatch* sw, const struct sys_timestamp* sts) {
    sw->start_tsc = sw->elapsed_sum = 0ull;
    sw->ops = sts;
}

static inline
unsigned long long sw_start(struct stopwatch* sw) {
    sys_barrier();
    sw->start_tsc = sw->ops->timestamp();
    sys_barrier();
    return sw->start_tsc;
}

static inline
unsigned long long sw_stop(struct stopwatch* sw) {
    unsigned long long now;
    sys_barrier();
    now = sw->ops->timestamp();
    sys_barrier();
    sw->elapsed_sum += (now - sw->start_tsc);
    return now;
}

static inline
unsigned long long sw_get_nsec(const struct stopwatch* sw) {
    return (unsigned long long)((sw->elapsed_sum * 1000 * 1000)/sw->ops->base_freq_khz);
}

static inline
unsigned sw_get_usec(const struct stopwatch* sw) {
    return (unsigned)((sw->elapsed_sum * 1000)/sw->ops->base_freq_khz); 
}

extern struct sys_timestamp rdtsc_ops;
extern struct sys_timestamp rdtscp_ops;
extern struct sys_timestamp perfc_ops;

extern struct sys_timestamp* get_timestamp_from_name(const char* str);

#define SYS_PMBENCH_VERSION_STRING "pmbench 0.4"

extern void sys_print_pmbench_info(void);
extern void sys_print_os_info(void);
extern void sys_print_time_info(void);
extern void sys_print_uuid(void);

extern void print_tlb_info(void) __attribute__((cold));
extern void print_cache_info(void) __attribute__((cold));

/* 
 * Linux memory usage statistics - /proc/meminfo
 * MemTotal : total pages in the system
 * MemFree  : pages in free list
 * Buffers  : pages allocated for disk buffer cache (inode, meta etc)
 * Cached   : pages allocated for disk page cache (content)
 *  Memory used = MemTotal - MemFree
 *  Free mem if disk cache is dropped = MemFree + Buffers + Cached
 * SwapCached : ???
 * Active   : Active 
 */

typedef struct sys_mem_ctx {
    int fd_meminfo;	// /proc/meminfo
    int fd_vmstat;	// /proc/vmstat
} sys_mem_ctx;

#ifdef _WIN32
typedef struct sys_mem_item {
    MEMORYSTATUSEX memstatex;
    int	recorded;
} sys_mem_item;
#else
/* currently 16 * 4 bytes = 64 bytes, meaning 64 entries per 4K page */
typedef struct sys_mem_item {
    int total_kib;	// meminfo->MemTotal
    int free_kib;	// meminfo->MemFree
    int buffer_kib;	// meminfo->Buffers
    int cache_kib;	// meminfo->Cached
    int active_kib;	// meminfo->Active
    int inactive_kib;	// meminfo->Inactive
    long long pgpgin;	// vmstat->pgpgin;
    long long pgpgout;	// vmstat->pgpgout;
    long long pswpin;	// vmstat->pswpin;
    long long pswpout;	// vmstat->pswpout;
    long long pgmajfault; // vmstat->pgmajfault;
    int recorded;
} sys_mem_item;
#endif

extern int sys_stat_mem_init(sys_mem_ctx* ctx);
extern int sys_stat_mem_update(sys_mem_ctx* ctx, sys_mem_item* info);
extern void sys_stat_mem_print_header(void) __attribute__((cold));
extern void sys_stat_mem_print(const sys_mem_item* info) __attribute__((cold));
extern void sys_stat_mem_print_delta(const sys_mem_item* before, const sys_mem_item* after) __attribute__((cold));
extern int sys_stat_mem_exit(sys_mem_ctx* ctx);

extern void test_stat_mem(void);



#endif
