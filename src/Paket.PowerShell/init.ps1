param($installPath, $toolsPath, $package)
Import-Module (Join-Path $toolsPath 'Paket.PowerShell\Paket.PowerShell.dll') -DisableNameChecking