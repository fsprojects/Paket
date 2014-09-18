#!/bin/bash

mono paket.bootstrapper.exe
exit_code=$?
if [ $exit_code -ne 0 ]; then
	exit $exit_code
fi

mono paket.exe install
exit_code=$?
if [ $exit_code -ne 0 ]; then
	exit $exit_code
fi

mono packages/FAKE/tools/FAKE.exe $@ --fsiargs -d:MONO build.fsx 