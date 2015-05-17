#!/usr/bin/env bash

set -eu
set -o pipefail

cd `dirname $0`


LOADER=""
FSIARGS=""
OS=${OS:-"unknown"}
if test "$OS" != "Windows_NT"
then
  LOADER=mono
  FSIARGS="-d:MONO"
fi

"$LOADER" .paket/paket.bootstrapper.exe

[ ! -e ~/.config/.mono/certs ] && mozroots --import --sync --quiet

"$LOADER" .paket/paket.exe restore

"$LOADER" packages/FAKE/tools/FAKE.exe "$@" "$FSIARGS" build.fsx
