#!/bin/bash

mono .nuget/NuGet.exe install FAKE -OutputDirectory packages -ExcludeVersion
mono .nuget/NuGet.exe install SourceLink.Fake -OutputDirectory packages -ExcludeVersion

if [ ! -e build.fsx ]; then 
  mono packages/FAKE/tools/FAKE.exe init.fsx
fi

mono packages/FAKE/tools/FAKE.exe $@ --fsiargs -d:MONO build.fsx 
