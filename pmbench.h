#ifndef __PMBENCH_H__
#define __PMBENCH_H__

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


#include <inttypes.h>
#include "access.h"
#include "pattern.h"

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

#define PMBENCH_VERSION_STR "pmbench 0.8"

/* 
 * No lock needed for threads access to params.
 * (once set at program start, parameters are only read until program exit)
 */ 
typedef struct parameters {
    int duration_sec;	//benchmark duration in seconds (excluding warmup)
    int mapsize_mib;	// anonymous mmap size in megabytes
    int setsize_mib;	// 'working set' size in megabytes (depends on pattern)
    access_fn_set* access;  	// access method (touch or histo)
    pattern_generator* pattern;		//benchmark pattern
    uint32_t (*get_offset) (uint64_t *state);	// gets random or static offset
    uint32_t (*get_accesstype) (uint64_t *action); 	// future improvements: read/write selection pattern (used by -r) selectable like pattern/shape?
    double shape;   	// 'shape' parameter to use for pattern
    int delay;	    	// minimum clock cycles between accesses
    int quiet;	    	// no output until done
    int cold;	    	// don't perform warm up exercise before benchmark
    struct sys_timestamp* tsops;// timestamp ops (rdtsc_ops or perfc_ops)
    int jobs;		// number of worker threads
    int init_garbage;
#ifdef XALLOC
    int xalloc_mib;	// positive xalloc_mib indicates we use xalloc instead of mmap
    char* xalloc_path;	// xalloc backend file pathname
#endif
    int offset;		// page offset, negative = random
    int ratio;		// % likelihood of read access
#ifdef PMB_XML
    uint8_t xml;
    char *xml_path;
#endif
} parameters;

extern parameters params;

extern uint32_t freq_khz;

/*benchmark result processing */
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

extern struct bench_result* get_result(int jobid);

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
#endif
