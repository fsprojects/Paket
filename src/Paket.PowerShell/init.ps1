param($installPath, $toolsPath, $package)
Import-Module (Join-Path $toolsPath Paket.PowerShell.dll) -WarningAction SilentlyContinue
# Paket-Restore here so that packages get restored when Visual Studio opens the solution