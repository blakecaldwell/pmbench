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

#include "system.h"
#include "rdtsc.h"
#include "pattern.h"


#ifdef USE_LONG_DOUBLE 
#define exp expl
#define log logl
#define expm1 expm1l
#define log1p log1pl
#define pow powl
#define sqrt sqrtl
#endif

#define SYS_RAND_MAX 2147483647

static inline
int64_t sys_random(void) {
	return rand();
}

/* 
 * N.B. it is observed that threads calling random() concurrently has 
 * contention issues in Linux - I suspect glibc internal spinlock.
 * So even though reproducible random number sequences are _not_ needed,
 * we use reentrant versions to avoid contention which skews latencies.
 */

/*
 * linear congruential generator using Don Knuth parameters
 */
static inline
uint32_t dk_random_next(uint64_t* state) {
    (*state) = (*state) * 6364136223846793005ull + 1442695040888963407ull;
    return ((*state) >> 33);
}

struct sys_random_state {
    uint64_t dkstate;
};

static inline
void sys_random_init(struct sys_random_state* s, uint32_t seed) {
    s->dkstate = (uint64_t)seed;
}

static inline
size_t sys_random_r(struct sys_random_state* s) {
    return (size_t)dk_random_next(&s->dkstate);
}

_code
uint32_t roll_dice(uint64_t* state) {
    return dk_random_next(state);
}

/*
 * Deterministic Linear increment in range [0, size-1] with stride s (integer).
 * For instance, when size = 10 and s = 3, this generates sequence of
 *   0 3 6 9 1 4 7 2 5 8 0 3 6 9 1 4 7 2 5 8 0 3 ...
 */
typedef struct linear_context {
    size_t i, n;
    int phase;
    int s;
} linear_context;

static
void * linear_alloc_pattern_fn(size_t size, fp_t s, uint32_t random_seed)
{
    linear_context* ctx = malloc(sizeof(linear_context));
    if (!ctx) return NULL;
    ctx->n = size;
    ctx->s = (int)s;   // s must be positive integer
    ctx->i = 0;
    ctx->phase = 0;
    return ctx;
}

static
_code
size_t linear_get_number(void *ctx_)
{
    linear_context* ctx = ctx_;
    size_t val = ctx->i;
    ctx->i += ctx->s;
    if (__builtin_expect(ctx->i >= ctx->n, 0)) {
	ctx->phase++;
	if (ctx->phase >= ctx->s) {
	    ctx->phase = 0;
	}
	ctx->i = ctx->phase;
    }
    return val;
}

static
size_t linear_get_warmup_run(void *ctx_)
{
    linear_context* ctx = ctx_;
    return ctx->n;
}

static
int generic_free_pattern(void* ctx)
{
    free(ctx);
    return 0;
}

pattern_generator linear_pattern = {
    .alloc_pattern = linear_alloc_pattern_fn,
    .get_next = linear_get_number,
    .get_warmup_run = linear_get_warmup_run,
    .free_pattern = generic_free_pattern,
    .name = "linear",
    .description = "Deterministic Sequential Pattern"
};

/*
 * Uniform distribution with support [0, size-1].
 */
typedef struct uniform_context {
    struct sys_random_state rstate;
    size_t n;
} uniform_context;

static
void * uniform_alloc_pattern_fn(size_t size, fp_t dummy1, uint32_t random_seed)
{
    uniform_context* ctx = malloc(sizeof(uniform_context));
    if (!ctx) return NULL;

    sys_random_init(&ctx->rstate, random_seed + 2);  // 2 is just a random number
    ctx->n = size;
    return ctx;
}

static
_code
size_t uniform_get_number(void *ctx_)
{
    uniform_context* ctx = ctx_;   
    return sys_random_r(&ctx->rstate) % ctx->n;
}

/*
 * We somewhat arbitrarily use 4*n as the the warm-up period.
 * (Although not every pages, 4*n trials will touch significantly 
 * large amount of pages)
 */
static
size_t uniform_get_warmup_run(void *ctx_)
{
    uniform_context* ctx = ctx_;
    return 4 * ctx->n;
}

pattern_generator uniform_pattern = 
{
    .alloc_pattern = uniform_alloc_pattern_fn,
    .get_next = uniform_get_number,
    .get_warmup_run = uniform_get_warmup_run,
    .free_pattern = generic_free_pattern,
    .name = "uniform",
    .description = "Randomized Uniform Distribution"
};

extern pattern_generator uniform_pattern;

/**
 * get_ih_normal - return a random value with normal distribution 
 * in the range of [0, 12*@stdev], with mean in the middle
 *	@stdev: one standard deviation
 * NB. We use Irwin-Hall approximation. 
 * Obviously, this is not a 'true' Gaussian because range is limited and
 * return value is discrete. And also the range is dependent on @stdev,
 * namely, the range = [0, sqrt(12*ORDER)*stdev]. We use ORDER = 12, hence
 * the range becomes [0, 12*stdev]. 
 * The side benefit of using this approximation is that it is guaranteed that
 * all page frames with in this range have non-zero probability to be selected.
 *
 * Mathematics Note:
 * Let H be the sum of outcomes of i trials of random pick between [0, n).
 * Then H is Irwin-Hall with E[H] = n*i/2 and V(H) ~= n*n*i/12.
 * (If i is sufficiently large, we can approximate H to be Gaussian)
 * What we want here is a Gaussian X with E[X] = n/2 and V[X] = s*s, where s is
 * user-input standard deviation. Since all we have to do is scale H with 1/b, 
 * so we let X = H/b and solve b and i in terms of n and s.
 *   n/2 = E[X] = E[H/b] = E[H] / b =  (n*i)/(2*b)	    .... (1)
 *   s*s = V[X] = V[H/b] ~= V[H] /(b*b) = (n*n*i)/(12*b*b)  .... (2)
 * (in (2), we assume H is Gaussian)
 * Solving (1) and (2), we get b = i = (1/12) * (n/s)^2	    .... (3)
 * If we let i = 12 (= ORDER), we have n = 12*s
 */ 

typedef struct normal_ih_context {
    struct sys_random_state rstate;
    size_t n;	 // calculated n (must be 12*stdev)
    size_t stdev;	 // calculated stdev
    int shift;   // calculated shift (n_user - n)
    size_t n_user; // support [0, n_user]
} normal_ih_context;

/* setup parameter normal with support range [0, size-1], 
 * mean at size/2, stdev with approx size/12.
 * For example, for size=1000, average is 499, stdev is 83
 * For size=1005, average is 502, stdev is 84
 */

static
void * normal_ih_alloc_pattern_fn(size_t size, fp_t param1, uint32_t random_seed)
{
    normal_ih_context* ctx = malloc(sizeof(normal_ih_context));
    static const int ORDER = 12;
    int k;
    if (!ctx) return NULL;
    sys_random_init(&ctx->rstate, random_seed + 3);

    ctx->n_user = size - 1;
    k = ((ctx->n_user) + (ORDER/2) - 1) / ORDER;
    ctx->n = ORDER * k;
    ctx->stdev = k;
    ctx->shift = (ctx->n_user - ctx->n)/2;
    return ctx;
}

static
_code
size_t normal_ih_get_number(void *ctx_)
{
    normal_ih_context* ctx = ctx_;   
    static const int ORDER = 12;
    size_t sum;
    int i;
    int n = (int)ctx->n;
retry:
    sum = 0;
//    for (i = 0; i < ORDER; ++i) {
//	sum += rand() % ctx->n;
//    }
    for (i = 0; i < ORDER/6; ++i) {
	int a,b,c,d,e,f;
	a = sys_random_r(&ctx->rstate);
	b = sys_random_r(&ctx->rstate);
	c = sys_random_r(&ctx->rstate);
	d = sys_random_r(&ctx->rstate);
	e = sys_random_r(&ctx->rstate);
	f = sys_random_r(&ctx->rstate);
	sum += (a % n) + (b % n) + (c % n) + (d % n) + (e % n) + (f % n);
    }
    sum = sum / ORDER;
    sum += ctx->shift;
    if (__builtin_expect((sum > ctx->n_user || sum < 0), 0)) goto retry;
    return (size_t)sum;
}

/*
 * we just use n_user.
 */
static
size_t normal_ih_get_warmup_run(void *ctx_)
{
    normal_ih_context* ctx = ctx_;
    return ctx->n_user;
}

pattern_generator normal_ih_pattern = 
{
    .alloc_pattern = normal_ih_alloc_pattern_fn,
    .get_next = normal_ih_get_number,
    .get_warmup_run = normal_ih_get_warmup_run,
    .free_pattern = generic_free_pattern,
    .name = "normal_ih",
    .description = "Randomized Normal Distribution (Irwin-Hall)"
};

/**
 * normal distribution using Box-Muller algorithm
 */
typedef struct normal_context {
    struct sys_random_state rstate;
    size_t n, save;
    int stdev;
} normal_context;

static
void* normal_alloc_pattern_fn(size_t size, fp_t param1, uint32_t random_seed)
{
    normal_context* ctx = malloc(sizeof(normal_context));
    if (!ctx) return NULL;
    sys_random_init(&ctx->rstate, random_seed + 5);
    ctx->n = size;
    ctx->stdev = (int)param1;
    ctx->save = -1;
    return ctx;
}

static
_code
size_t normal_get_number(void *ctx_)
{
    normal_context* ctx = ctx_;   

    size_t res1, res2;
    fp_t u1, u2, x1, x2, w, y1, y2;

    if (ctx->save != -1) {
	res2 = ctx->save;
	ctx->save = -1;
	return res2;
    }

    do {
	/* u is uniformly distributed on [0, 1] */
	u1 = (fp_t)sys_random_r(&ctx->rstate) / SYS_RAND_MAX;
	u2 = (fp_t)sys_random_r(&ctx->rstate) / SYS_RAND_MAX;
	x1 = 2.0 * u1 - 1.0;
	x2 = 2.0 * u2 - 1.0;
	w = x1 * x1 + x2 * x2;
    } while (w >= 1.0);
    
    w = sqrt( (-2.0 * log(w)) / w);
    y1 = x1 * w;
    y2 = x2 * w;
    
    /* y1,y2 are Gaussian with mean 0, stdev 1 */
    y1 = y1 * ctx->stdev;
    y2 = y2 * ctx->stdev;
    
    res1 = y1 + (ctx->n / 2);
    res2 = y2 + (ctx->n / 2);

    if (__builtin_expect((res1 >= ctx->n || res1 < 0), 0)) res1 = ctx->n/2;
    if (__builtin_expect((res2 >= ctx->n || res2 < 0), 0)) res2 = ctx->n/2;
    
    ctx->save = res2;
    return res1;
}

/*
 * we just use n.
 */
static
size_t normal_get_warmup_run(void *ctx_)
{
    normal_context* ctx = ctx_;
    return ctx->n;
}

pattern_generator normal_pattern = 
{
    .alloc_pattern = normal_alloc_pattern_fn,
    .get_next = normal_get_number,
    .get_warmup_run = normal_get_warmup_run,
    .free_pattern = generic_free_pattern,
    .name = "normal",
    .description = "Randomized Normal Distribution"
};

/**
 * bounded pareto with support [0, size-1] and alpha a
 */
typedef struct pareto_context {
    struct sys_random_state rstate;
    fp_t l;
    fp_t h;
    fp_t a;
    fp_t _rep1; // 1.0 - pow(l/h, a)
    fp_t _rep2; // -1.0/a
} pareto_context;

static
void* pareto_alloc_pattern_fn(size_t size, fp_t a, uint32_t random_seed)
{
    pareto_context* ctx = malloc(sizeof(pareto_context));
    if (!ctx) return NULL;
    sys_random_init(&ctx->rstate, random_seed + 8);

    ctx->l = 1.0;
    ctx->h = (fp_t)size;
    ctx->a = a;
    ctx->_rep1 = 1.0 - pow(ctx->l/ctx->h, ctx->a);
    ctx->_rep2 = -1.0/ctx->a;
    
    return ctx;
}

/*
 * use_context version takes 1.70 seconds for 10 million samples
 * returns value in [1-num_pages]
 */
static
_code
size_t pareto_get_number(void *ctx_)
{
    pareto_context* ctx = ctx_;

    fp_t u;
    fp_t val;
    /* u is uniformly distributed on (0, 1) */
    u = (fp_t)((sys_random_r(&ctx->rstate) % SYS_RAND_MAX -1) + 1) / SYS_RAND_MAX;
    val = 1.0 - u * ctx->_rep1;
    val = ctx->l * pow(val, ctx->_rep2);
    return (size_t)val - 1;
}

/*
 * we use n * 8 (arbitrarily chosen).
 */
static
size_t pareto_get_warmup_run(void *ctx_)
{
    pareto_context* ctx = ctx_;
    return (size_t)ctx->h * 8;
}

pattern_generator pareto_pattern = 
{
    .alloc_pattern = pareto_alloc_pattern_fn,
    .get_next = pareto_get_number,
    .get_warmup_run = pareto_get_warmup_run,
    .free_pattern = generic_free_pattern,
    .name = "pareto",
    .description = "Randomized Bounded Pareto Distribution"
};


pattern_generator zipf_pattern = {
    .alloc_pattern = pareto_alloc_pattern_fn,
    .get_next = pareto_get_number,
    .get_warmup_run = pareto_get_warmup_run,
    .free_pattern = generic_free_pattern,
    .name = "zipf",
    .description = "Randomized Zipf Distribution"
};

/*
 * all patterns
 */
static pattern_generator* all_pattern[] = {
    &linear_pattern, &uniform_pattern, &normal_pattern, &normal_ih_pattern,
    &pareto_pattern, &zipf_pattern, 0
};

pattern_generator* get_pattern_from_name(const char* str)
{
    int i = 0;

    if (!str) return NULL;

    while (all_pattern[i]) {
	if (!my_strncmp(all_pattern[i]->name, str, 16))
	    return all_pattern[i];
	i++;
    }
    return NULL;
}

/*
 * page offset random generator
 */

static _code 
uint32_t offset_random(uint64_t* state)
{
    return dk_random_next(state) >> 21;
}

static _code 
uint32_t offset_constant(uint64_t* i)
{
    return (uint32_t)(*i);
}

get_pattern_fn get_offset_function(int n)
{
    if (n >= 0) return &offset_constant;

    switch (n) {
    case -1: 	
	return &offset_random; //other negative numbers could be passed here to select an offset pattern
    default: 	
	return &offset_random;
    }
}

