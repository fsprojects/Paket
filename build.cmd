@echo off

.paket\paket.exe restore
if errorlevel 1 (
  exit /b %errorlevel%
)

setlocal

set MSBuild=%~dp0packages\build\RoslynTools.MSBuild\tools\msbuild

powershell -NoProfile -ExecutionPolicy unrestricted -Command "[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; &([scriptblock]::Create((Invoke-WebRequest -UseBasicParsing 'https://dot.net/v1/dotnet-install.ps1'))) -InstallDir './.dotnet' -Version '3.1.302'"

packages\build\FAKE\tools\FAKE.exe build.fsx %*

endlocal