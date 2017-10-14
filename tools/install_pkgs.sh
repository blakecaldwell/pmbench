#!/bin/bash

# pmbench distribution build
# install packages for fedora system with dnf

declare -a pkgs_needed=(
 "libuuid-devel.x86_64"
 "mingw32-gcc.x86_64"
 "mingw32-winpthreads.noarch"
 "mingw64-gcc.x86_64"
 "mingw64-winpthreads.noarch"
 "libxml2-devel.x86_64"
 "mingw32-libxml2.noarch"
 "mingw32-win-iconv.noarch"
 "mingw64-libxml2.noarch"
 "mingw64-win-iconv.noarch"
 "libxml2-devel.i686"
 "libuuid-devel.i686"
 "libgcc.i686"
 "glibc-devel.i686"
 "numactl-devel.x86_64"
 "numactl-devel.i686"
)

declare -a to_install=( )

if ! [[ $(uname -m) == "x86_64" ]]; then
    echo "this script is for x86_86 host. bailing out"
    exit 1
fi



function ispkginstalled {
    if dnf -q list --installed "$@" > /dev/null 2>&1; then
	true	
    else
	false
    fi
}

function ispkgavailable {
    if dnf -q list --available "$@" > /dev/null 2>&1; then
	true	
    else
	false
    fi
}


for i in "${pkgs_needed[@]}"; do
    if ispkginstalled $i; then
	echo "$i is already installed"
    else 
	if ispkgavailable $i; then
	    echo "$i is available and to be installed"
	    to_install+=("$i")
	else 
	    echo "$i is not available. quitting"
	    exit 1
	fi
    fi
done


# check if running as root. if not, print out package list and exit.
if [[ $(id -u) -ne 0 ]]; then
    echo "You are not root"
    declare -p to_install
    exit 1
fi

# now install pkgs. 
dnf install ${to_install[*]}
#echo "${to_install[*]}"



