# Packages needed to build:
# - libuuid
# - libuuid-devel
# - mingw32-gcc
# - mingw32-pthreads (if pthread is missing in mingw32 dist)
# - mingw32-pthreads-static (if you want static pthread)
# These are optional for WIN64 build:
# - mingw64-gcc
# - mingw64-pthreads
# - mingw64-pthreads-static
#
# Packages needed for XML output:
# - libxml2
# - libxml2-devel
# - mingw32-libxml2	 (below for windows 32/64 builds)
# - mingw32-win-iconv
# - mingw64-libxml2
# - mingw64-win-iconv


# 32/64 cross-platform compilation tip:
# gcc supports multilib which means -m32/-m64 option pretty much
# takes care of 32/64bit code generation. All you need is to place
# needed 32/64 bit libraries in place as follows:
# - libuuid.i686
# - libuuid-devel.i686
# - libgcc.i686
# - glibc-devel.i686
CC := gcc

ifndef march
march=64
#march=32
endif

# Windows cross-platform compilation
# Choose your mingw32 compiler name! 
ifeq ($(strip $(march)), 32)
#MINGW_PREFIX :=i686-pc-mingw32
MINGW_PREFIX :=i686-w64-mingw32
else
MINGW_PREFIX :=x86_64-w64-mingw32
endif
WCC := $(MINGW_PREFIX)-gcc

CFLAGS := -g -O2 -Wall

# define PMB_THREAD for threaded benchmark 
# flags for Linux 
CFLAGS_LINUX := 
LFLAGS_LINUX :=
ifeq ($(strip $(march)), 32)
CFLAGS_LINUX += -m32
LFLAGS_LINUX += -m32
else
CFLAGS_LINUX += -m64
LFLAGS_LINUX += -m64
endif

#LFLAGS_LINUX += -Wl,--section-start=.pmbench_code_page=408000
# uncomment below to compile-in multi-threaded benchmark
CFLAGS_LINUX += -DPMB_THREAD=1 -pthread
LFLAGS_LINUX += -pthread 
# uncomment below to add XALLOC
#CFLAGS_LINUX += -DXALLOC -I../xalloc
#LFLAGS_LINUX += ../xalloc/libxalloc.a

SRCPATH_WINARGP := ../argpwin
INCPATH_WINARGP := ../argpwin
LIBPATH_WINARGP := ../argpwin
#this is to Windows XP and after
CFLAGS_WIN := -D_WIN32_WINNT=0x0501 -I$(INCPATH_WINARGP)
LFLAGS_WIN := -lrpcrt4  -L$(LIBPATH_WINARGP)
# uncomment below to compile-in multi-threaded benchmark
CFLAGS_WIN += -DPMB_THREAD=1 -lpthread
LFLAGS_WIN += -lpthread
# uncomment below to add XALLOC
#CFLAGS_WIN += -DXALLOC -I../xalloc
#LFLAGS_WIN += ../xalloc/xalloc.dll 
#-save-temps

LFLAGS_WIN += $(LIBPATH_WINARGP)/argp.dll

# libxml2
CFLAGS_WIN += -DPMB_XML=1 -I/usr/include/libxml2
CFLAGS_LINUX += -DPMB_XML=1 -I/usr/include/libxml2
LXML := -lxml2

.PHONY: all clean dist dist_src dist_bin dist_bin32 dist_bin64 dist_doc check help

all: pmbench pmbench.exe

pmbench: pmbench.o pattern.o system.o access.o xmlgen.o
	$(CC) $+ -lm -luuid $(LXML) -o $@ $(LFLAGS_LINUX)
	objdump -d $@ > $@.dmp


check:
	@sym=`readelf -S -W pmbench |grep .pmbench_code_page`;\
		set -- junk $$sym;shift;\
		echo "pmbench_code_page section size:0x$$6"
	objdump -j .pmbench_code_page -t pmbench


clean:
	@rm -f pmbench pmbench.exe *.o *.obj *.dmp *.i *.s .depend 

help:
	@echo "make [all|clean|dist|dist_src|check|help]"
	@echo " all      - make pmbench and pmbench.exe only. requires argp.dll"
	@echo " clean    - clean intermediate files"
	@echo " dist     - distributable binaries under ./dist_staging"
	@echo " dist_src - distributable sources under ./dist_staging"
	@echo " check    - report binary section information"
	@echo " help     - print this help"

DISTDIR := dist_staging

dist: dist_bin dist_doc

dist_bin:
	$(MAKE) -C $(SRCPATH_WINARGP) clean; 
	export march=64;$(MAKE) -C $(SRCPATH_WINARGP)
	$(MAKE) clean
	export march=64;$(MAKE) dist_bin64
	$(MAKE) -C $(SRCPATH_WINARGP) clean; 
	export march=32;$(MAKE) -C $(SRCPATH_WINARGP)
	$(MAKE) clean
	export march=32;$(MAKE) dist_bin32

ifeq ($(strip $(MAKELEVEL)), 1)
dist_bin32: pmbench pmbench.exe
	install -D ./pmbench $(DISTDIR)/bin/linux/i686/pmbench
	install -D ./pmbench.exe $(DISTDIR)/bin/windows/win32/pmbench.exe
	install -m664 -D $(LIBPATH_WINARGP)/argp.dll $(DISTDIR)/bin/windows/win32/dll/argp.dll
	install -m664 -D /usr/$(MINGW_PREFIX)/sys-root/mingw/bin/pthreadGC2.dll $(DISTDIR)/bin/windows/win32/dll/pthreadGC2.dll
	install -m664 -D /usr/$(MINGW_PREFIX)/sys-root/mingw/bin/libgcc_s_sjlj-1.dll $(DISTDIR)/bin/windows/win32/dll/libgcc_s_sjlj-1.dll
	
dist_bin64: pmbench pmbench.exe
	install -D ./pmbench $(DISTDIR)/bin/linux/x86_64/pmbench
	install -D ./pmbench.exe $(DISTDIR)/bin/windows/win64/pmbench.exe
	install -m664 -D $(LIBPATH_WINARGP)/argp.dll $(DISTDIR)/bin/windows/win64/dll/argp.dll
	install -m664 -D /usr/$(MINGW_PREFIX)/sys-root/mingw/bin/pthreadGC2.dll $(DISTDIR)/bin/windows/win64/dll/pthreadGC2.dll

endif
dist_doc:
	install -m664 -D ./doc/pmbench.1 $(DISTDIR)/man/pmbench.1
	install -m664 -t $(DISTDIR) ./doc/README ./doc/license-*.txt

dist_src:
	install -d $(DISTDIR)/src/pmbench
	install -m664 -t $(DISTDIR)/src/pmbench *.[ch]
	install -m664 -t $(DISTDIR)/src/pmbench Makefile
	rm -f $(DISTDIR)/src/pmbench/pendingreview.c
	install -d $(DISTDIR)/src/pmbench/doc
	install -m664 -t $(DISTDIR)/src/pmbench/doc ./doc/README ./doc/license-*.txt ./doc/pmbench.1
	install -d $(DISTDIR)/src/argpwin
	install -m664 -t $(DISTDIR)/src/argpwin $(SRCPATH_WINARGP)/*.[ch]
	install -m664 -t $(DISTDIR)/src/argpwin $(SRCPATH_WINARGP)/Makefile


.SUFFIXES: .c .o .obj .dmp 

%.o: %.c
	$(CC) -c $(CFLAGS) $(CFLAGS_LINUX) -o $@ $<


pmbench.exe: pmbench.obj pattern.obj system.obj access.obj xmlgen.obj
	$(WCC) $+ -lm -lrpcrt4 $(LXML) -o $@ $(LFLAGS_WIN) 
	objdump -d $@ > $@.dmp

%.obj: %.c
	$(WCC) -c $(CFLAGS) $(CFLAGS_WIN) -o $@ $< $(LXML)


.depend:  pmbench.c pattern.c system.c access.c
	@gcc -MM $(CFLAGS) $^ > $@

-include .depend
