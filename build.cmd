@echo off

rem https://github.com/dotnet/cli/issues/6317#issuecomment-294193469
set DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

.paket\paket.exe restore
if errorlevel 1 (
  exit /b %errorlevel%
)

packages\build\FAKE\tools\FAKE.exe build.fsx %*
