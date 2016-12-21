#ifndef __RDTSC_H__
#define __RDTSC_H__
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

#ifdef __MSVC__
#error "Need gcc compiler (e.g., mingw32)"
#endif
#include <inttypes.h>
static inline
uint64_t rdtsc(void) __attribute__((always_inline));

static inline
uint64_t rdtscp(void) __attribute__((always_inline));

#if defined(__i386__)
static inline
uint64_t rdtsc(void) 
{
    uint64_t val;

    asm volatile ( "rdtsc" : "=A"(val));

    return val;
}

static inline
uint64_t rdtscp(void) 
{
    uint64_t val;

    asm volatile ( "rdtscp" : "=A"(val) :: "ecx" );

    return val;
}
#elif defined(__x86_64__)
/*
 * According to Intel IDM, high 32 bits of rax, rdx, (and rcx in case of rdtscp)
 * are zeroed.
 */
static inline
uint64_t rdtsc(void) 
{
    uint64_t rax, rdx;

    asm volatile ( "rdtsc" : "=a"(rax), "=d"(rdx) );

    return rax | (rdx << 32);
}

static inline
uint64_t rdtscp(void) 
{
    uint64_t rax, rdx;

    asm volatile ( "rdtscp" : "=a"(rax), "=d"(rdx) : : "rcx" );

    return rax | (rdx << 32);
}
#endif

#endif
