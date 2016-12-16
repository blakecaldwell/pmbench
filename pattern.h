#ifndef __PATTERN_H__
#define __PATTERN_H__
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

//#define USE_LONG_DOUBLE 1
#ifdef USE_LONG_DOUBLE
typedef long double fp_t;
#else
typedef double fp_t;
#endif

typedef struct pattern_generator {
    void * (*alloc_pattern)(size_t size, fp_t param1, uint32_t random_seed);
    size_t (*get_next)(void* ctx);
    size_t (*get_warmup_run)(void* ctx);
    int (*free_pattern)(void* ctx);
    const char* name;
    const char* description;
} pattern_generator;

extern pattern_generator linear_pattern;
extern pattern_generator uniform_pattern;
extern pattern_generator normal_pattern;
extern pattern_generator pareto_pattern;
extern pattern_generator zipf_pattern;

extern pattern_generator* get_pattern_from_name(const char* str);

typedef uint32_t (*get_pattern_fn)(uint64_t *); 
extern get_pattern_fn get_offset_function(int n);

extern uint32_t roll_dice(uint64_t* state);
#endif
