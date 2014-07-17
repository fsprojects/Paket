#!/bin/bash

if [ ! -e packages/FAKE/tools/FAKE.exe ]; then 
  mono .nuget/NuGet.exe install FAKE -OutputDirectory packages -ExcludeVersion -version "3.2.1"
fi

if [ ! -e packages/SourceLink.Fake/Tools/Fake.fsx ]; then
  mono .nuget/NuGet.exe install SourceLink.Fake -OutputDirectory packages -ExcludeVersion
fi

if [ ! -e build.fsx ]; then 
  mono packages/FAKE/tools/FAKE.exe init.fsx
fi

mono packages/FAKE/tools/FAKE.exe $@ --fsiargs -d:MONO build.fsx 
