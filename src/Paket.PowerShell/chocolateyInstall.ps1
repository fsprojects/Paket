$tools = "$env:chocolateyPackageFolder\tools"
. "$tools\Install-PSModulePath.ps1"
Install-PSModulePath -pathToInstall $tools -pathType ([System.EnvironmentVariableTarget]::Machine)