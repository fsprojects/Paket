#!/bin/bash

mono .nuget/NuGet.exe install Paket -OutputDirectory packages -Prerelease -ExcludeVersion
if [ $? -ne 0 ]; then
	exit 1
fi

mono packages/Paket/tools/Paket.exe install
if [ $? -ne 0 ]; then
	exit 1
fi

mono packages/FAKE/tools/FAKE.exe $@ --fsiargs -d:MONO build.fsx 