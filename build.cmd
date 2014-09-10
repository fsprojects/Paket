@echo off
cls

.nuget\nuget.exe install Paket -OutputDirectory packages -Prerelease -ExcludeVersion

packages\Paket\tools\paket.exe install
packages\FAKE\tools\FAKE.exe build.fsx %*