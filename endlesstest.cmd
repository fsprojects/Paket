@echo off

:start

del /s /q "%LOCALAPPDATA%\NuGet\Cache\"
del /s /q packages

.nuget\nuget.exe install Paket -OutputDirectory packages -Prerelease -ExcludeVersion

packages\Paket\tools\paket.exe install

goto start