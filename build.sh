#!/bin/bash
export MONO=mono

if [ ! -e packages/FAKE/tools/FAKE.exe ]; then 
  mono .nuget/NuGet.exe install FAKE -OutputDirectory packages -ExcludeVersion
fi
if [ ! -e packages/SourceLink.Fake/Tools/Fake.fsx ]; then
  mono .nuget/NuGet.exe install SourceLink.Fake -OutputDirectory packages -ExcludeVersion
fi

if [ ! -e build.fsx ]; then 
  fsharpi init.fsx
fi

#workaround assembly resolution issues in build.fsx
mono packages/FAKE/tools/FAKE.exe --fsiargs -d:MONO build.fsx $@
