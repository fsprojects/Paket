#!/bin/bash

mono .nuget/NuGet.exe install Paket -OutputDirectory packages -Prerelease -ExcludeVersion
exit_code=$?
if [ $exit_code -ne 0 ]; then
	exit $exit_code
fi

mono packages/Paket/tools/paket.exe install
exit_code=$?
if [ $exit_code -ne 0 ]; then
	exit $exit_code
fi

mono packages/FAKE/tools/FAKE.exe $@ --fsiargs -d:MONO build.fsx 