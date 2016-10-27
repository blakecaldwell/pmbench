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
static inline
void atomic_inc(uint64_t *pval) { //changed from unsigned
    asm volatile (
	"lock; incl %0" 
	: "=m"(*(volatile unsigned*)pval)
	: "m"(*(volatile unsigned*)pval)
    );
}

static inline
void atomic_dec(uint64_t *pval) {
    asm volatile (
	"lock; decl %0" 
	: "=m"(*(volatile unsigned*)pval)
	: "m"(*(volatile unsigned*)pval)
    );
}

static int finish_touch(char* buf, size_t index, int ratio) { return 1; }

static void touch_report(char* buf, int ratio)
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

struct histogram_sub_64 { uint64_t hex[16]; }  __attribute__((packed)); //64-bit histogram
struct histogram_64 { struct histogram_sub_64 buckets[32]; } __attribute__((packed)); //64-bit read and write histogram, occupies a full page

extern const struct sys_timestamp* get_tsops(void);

_code int read_access_histogram(char* buf, size_t pfn, unsigned int offset)
{
	static volatile int _val_sink = 0;
	//volatile register int _val_sink = 0;
	int *ptr = (int*)(buf + (pfn << PAGE_SHIFT) + (offset >> 22) * 4); //access address = ( base address of map + (page number << 12) + (10 bit random number) * sizeof(u32) )
	//printf("\tPage:\t%d\t\tOffset:\t%04x\n", (int) pfn, offset >> 22);
    struct stopwatch swr;
    sw_reset(&swr, get_tsops());
    sw_start(&swr);
    asm volatile (		//"nop\n\t"
    							//"mfence \n\t"
    							"mov %1, %0" //\n\t"
    							//"nop"
    						: 	"=r" (_val_sink)
							: 	"m" (*ptr) );
							//:	"memory");    	//_val_sink = *ptr;
    sw_stop(&swr);
    return sw_get_nsec(&swr); //_val_sink;
}

_code int write_access_histogram(char* buf, size_t pfn, unsigned int offset)
{
	volatile register int _val_sink = 0;
	int *ptr = (int*)(buf + (pfn << PAGE_SHIFT) + (offset >> 22) * 4);
    struct stopwatch sww;
    sw_reset(&sww, get_tsops());
    sw_start(&sww);
    asm volatile ( 		//"nop\n\t"
    							//"mfence \n\t"
    							"mov %1, %0" //\n\t"
								//"nop"
           				: 	"=m" (*ptr)
       					: 	"r" (_val_sink) );
							//:	"memory");    	//*ptr = _val_sink;
    sw_stop(&sww);
    return sw_get_nsec(&sww);
}

_code int record_touch_dummy(char *a, unsigned int b) { return 1; }

_code int record_histogram(char *stats, unsigned int elapsed_nsec)
{
	/*histo = (struct histogram_log2_sub*)(buf + (pfn << PAGE_SHIFT));
    	elapsed_nsec = sw_get_nsec(&sw);
    	if (elapsed_nsec < (1 << 8)) 
	{
		pcounter = &histo->bucket_sub[0].hex[0];
    	} 
	else 
	{
		index = ilog2(elapsed_nsec) - 7;
		if (index < 16) 
		{
	    		int sub_index = (~(1u << (index + 7)) & elapsed_nsec) >> (index + 7 - 4);
	    		pcounter = &histo->bucket_sub[index].hex[sub_index];
		} 
		else if (index >= 16+7) 
		{
	    	pcounter = &histo->bucket_sub[0].hex[8 + 7];
		} 
		else 
		{
		    pcounter = &histo->bucket_sub[0].hex[8 + index - 16];
		}
    	}
    	atomic_inc(pcounter);*/
	struct histogram_sub_64* histo = (struct histogram_sub_64*)(stats); //this should be pointing to the thread's read/write histogram page
	uint64_t *pcounter; //changed from u32
	if (elapsed_nsec < (1 << 8)) { pcounter = &(histo[0].hex[0]); }
	else
	{
		int index = ilog2(elapsed_nsec) - 7;
		if (index < 16)
		{
			int sub_index = (~(1u << (index + 7)) & elapsed_nsec) >> (index + 7 - 4);
			pcounter = &(histo[index].hex[sub_index]);
		}
		else if (index >= 16+7) { pcounter = &(histo[0].hex[8 + 7]); }
		else { pcounter = &(histo[0].hex[8 + index - 16]); }
	}
	atomic_inc(pcounter);
	return 1;
}

static int finish_histogram(char *stats, size_t index, int ratio)
{
	struct histogram_64 *result = (struct histogram_64*) stats;
	struct histogram_64 *histo = (struct histogram_64*) (stats + (index - 1) * 4096);
	int bucket, hex;
	if (ratio > 0)
	{
		for (bucket = 0; bucket <= 0x0f; bucket++)
		{
			for (hex = 0; hex <= 0x0f; hex++) { result->buckets[bucket].hex[hex] += histo->buckets[bucket].hex[hex]; }
		}
	}
	if (ratio < 100)
	{
		for (bucket = 0; bucket <= 0x0f; bucket++)
		{
			for (hex = 0; hex <= 0x0f; hex++) { result->buckets[bucket+16].hex[hex] += histo->buckets[bucket+16].hex[hex]; }
		}
	}
	return 1;
}

uint64_t * get_histogram_bucket(char *buf, int writehisto, int bucketnum)
{
	struct histogram_64 *bin = (struct histogram_64 *)(buf);
	return bin->buckets[bucketnum + (writehisto*16)].hex;
}

void __attribute__((cold)) dump_histogram_64(const struct histogram_64 *bin, int writehisto) //pass this a 1 to print the write histogram, 0 for read
{
	writehisto *= 16;
    int i, j;
    uint64_t sum_count;
    printf("2^(00,08) ns: %"PRIu64"\n", bin->buckets[writehisto].hex[0]);
    for (i = 1; i < 16; ++i)
    {
    	sum_count = 0;
    	for (j = 0; j < 16; ++j) { sum_count += bin->buckets[i + writehisto].hex[j]; } 		
    	printf("2^(%02d,%02d) ns: %"PRIu64"  ", i + 7, i + 8, sum_count);
    	printf("[%"PRIu64", %"PRIu64", %"PRIu64", %"PRIu64", %"PRIu64", %"PRIu64", %"PRIu64", %"PRIu64", " \
    		"%"PRIu64", %"PRIu64", %"PRIu64", %"PRIu64", %"PRIu64", %"PRIu64", %"PRIu64", %"PRIu64"]\n",
			bin->buckets[i + writehisto].hex[0],
			bin->buckets[i + writehisto].hex[1],
			bin->buckets[i + writehisto].hex[2],
			bin->buckets[i + writehisto].hex[3],
			bin->buckets[i + writehisto].hex[4],
			bin->buckets[i + writehisto].hex[5],
			bin->buckets[i + writehisto].hex[6],
			bin->buckets[i + writehisto].hex[7],
			bin->buckets[i + writehisto].hex[8],
			bin->buckets[i + writehisto].hex[9],
			bin->buckets[i + writehisto].hex[10],
			bin->buckets[i + writehisto].hex[11],
			bin->buckets[i + writehisto].hex[12],
			bin->buckets[i + writehisto].hex[13],
			bin->buckets[i + writehisto].hex[14],
			bin->buckets[i + writehisto].hex[15] );
   }
   for (i = 0; i < 7; ++i) { printf("2^(%d,%d) ns: %"PRIu64"\n", i + 23, i + 24, bin->buckets[writehisto].hex[8 + i]); }
   printf("2^(30,32) ns: %"PRIu64"\n\n", bin->buckets[writehisto].hex[8 + 7]);
}

static void histogram_report(char* buf, int ratio)
{
    struct histogram_64 *result = (struct histogram_64*)(buf);
    printf("# Access latency histogram\n");
    if (ratio > 0) { printf("Read:\n"); dump_histogram_64(result, 0); }
    if (ratio < 100) { printf("Write:\n"); dump_histogram_64(result, 1); }
    return;
}

access_fn_set touch_access = {
    .warmup = NULL, //touch_only,
    .exercise_read = read_access_histogram, 	//should this be an edited version without the stopwatch?
	.exercise_write = write_access_histogram,
	.record = record_touch_dummy,
    .finish = finish_touch,
    .report = touch_report,
    .name = "touch",
    .description = "Simple touching"
};

access_fn_set histogram_access = {
    .warmup = NULL, //touch_only,
    .exercise_read = read_access_histogram,
	.exercise_write = write_access_histogram,
	.record = record_histogram,
    .finish = finish_histogram,
    .report = histogram_report,
    .name = "histo",
    .description = "Touch and keep latency histogram"
};

/*b* all access_fns */
static access_fn_set* all_access_fn[] = { &touch_access, &histogram_access, 0 };

access_fn_set* get_access_from_name(const char* str)
{
    int i = 0;
    if (!str) return 0;
    while (all_access_fn[i])
    {
    	if (!my_strncmp(all_access_fn[i]->name, str, 16)) return all_access_fn[i];
    	i++;
    }
    return 0;
}
