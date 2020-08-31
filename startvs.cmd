:: Does `startvs.cmd` needs a Copyright HEADER ? as this was inspired from `dotnet/aspnetcore` ? 

@ECHO OFF
SETLOCAL

:: This command launches a Visual Studio solution with environment variables required to use a local version of the .NET Core SDK.

:: This tells .NET Core to use .dotnet\dotnet.exe
SET DOTNET_ROOT=%AppData%\..\Local\dotnetcore\

:: This tells .NET Core not to go looking for .NET Core in other places
SET DOTNET_MULTILEVEL_LOOKUP=0

:: Put our local dotnet.exe on PATH first so Visual Studio knows which one to use
SET PATH=%DOTNET_ROOT%;%PATH%

SET sln=%~1

IF "%sln%"=="" (
    SET sln=paket.sln
)

IF NOT EXIST "%DOTNET_ROOT%\dotnet.exe" (
    echo .NET Core has not yet been installed. Running restore.cmd to install it.
    call restore.cmd
)

start "" "%sln%"
