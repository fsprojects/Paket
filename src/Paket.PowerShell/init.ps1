param($installPath, $toolsPath, $package)
Import-Module (Join-Path $toolsPath Paket.PowerShell.dll) -DisableNameChecking

# restore packages when Visual Studio opens the solution
#if(Test-Path paket.dependencies){
#    Paket-Restore
#}