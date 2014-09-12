@echo off
cls

.nuget\nuget.exe install Paket -OutputDirectory packages -Prerelease -ExcludeVersion
if errorlevel 1 (
  exit /b %errorlevel%
)

packages\Paket\tools\paket.exe install
if errorlevel 1 (
  exit /b %errorlevel%
)

packages\FAKE\tools\FAKE.exe build.fsx %*
