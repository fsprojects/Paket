@echo off
cls

IF NOT EXIST packages\FAKE\tools\FAKE.exe  (
  .nuget\nuget.exe install FAKE -OutputDirectory packages -ExcludeVersion
  .nuget\nuget.exe install SourceLink.Fake -OutputDirectory packages -ExcludeVersion
)

IF NOT EXIST build.fsx (
  packages\FAKE\tools\FAKE.exe init.fsx
)
packages\FAKE\tools\FAKE.exe build.fsx %*
