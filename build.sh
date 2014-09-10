#!/bin/bash
if test "$OS" = "Windows_NT"
then
  # use .Net
  .nuget/NuGet.exe install FAKE -OutputDirectory packages -ExcludeVersion
  .nuget/NuGet.exe install SourceLink.Fake -OutputDirectory packages -ExcludeVersion
  [ ! -e build.fsx ] && packages/FAKE/tools/FAKE.exe init.fsx
  packages/FAKE/tools/FAKE.exe build.fsx $@
else
  # use mono
  mono .nuget/NuGet.exe install FAKE -OutputDirectory packages -ExcludeVersion
  mono .nuget/NuGet.exe install SourceLink.Fake -OutputDirectory packages -ExcludeVersion
  [ ! -e build.fsx ] && mono packages/FAKE/tools/FAKE.exe init.fsx
  mono packages/FAKE/tools/FAKE.exe $@ --fsiargs -d:MONO build.fsx
fi
