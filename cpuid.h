#ifndef __CPUID_H__
#define __CPUID_H__
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

/* generic cpuid */
static inline
void _sys_cpuid(unsigned* a, unsigned* b, unsigned* c, unsigned* d)
{
    asm volatile ("cpuid\n" 
	: "=a"(*a), "=b"(*b), "=c"(*c), "=d"(*d)
	: "0"(*a), "2"(*c));
}

/* CPUID 02 leaflets as of Intel SDM Version 047 (December 2015) */
#define __CPUID02_0x01_STR "ITLB: 32 entries for 4KB, 4-way\n"
#define __CPUID02_0x02_STR "ITLB: 2 entries for 4MB, full\n"
#define __CPUID02_0x03_STR "DTLB: 64 entries for 4KB, 4-way\n"
#define __CPUID02_0x04_STR "DTLB: 8 entries for 4MB, 4-way\n"
#define __CPUID02_0x05_STR "DTLB: 32 entries for 4MB, 4-way\n"
#define __CPUID02_0x0B_STR "ITLB: 4 entries for 4MB, 4-way\n"
#define __CPUID02_0x4F_STR "ITLB: 32 entries for 4KB\n"
#define __CPUID02_0x50_STR "ITLB: 64 entries for 4KB and 2MB or 4MB\n"
#define __CPUID02_0x51_STR "ITLB: 128 entries for 4KB and 2MB or 4MB\n"
#define __CPUID02_0x52_STR "ITLB: 256 entries for 4KB and 2MB or 4MB\n"
#define __CPUID02_0x55_STR "ITLB: 7 entries for 2MB or 4MB, full\n"
#define __CPUID02_0x56_STR "DTLB0: 16 entries for 4MB, 4-way\n"
#define __CPUID02_0x57_STR "DTLB0: 16 entries for 4KB, 4-way\n"
#define __CPUID02_0x59_STR "DTLB0: 16 entries for 4MB, full\n"
#define __CPUID02_0x5A_STR "DTLB0: 32 entries for 2MB or 4MB, 4-way\n"
#define __CPUID02_0x5B_STR "DTLB: 64 entries for 4KB and 4MB\n"
#define __CPUID02_0x5C_STR "DTLB: 128 entries for 4KB and 4MB\n"
#define __CPUID02_0x5D_STR "DTLB: 256 entries for 4KB and 4MB\n"
#define __CPUID02_0x61_STR "ITLB: 48 entries for 4KB, full \n"
#define __CPUID02_0x63_STR "DTLB: 4 entries for 1GB, 4-way\n"
#define __CPUID02_0x6A_STR "ITLB: 64 entries for 4KB, 8-way\n"
#define __CPUID02_0x6B_STR "DTLB: 256 entries for 4KB, 8-way\n"
#define __CPUID02_0x6C_STR "DTLB: 128 entries for 2MB or 4MB, 8-way\n"
#define __CPUID02_0x6D_STR "DTLB: 16 entries for 1GB, full\n"
#define __CPUID02_0x76_STR "ITLB: 8 entries for 2MB or 4MB, full\n"
#define __CPUID02_0xA0_STR "DTLB: 32 entries for 4KB, full\n"
#define __CPUID02_0xB0_STR "ITLB: 128 entries for 4KB, 4-way\n"
#define __CPUID02_0xB1_STR "ITLB: 8 entries for 2MB, 4-way or 4 entries for 4MB, 4-way\n"
#define __CPUID02_0xB2_STR "ITLB: 64 entries for 4KB, 4-way\n"
#define __CPUID02_0xB3_STR "DTLB: 128 entries for 4KB, 4-way\n"
#define __CPUID02_0xB4_STR "DTLB1: 256 entries for 4KB, 4-way\n"
#define __CPUID02_0xB5_STR "ITLB: 64 entries for 4KB, 8-way\n"
#define __CPUID02_0xB6_STR "ITLB: 128 entries for 4KB, 8-way\n"
#define __CPUID02_0xBA_STR "DTLB1: 64 entries for 4KB, 4-way\n"
#define __CPUID02_0xC0_STR "DTLB: 8 entries for 4KB and 4MB, 4-way\n"
#define __CPUID02_0xC1_STR "STLB: 1024 entries for 4KB and 2MB, 8-way, shared 2nd level\n"
#define __CPUID02_0xC2_STR "DTLB: 16 entries for 4KB and 2MB, 4-way\n"
#define __CPUID02_0xC3_STR "STLB: 1536 entries for 4KB and 2MB, 6-way, shared 2nd level. Also 16 entries for 1GB, 4-way\n"
#define __CPUID02_0xCA_STR "STLB: 512 entries for 4KB, 4-way, shared 2nd level\n"


#define __CPUID02_0x06_STR "ICACHE: 8KB 1st, 32B line, 4-way\n"
#define __CPUID02_0x08_STR "ICACHE: 16KB 1st, 32B line, 4-way\n"
#define __CPUID02_0x09_STR "ICACHE: 32KB 1st, 64B line, 4-way\n"
#define __CPUID02_0x0A_STR "DCACHE: 8KB 1st, 32B line, 2-way\n"
#define __CPUID02_0x0C_STR "DCACHE: 16KB 1st, 32B line, 4-way\n"
#define __CPUID02_0x0D_STR "DCACHE: 16KB 1st, 64B line, 4-way\n"
#define __CPUID02_0x0E_STR "DCACHE: 24KB 1st, 64B line, 6-way\n"
#define __CPUID02_0x1D_STR "CACHE: 128KB 2nd, 64B line, 2-way\n"
#define __CPUID02_0x21_STR "CACHE: 256KB 2nd, 64B line, 8-way\n"
#define __CPUID02_0x22_STR "CACHE: 512KB 3rd, 64B line, 4-way, 2-lines per sector\n"
#define __CPUID02_0x23_STR "CACHE: 1MB 3rd, 64B line, 8-way, 2-lines per sector\n"
#define __CPUID02_0x24_STR "CACHE: 1MB 2nd, 64B line, 16-way\n"
#define __CPUID02_0x25_STR "CACHE: 2MB 3rd, 64B line, 8-way, 2-lines per sector\n"
#define __CPUID02_0x29_STR "CACHE: 4MB 3rd, 64B line, 8-way, 2-lines per sector\n"
#define __CPUID02_0x2C_STR "DCACHE: 32KB 1st, 64B line, 8-way\n"
#define __CPUID02_0x30_STR "ICACHE: 32KB 1st, 64B line, 8-way\n"
#define __CPUID02_0x40_STR "CACHE: No 2nd or 3rd level cache\n"
#define __CPUID02_0x41_STR "CACHE: 128KB 2nd, 32B line, 4-way\n"
#define __CPUID02_0x42_STR "CACHE: 256KB 2nd, 32B line, 4-way\n"
#define __CPUID02_0x43_STR "CACHE: 512KB 2nd, 32B line, 4-way\n"
#define __CPUID02_0x44_STR "CACHE: 1MB 2nd, 32B line, 4-way\n"
#define __CPUID02_0x45_STR "CACHE: 2MB 2nd, 32B line, 4-way\n"
#define __CPUID02_0x46_STR "CACHE: 4MB 3rd, 64B line, 4-way\n"
#define __CPUID02_0x47_STR "CACHE: 8MB 3rd, 64B line, 8-way\n"
#define __CPUID02_0x48_STR "CACHE: 3MB 2nd, 64B line, 12-way\n"
#define __CPUID02_0x49_STR "CACHE: 4MB 2nd or 3rd, 64B line, 16-way\n"
#define __CPUID02_0x4A_STR "CACHE: 6MB 3rd, 64B line, 12-way\n"
#define __CPUID02_0x4B_STR "CACHE: 8MB 3rd, 64B line, 16-way\n"
#define __CPUID02_0x4C_STR "CACHE: 12MB 3rd, 64B line, 12-way\n"
#define __CPUID02_0x4D_STR "CACHE: 16MB 3rd, 64B line, 16-way\n"
#define __CPUID02_0x4E_STR "CACHE: 6MB 2nd, 64B line, 24-way\n"
#define __CPUID02_0x60_STR "DCACHE: 16KB 1st, 64B line, 8-way\n"
#define __CPUID02_0x66_STR "DCACHE: 8KB 1st, 64B line, 4-way\n"
#define __CPUID02_0x67_STR "DCACHE: 16KB 1st, 64B line, 4-way\n"
#define __CPUID02_0x68_STR "DCACHE: 32KB 1st, 64B line, 4-way\n"
#define __CPUID02_0x70_STR "TCACHE: 12K-uop, 8-way\n"
#define __CPUID02_0x71_STR "TCACHE: 16K-uop, 8-way\n"
#define __CPUID02_0x72_STR "TCACHE: 32K-uop, 8-way\n"
#define __CPUID02_0x78_STR "CACHE: 1MB 2nd, 64B line, 4-way\n"
#define __CPUID02_0x79_STR "CACHE: 128KB 2nd, 64B line, 8-way, 2-lines\n"
#define __CPUID02_0x7A_STR "CACHE: 256KB 2nd, 64B line, 8-way, 2-lines\n"
#define __CPUID02_0x7B_STR "CACHE: 512KB 2nd, 64B line, 8-way, 2-lines\n"
#define __CPUID02_0x7C_STR "CACHE: 1MB 2nd, 64B line, 8-way, 2-lines\n"
#define __CPUID02_0x7D_STR "CACHE: 2MB 2nd, 64B line, 8-way\n"
#define __CPUID02_0x7F_STR "CACHE: 512KB 2nd, 64B line, 2-way\n"
#define __CPUID02_0x80_STR "CACHE: 512KB 2nd, 64B line, 8-way\n"
#define __CPUID02_0x82_STR "CACHE: 256KB 2nd, 32B line, 8-way\n"
#define __CPUID02_0x83_STR "CACHE: 512KB 2nd, 32B line, 8-way\n"
#define __CPUID02_0x84_STR "CACHE: 1MB 2nd, 32B line, 8-way\n"
#define __CPUID02_0x85_STR "CACHE: 2MB 2nd, 32B line, 8-way\n"
#define __CPUID02_0x86_STR "CACHE: 512KB 2nd, 64B line, 4-way\n"
#define __CPUID02_0x87_STR "CACHE: 1MB 2nd, 64B line, 8-way\n"

#define __CPUID02_0xD0_STR "CACHE: 512KB 3rd, 64B line, 4-way\n"
#define __CPUID02_0xD1_STR "CACHE: 1MB 3rd, 64B line, 4-way\n"
#define __CPUID02_0xD2_STR "CACHE: 2MB 3rd, 64B line, 4-way\n"
#define __CPUID02_0xD6_STR "CACHE: 1MB 3rd, 64B line, 8-way\n"
#define __CPUID02_0xD7_STR "CACHE: 2MB 3rd, 64B line, 8-way\n"
#define __CPUID02_0xD8_STR "CACHE: 4MB 3rd, 64B line, 8-way\n"
#define __CPUID02_0xDC_STR "CACHE: 1.5MB 3rd, 64B line, 12-way\n"
#define __CPUID02_0xDD_STR "CACHE: 3MB 3rd, 64B line, 12-way\n"
#define __CPUID02_0xDE_STR "CACHE: 6MB 3rd, 64B line, 12-way\n"


#define __CPUID02_0xE2_STR "CACHE: 2MB 3rd, 64B line, 16-way\n"
#define __CPUID02_0xE3_STR "CACHE: 4MB 3rd, 64B line, 16-way\n"
#define __CPUID02_0xE4_STR "CACHE: 8MB 3rd, 64B line, 16-way\n"
#define __CPUID02_0xEA_STR "CACHE: 12MB 3rd, 64B line, 24-way\n"
#define __CPUID02_0xEB_STR "CACHE: 18MB 3rd, 64B line, 24-way\n"
#define __CPUID02_0xEC_STR "CACHE: 24MB 3rd, 64B line, 24-way\n"

#define __CPUID02_0xF0_STR "PREFETCH: 64B prefetching\n"
#define __CPUID02_0xF1_STR "PREFETCH: 128B prefetching\n"



#define ___CPUID02_PRN(num) \
case num: printf(__CPUID02_##num##_STR); break; 

#define CPUID02_TLB_PRN() \
___CPUID02_PRN(0x01) \
___CPUID02_PRN(0x02) \
___CPUID02_PRN(0x03) \
___CPUID02_PRN(0x04) \
___CPUID02_PRN(0x05) \
___CPUID02_PRN(0x0B) \
___CPUID02_PRN(0x4F) \
___CPUID02_PRN(0x50) \
___CPUID02_PRN(0x51) \
___CPUID02_PRN(0x52) \
___CPUID02_PRN(0x55) \
___CPUID02_PRN(0x56) \
___CPUID02_PRN(0x57) \
___CPUID02_PRN(0x59) \
___CPUID02_PRN(0x5A) \
___CPUID02_PRN(0x5B) \
___CPUID02_PRN(0x5C) \
___CPUID02_PRN(0x5D) \
___CPUID02_PRN(0x61) \
___CPUID02_PRN(0x63) \
___CPUID02_PRN(0x6A) \
___CPUID02_PRN(0x6B) \
___CPUID02_PRN(0x6C) \
___CPUID02_PRN(0x6D) \
___CPUID02_PRN(0x76) \
___CPUID02_PRN(0xA0) \
___CPUID02_PRN(0xB0) \
___CPUID02_PRN(0xB1) \
___CPUID02_PRN(0xB2) \
___CPUID02_PRN(0xB3) \
___CPUID02_PRN(0xB4) \
___CPUID02_PRN(0xB5) \
___CPUID02_PRN(0xB6) \
___CPUID02_PRN(0xBA) \
___CPUID02_PRN(0xC0) \
___CPUID02_PRN(0xC1) \
___CPUID02_PRN(0xC2) \
___CPUID02_PRN(0xC3) \
___CPUID02_PRN(0xCA) 

#define CPUID02_CACHE_PRN() \
___CPUID02_PRN(0x06) \
___CPUID02_PRN(0x08) \
___CPUID02_PRN(0x09) \
___CPUID02_PRN(0x0A) \
___CPUID02_PRN(0x0C) \
___CPUID02_PRN(0x0D) \
___CPUID02_PRN(0x0E) \
___CPUID02_PRN(0x1D) \
___CPUID02_PRN(0x21) \
___CPUID02_PRN(0x22) \
___CPUID02_PRN(0x23) \
___CPUID02_PRN(0x24) \
___CPUID02_PRN(0x25) \
___CPUID02_PRN(0x29) \
___CPUID02_PRN(0x2C) \
___CPUID02_PRN(0x30) \
___CPUID02_PRN(0x40) \
___CPUID02_PRN(0x41) \
___CPUID02_PRN(0x42) \
___CPUID02_PRN(0x43) \
___CPUID02_PRN(0x44) \
___CPUID02_PRN(0x45) \
___CPUID02_PRN(0x46) \
___CPUID02_PRN(0x47) \
___CPUID02_PRN(0x48) \
___CPUID02_PRN(0x49) \
___CPUID02_PRN(0x4A) \
___CPUID02_PRN(0x4B) \
___CPUID02_PRN(0x4C) \
___CPUID02_PRN(0x4D) \
___CPUID02_PRN(0x4E) \
___CPUID02_PRN(0x60) \
___CPUID02_PRN(0x66) \
___CPUID02_PRN(0x67) \
___CPUID02_PRN(0x68) \
___CPUID02_PRN(0x70) \
___CPUID02_PRN(0x71) \
___CPUID02_PRN(0x72) \
___CPUID02_PRN(0x78) \
___CPUID02_PRN(0x79) \
___CPUID02_PRN(0x7A) \
___CPUID02_PRN(0x7B) \
___CPUID02_PRN(0x7C) \
___CPUID02_PRN(0x7D) \
___CPUID02_PRN(0x7F) \
___CPUID02_PRN(0x80) \
___CPUID02_PRN(0x82) \
___CPUID02_PRN(0x83) \
___CPUID02_PRN(0x84) \
___CPUID02_PRN(0x85) \
___CPUID02_PRN(0x86) \
___CPUID02_PRN(0x87) \
___CPUID02_PRN(0xD0) \
___CPUID02_PRN(0xD1) \
___CPUID02_PRN(0xD2) \
___CPUID02_PRN(0xD6) \
___CPUID02_PRN(0xD7) \
___CPUID02_PRN(0xD8) \
___CPUID02_PRN(0xDC) \
___CPUID02_PRN(0xDD) \
___CPUID02_PRN(0xDE) \
___CPUID02_PRN(0xE2) \
___CPUID02_PRN(0xE3) \
___CPUID02_PRN(0xE4) \
___CPUID02_PRN(0xEA) \
___CPUID02_PRN(0xEB) \
___CPUID02_PRN(0xEC) 

#endif
