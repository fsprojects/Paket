#!/bin/bash
if [ ! -f packages/FAKE/tools/FAKE.exe ]; then
  mono .nuget/NuGet.exe install FAKE -OutputDirectory packages -ExcludeVersion
fi
#workaround assembly resolution issues in build.fsx
mono packages/FAKE/tools/FAKE.exe build.fsx $@
