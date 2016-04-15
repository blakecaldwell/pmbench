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

#include <inttypes.h>

#include "rdtsc.h"
#include "system.h"
#include "access.h"

/* a few atomic operation definitions */
static inline
void atomic_inc(unsigned* pval) {
    asm volatile (
	"lock; incl %0" 
	: "=m"(*(volatile unsigned*)pval)
	: "m"(*(volatile unsigned*)pval)
    );
}

static inline
void atomic_dec(unsigned* pval) {
    asm volatile (
	"lock; decl %0" 
	: "=m"(*(volatile unsigned*)pval)
	: "m"(*(volatile unsigned*)pval)
    );
}

/** 
 * NB. Using function pointer to 1) choose method easily and 2) avoid
 * possible compiler optimization around the function call.
 */

/*
 * Leave first 128 bytes intact so that other complicated access function
 * can use the space.
 */

int
_code
touch_only(char* buf, size_t pfn)
{
    static int _val_sink = 0;
    
    // memory read AND write. 128 is an offset chosen randomly
    int* ptr = (int*)(buf + (pfn << PAGE_SHIFT) + 128);
    _val_sink += *ptr;
    *ptr = _val_sink;
    return _val_sink;
}

static
int finish_touch(char* buf, size_t num_pages)
{
    return 1;
}

static
void touch_report(char* buf)
{
    printf("touch_report: nothing to report\n");
    return;
}

access_fn_set touch_access = {
    .warmup = touch_only,
    .exercise = touch_only,
    .finish = finish_touch,
    .report = touch_report,
    .name = "touch",
    .description = "Simple read touching"
};

/*
 * long_latency counts log2-base latency >= 2^23 nsec
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
struct histogram_sub {
    uint32_t hex[16];
} __attribute__((packed));


struct histogram_log2_sub {
    union {
	struct histogram_sub bucket_sub[16];
	uint64_t hit_counts_sum;
    };
} __attribute__((packed));

extern const struct sys_timestamp* get_tsops(void);

static
_code
int access_histogram(char* buf, size_t pfn)
{
    struct stopwatch sw;
    static int _val_sink = 0;
    struct histogram_log2_sub* histo;
    int* ptr;
    int index;
    uint32_t elapsed_nsec;
    uint32_t* pcounter;

    sw_reset(&sw, get_tsops());
    sw_start(&sw);
    ptr = (int*)(buf + (pfn << PAGE_SHIFT) + (2048 - 64));
    _val_sink += *ptr;
    *ptr = _val_sink;
    sw_stop(&sw);

    histo = (struct histogram_log2_sub*)(buf + (pfn << PAGE_SHIFT));
    elapsed_nsec = sw_get_nsec(&sw);
    if (elapsed_nsec < (1 << 8)) {
	pcounter = &histo->bucket_sub[0].hex[0];
    } else {
	index = ilog2(elapsed_nsec) - 7;
	if (index < 16) {
	    int sub_index = (~(1u << (index + 7)) & elapsed_nsec) >> (index + 7 - 4);
	    pcounter = &histo->bucket_sub[index].hex[sub_index];
	} else if (index >= 16+7) {
	    pcounter = &histo->bucket_sub[0].hex[8 + 7];
	} else {
	    pcounter = &histo->bucket_sub[0].hex[8 + index - 16];
	}
    }
    atomic_inc(pcounter);
    return _val_sink;
}

/*
 * we use buf[2048-4095] for storing result.
 */
static
int finish_histogram(char* buf, size_t num_pages)
{
    struct histogram_log2_sub* histo;
    struct histogram_log2_sub* result = (struct histogram_log2_sub*)(buf + 2048);
    size_t i;	// In Win64 long is 32bit and overflows when > 2GiB.
    int j, k;
    
    for (i = 0; i < num_pages; ++i) {
	histo = (struct histogram_log2_sub*)(buf + (i << PAGE_SHIFT));

	/* special case for [0][0] - count 64bit to prevent overflow */
	result->hit_counts_sum += histo->bucket_sub[0].hex[0];
	for (k = 8; k < 16; ++k) {
	    result->bucket_sub[0].hex[k] += histo->bucket_sub[0].hex[k];
	}

	for (j = 1; j < 16; ++j) {
	    for (k = 0; k < 16; ++k) {
		result->bucket_sub[j].hex[k] += histo->bucket_sub[j].hex[k];
	    }
	}
    }
    
    return 1;
}

void
__attribute__((cold))
dump_histogram_log2_new(const struct histogram_log2_sub* bin)
{
    int i, j;
    uint64_t sum_count;
    printf("2^(00,08) ns: %"PRIu64"\n", bin->hit_counts_sum);
    for (i = 1; i < 16; ++i) {
	sum_count = 0;
	for (j = 0; j < 16; ++j) {
	    sum_count += bin->bucket_sub[i].hex[j];
	}
	printf("2^(%02d,%02d) ns: %"PRIu64"  ", i + 7, i + 8, sum_count);
	printf("[%d, %d, %d, %d, %d, %d, %d, %d, " \
		"%d, %d, %d, %d, %d, %d, %d, %d]\n", 
		bin->bucket_sub[i].hex[0], 
		bin->bucket_sub[i].hex[1], 
		bin->bucket_sub[i].hex[2], 
		bin->bucket_sub[i].hex[3],
		bin->bucket_sub[i].hex[4], 
		bin->bucket_sub[i].hex[5], 
		bin->bucket_sub[i].hex[6], 
		bin->bucket_sub[i].hex[7],
		bin->bucket_sub[i].hex[8], 
		bin->bucket_sub[i].hex[9], 
		bin->bucket_sub[i].hex[10], 
		bin->bucket_sub[i].hex[11],
		bin->bucket_sub[i].hex[12], 
		bin->bucket_sub[i].hex[13], 
		bin->bucket_sub[i].hex[14], 
		bin->bucket_sub[i].hex[15] );
    }

    for (i = 0; i < 7; ++i) {
	printf("2^(%d,%d) ns: %d\n", i + 23, i + 24, bin->bucket_sub[0].hex[8 + i]);
    }
    printf("2^(30,32) ns: %d\n", bin->bucket_sub[0].hex[8 + 7]);
}

static
void histogram_report(char* buf)
{
    struct histogram_log2_sub* result = (struct histogram_log2_sub*)(buf + 2048);
    printf("# Access latency histogram\n");
    dump_histogram_log2_new(result);
    return;
}

access_fn_set histogram_access = {
    .warmup = touch_only,
    .exercise = access_histogram,
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
