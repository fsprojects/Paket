#!/bin/bash

mono .nuget/NuGet.exe install Paket -OutputDirectory packages -Prerelease -ExcludeVersion
mono .nuget/NuGet.exe install SourceLink.Fake -OutputDirectory packages -ExcludeVersion

mono packages/Paket/tools/Paket.exe install
mono packages/FAKE/tools/FAKE.exe $@ --fsiargs -d:MONO build.fsx 