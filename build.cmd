@echo off

dotnet tool restore
dotnet paket restore
if errorlevel 1 (
  exit /b %errorlevel%
)

dotnet paket generate-load-scripts --group BuildScript --framework net10.0 --type fsx
if errorlevel 1 (
  exit /b %errorlevel%
)

setlocal


dotnet fsi build.fsx %*

endlocal