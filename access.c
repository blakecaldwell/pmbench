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

#include <stdlib.h>
#include <stdio.h>
#include <string.h>
#include <math.h>

#include "rdtsc.h"
#include "system.h"
#include "access.h"

/* a few atomic operation definitions */
/*

#ifdef __x86_64__
static inline
void atomic_inc_64(uint64_t *pval) { //changed from unsigned
    asm volatile (
	"lock; incq %0" 
	: "=m"(*(volatile uint64_t *)pval)
	: "m"(*(volatile uint64_t *)pval)
    );
}

static inline
void atomic_dec_64(uint64_t *pval) {
    asm volatile (
	"lock; decq %0" 
	: "=m"(*(volatile uint64_t *)pval)
	: "m"(*(volatile uint64_t *)pval)
    );
}
#else

#error "find 32-bit atomic 64bit operation"

#endif //__x86_64__
*/

static
void finish_touch(char* buf, int b)
{
    return;
}

static
void touch_report(char* buf, int ratio)
{
    printf("touch_report: nothing to report\n");
    return;
}

/** long_latency counts log2-base latency >= 2^23 nsec
 * consumes 1024 bytes (= 16*16*4 = 1024) 
 * N.B. this version uses bucket_sub[0].hex[8-15] for storing long latencies 
 * bucket_sub[0].hex[ 8] := [2^23, 2^24)
 * bucket_sub[0].hex[ 9] := [2^24, 2^25)
 * bucket_sub[0].hex[10] := [2^25, 2^26)
 * ...
 * bucket_sub[0].hex[15] := [2^30, 2^32) <- 1-4 seconds
 * bucket_sub[0].hex[0] stores events < 2^8 (= 256) ns
 * (bucket_sub[0].hex[1-7] are unused)
 */
/*
 * Each log2 bucket is split into 16 bins (hex), where each bin 
 * counts the number of samples occurred in corresponding 1/16th range.
 * We simply use the next 4 bits to determine which hex to count.
 */

struct histogram_sub_64 { 
    uint64_t hex[16]; 
}  __attribute__((packed)); //64-bit histogram

struct histogram_64 { 
    struct histogram_sub_64 buckets[16]; 
} __attribute__((packed)); //64-bit read and write histogram, occupies a full page

extern const struct sys_timestamp* get_tsops(void);

static
_code 
uint32_t measure_read(uint32_t *ptr)
{
    register uint32_t _val_sink;
    struct stopwatch sw;
    sw_reset(&sw, get_tsops());
    sw_start(&sw);
    //asm following implements: _val_sink = *ptr;
    asm volatile (
	 "movl %1, %0\n\t"
	  : "=r" (_val_sink)
	  : "m" (*ptr)
	  : "memory");

    sw_stop(&sw);
    return sw_get_nsec(&sw); //_val_sink;
}

static
_code 
uint32_t measure_write(uint32_t *ptr)
{
    uint32_t val_to_write;
    struct stopwatch sw;

    val_to_write = (uint32_t)(uintptr_t)(ptr); // let's write the ptr value

    sw_reset(&sw, get_tsops());
    sw_start(&sw);
    // asm following implements: *ptr = val_to_write;
    asm volatile (
	 "movl %1, %0 \n\t"
	  : "=m" (*ptr)
	  : "r" (val_to_write)
	  : "memory");

    sw_stop(&sw);
    return sw_get_nsec(&sw);
}

/*
 * this performs write after explicit read
 */
static
_code 
uint32_t measure_write_after_read(uint32_t *ptr)
{
    register uint32_t val = 0;
    struct stopwatch sw;
    sw_reset(&sw, get_tsops());
    sw_start(&sw);
    // asm following implements: *ptr = (*ptr);
    asm volatile (
	 "movl (%1), %0\n\t"
	 "movl %0, (%1)\n\t"
	  : "=r"(val), "=r"(ptr)
	  : "1"(ptr)
	  : "memory"
	  );

    sw_stop(&sw);

    return sw_get_nsec(&sw);
}

_code 
uint32_t access_histogram(uint32_t *ptr, int is_write)
{
    uint32_t latency;

    switch (is_write) {
    case 0:
	latency = measure_read(ptr);
	break;
    case 1:
	latency = measure_write(ptr);
	break;
    case 2:
	latency = measure_write_after_read(ptr);
	break;
    default:
	latency = measure_read(ptr);
    }
    return latency;
}


_code
void record_touch_dummy(char *a, uint32_t b, int c)
{
    return;
}

_code
void record_histogram(char *stats, uint32_t elapsed_nsec, int is_write)
{
    struct histogram_64* histo = (struct histogram_64*)(stats); //this should be pointing to the thread's read/write histogram page
    uint64_t *pcounter;

    is_write = (is_write ? 1 : 0);

    if (elapsed_nsec < (1 << 8)) {
	pcounter = &histo[is_write].buckets[0].hex[0];
    } else {
	uint32_t index = ilog2(elapsed_nsec) - 7;
	if (index < 16) {
	    int sub_index = (~(1u << (index + 7)) & elapsed_nsec) >> (index + 7 - 4);
	    pcounter = &histo[is_write].buckets[index].hex[sub_index];
	} else if (index >= 16+7) {
	    pcounter = &histo[is_write].buckets[0].hex[8 + 7];
	} else { 
	    pcounter = &histo[is_write].buckets[0].hex[8 + index - 16];
	}
    }
    //atomic_inc_64(pcounter);
    (*pcounter)++;
}

/*
 * this finish function should be called by main thread after all workers join
 */
static
void finish_histogram(char *stats, int num_threads)
{
    struct histogram_64 *result_array = (struct histogram_64*)stats;
    struct histogram_64 *result_r = &result_array[0];
    struct histogram_64 *result_w = &result_array[1];

    int bucket, hex;
    int i;

    for (i = 1; i < num_threads; ++i) {
	struct histogram_64 *histo_r = &result_array[2*i];
	struct histogram_64 *histo_w = &result_array[2*i + 1];

	for (bucket = 0; bucket <= 0x0f; bucket++) {
	    for (hex = 0; hex <= 0x0f; hex++) { 
		result_r->buckets[bucket].hex[hex] += histo_r->buckets[bucket].hex[hex];

		result_w->buckets[bucket].hex[hex] += histo_w->buckets[bucket].hex[hex]; 
	    }
	}
    }
}

uint64_t* get_histogram_bucket(char *buf, int is_write, int bucketnum)
{
    is_write = (is_write ? 1 : 0);
    struct histogram_64 *bin = (struct histogram_64 *)(buf);
    return bin[is_write].buckets[bucketnum].hex;
}

void __attribute__((cold)) dump_histogram_64(const struct histogram_64 *bin)
{
    int i, j;
    uint64_t sum_count;
    uint64_t sum_all = bin->buckets[0].hex[0];
    printf("2^(00,08) ns: %"PRIu64"\n", bin->buckets[0].hex[0]);
    for (i = 1; i < 16; ++i) {
    	sum_count = 0;
    	for (j = 0; j < 16; ++j) { sum_count += bin->buckets[i].hex[j]; } 		
    	sum_all += sum_count;
    	printf("2^(%02d,%02d) ns: %"PRIu64"  ", i + 7, i + 8, sum_count);
    	printf("[%"PRIu64", %"PRIu64", %"PRIu64", %"PRIu64", %"PRIu64", %"PRIu64", %"PRIu64", %"PRIu64", " \
    		"%"PRIu64", %"PRIu64", %"PRIu64", %"PRIu64", %"PRIu64", %"PRIu64", %"PRIu64", %"PRIu64"]\n",
			bin->buckets[i].hex[0],
			bin->buckets[i].hex[1],
			bin->buckets[i].hex[2],
			bin->buckets[i].hex[3],
			bin->buckets[i].hex[4],
			bin->buckets[i].hex[5],
			bin->buckets[i].hex[6],
			bin->buckets[i].hex[7],
			bin->buckets[i].hex[8],
			bin->buckets[i].hex[9],
			bin->buckets[i].hex[10],
			bin->buckets[i].hex[11],
			bin->buckets[i].hex[12],
			bin->buckets[i].hex[13],
			bin->buckets[i].hex[14],
			bin->buckets[i].hex[15] );
   }
   for (i = 0; i < 7; ++i) {
       printf("2^(%d,%d) ns: %"PRIu64"\n", i + 23, i + 24, bin->buckets[0].hex[8 + i]);
       sum_all += bin->buckets[0].hex[8 + i];
   }
   printf("2^(30,32) ns: %"PRIu64"\n", bin->buckets[0].hex[8 + 7]);
   sum_all += bin->buckets[0].hex[8 + 7];
   printf("Total samples: %"PRIu64"\n\n", sum_all);
}

static void histogram_report(char* buf, int ratio)
{
    struct histogram_64 *result = (struct histogram_64*)(buf);

    printf("# Access latency histogram\n");
    if (ratio > 0) {
	printf("Read:\n"); 
	dump_histogram_64(&result[0]); 
    }
    if (ratio < 100) {
	printf("Write:\n");
	dump_histogram_64(&result[1]);
    }
    return;
}

access_fn_set touch_access = {
    //.warmup = NULL, //touch_only,
    .exercise = access_histogram,
    .record = record_touch_dummy,
    .finish = finish_touch,
    .report = touch_report,
    .name = "touch",
    .description = "Simple touching"
};

access_fn_set histogram_access = {
    //.warmup = NULL, //touch_only,
    .exercise = access_histogram,
    .record = record_histogram,
    .finish = finish_histogram,
    .report = histogram_report,
    .name = "histo",
    .description = "Touch and keep latency histogram"
};

/* 
 * all access_fns
 */
static access_fn_set* all_access_fn[] = { 
    &touch_access, &histogram_access, 0
};

access_fn_set* get_access_from_name(const char* str)
{
    int i = 0;
    if (!str) return 0;
    while (all_access_fn[i]) {
    	if (!my_strncmp(all_access_fn[i]->name, str, 16))
	    return all_access_fn[i];
    	i++;
    }
    return 0;
}
