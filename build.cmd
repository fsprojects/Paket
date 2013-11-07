@echo off
cls
if not exist packages\FAKE\tools\Fake.exe (
  .nuget\nuget.exe install FAKE -OutputDirectory packages -Version 2.1.440-alpha -ExcludeVersion
)
packages\FAKE\tools\FAKE.exe build.fsx %*
pause
