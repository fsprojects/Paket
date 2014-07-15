@echo off
cls
.nuget\nuget.exe install FAKE -OutputDirectory packages -ExcludeVersion
.nuget\nuget.exe install SourceLink.Fake -OutputDirectory packages -ExcludeVersion
packages\FAKE\tools\FAKE.exe build.fsx %*
