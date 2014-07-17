@echo off
cls

IF NOT EXIST packages\Nuget.Core\lib\net40-Client\NuGet.Core.dll (
  .nuget\nuget.exe install Nuget.Core -OutputDirectory packages -ExcludeVersion
)


.nuget\nuget.exe install FAKE -OutputDirectory packages -ExcludeVersion
.nuget\nuget.exe install SourceLink.Fake -OutputDirectory packages -ExcludeVersion

IF NOT EXIST build.fsx (
  packages\FAKE\tools\FAKE.exe init.fsx
)
packages\FAKE\tools\FAKE.exe build.fsx %*
