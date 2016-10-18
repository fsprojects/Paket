[CmdletBinding()]
Param(
    [Parameter(Mandatory=$true)][System.IO.FileInfo]$Path,
    [Switch]$AddToPath
)

$old_location = Get-Location
$paket_directory = Join-Path $Path ".paket"
If (-not (Test-Path $paket_directory)) {
    New-Item -Path $paket_directory -Type Directory
}

$bootstrapper_name = 'paket.bootstrapper.exe'
$latest_request = "https://api.github.com/repos/fsprojects/Paket/releases/latest"
$executable =  Join-Path $paket_directory $bootstrapper_name

$latest = (Invoke-WebRequest -Uri $latest_request).Content | ConvertFrom-Json
$asset_request = "https://api.github.com/repos/fsprojects/Paket/releases/$($latest.id)/assets"

$bootstrapper = (Invoke-WebRequest -Uri $asset_request).Content | ConvertFrom-Json
$bootstrapper = $bootstrapper | Where name -eq $bootstrapper_name | Select browser_download_url, name -First 1

Invoke-WebRequest -Uri $bootstrapper.browser_download_url -OutFile $executable

Set-Location $paket_directory
If (Test-Path $bootstrapper_name) {
    .\paket.bootstrapper.exe

    If ($AddToPath) {
        [Environment]::SetEnvironmentVariable("Path", $env:Path + ";$paket_directory", [System.EnvironmentVariableTarget]::Machine) 
    }
} Else {
    "Download of $bootstrapper_name failed" | Write-Error
}

Set-Location $old_location
