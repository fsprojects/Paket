@echo off

.paket\paket.exe restore
if errorlevel 1 (
  exit /b %errorlevel%
)

setlocal

set MSBuild=%~dp0packages\build\RoslynTools.MSBuild\tools\msbuild

packages\build\FAKE\tools\FAKE.exe build.fsx %*

endlocal