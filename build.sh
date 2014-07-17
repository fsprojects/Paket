#!/bin/bash

if [ ! -e packages/Nuget.Core/lib/net40-Client/NuGet.Core.dll ]; then 
  mono .nuget/NuGet.exe install Nuget.Core -OutputDirectory packages -ExcludeVersion 
fi

if [ ! -e packages/FAKE/tools/FAKE.exe ]; then 
  mono .nuget/NuGet.exe install FAKE -OutputDirectory packages -ExcludeVersion -version "3.2.1"
fi

if [ ! -e packages/SourceLink.Fake/Tools/Fake.fsx ]; then
  mono .nuget/NuGet.exe install SourceLink.Fake -OutputDirectory packages -ExcludeVersion
fi

if [ ! -e build.fsx ]; then 
  mono packages/FAKE/tools/FAKE.exe init.fsx
fi

#workaround assembly resolution issues in build.fsx
mono packages/FAKE/tools/FAKE.exe --fsiargs -d:MONO build.fsx $@
