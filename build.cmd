@echo off
cls

.nuget\nuget.exe install Paket -OutputDirectory packages -Prerelease -ExcludeVersion
.nuget\nuget.exe install SourceLink.Fake -OutputDirectory packages -ExcludeVersion

packages\Paket\tools\Paket.exe install
packages\FAKE\tools\FAKE.exe build.fsx %*