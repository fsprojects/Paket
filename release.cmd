@echo off
cls

.nuget\nuget.exe install Paket -OutputDirectory packages -Prerelease -ExcludeVersion

packages\Paket\tools\paket.exe install
packages\FAKE\tools\FAKE.exe build.fsx Release "NugetKey=6cfcf909-7b5a-417d-a706-d1a1ab9b4b9e"