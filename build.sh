#!/bin/bash

mono .nuget/NuGet.exe install FAKE -OutputDirectory packages -ExcludeVersion
mono .nuget/NuGet.exe install SourceLink.Fake -OutputDirectory packages -ExcludeVersion

mono packages/FAKE/tools/FAKE.exe $@ --fsiargs -d:MONO build.fsx 
