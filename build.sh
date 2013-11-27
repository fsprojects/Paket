#!/bin/bash
if [ ! -f packages/FAKE/tools/Fake.exe ]; then
  mono .NuGet/NuGet.exe install FAKE -OutputDirectory packages -ExcludeVersion -Prerelease
fi
mono packages/FAKE/tools/FAKE.exe build.fsx $@
