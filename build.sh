#!/bin/bash
mono .nuget/NuGet.exe install FAKE -OutputDirectory packages -ExcludeVersion
#workaround assembly resolution issues in build.fsx
mono packages/FAKE/tools/FAKE.exe build.fsx $@
