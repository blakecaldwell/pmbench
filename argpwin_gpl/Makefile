# argp for Windows source files obtained form 'madp-win' project:
#
# The files here were downloaded on January 13, 2014 from 
# http://code.google.com/p/madp-win/source/browse/#hg%2Fsrc%2Fargp-standalone-1.3
# 

ifndef march
march=64
#march=32
endif

# Windows cross-platform compilation. Choose your mingw32 compiler name! 
#
ifeq ($(strip $(march)), 32)
#MINGW_PREFIX :=i686-pc-mingw32
MINGW_PREFIX :=i686-w64-mingw32
else
MINGW_PREFIX :=x86_64-w64-mingw32
endif
WCC := $(MINGW_PREFIX)-gcc

CFLAGS := -g -O2 -Wall -Wno-pointer-sign -Wno-array-bounds -D_alloca=__builtin_alloca -D_WIN32_WINNT=0x0501 

all: argp.dll

%.obj : %.c
	$(WCC) $(CFLAGS) -o $@ -c $<

sources = $(wildcard *.c)

argp.dll: $(sources:.c=.obj)
	$(WCC) -shared -o $@ $^

clean:
	@rm  -f argp.dll *.obj
