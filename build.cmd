@echo off
cls

.nuget\nuget.exe install Paket -OutputDirectory packages -Prerelease -ExcludeVersion

packages\Paket\tools\Paket.exe install
packages\FAKE\tools\FAKE.exe build.fsx %*