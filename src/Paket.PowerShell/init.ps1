param($installPath, $toolsPath, $package)

#Import-Module (Join-Path $toolsPath paket.exe) -DisableNameChecking
# vote for the above to work: https://connect.microsoft.com/PowerShell/feedbackdetail/view/1421358/import-module-with-exe
Import-Module -Assembly ([Reflection.Assembly]::LoadFile($(Join-Path $toolsPath paket.exe))) -DisableNameChecking

# Paket-Restore here so that packages get restored when Visual Studio opens the solution
# TODO