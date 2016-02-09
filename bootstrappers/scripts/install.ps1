if (Test-Path ".paket")
{
    Remove-Item -Recurse -Force ".paket"
}

mkdir .paket
$client = new-object net.webclient

$json = $client.DownloadString("https://www.nuget.org/api/v2/package-versions/paket") | ConvertFrom-Json
$latestVersion = $json.Get($json.Count-1)
$bootstrapperUrl = [System.String]::Format("https://github.com/fsprojects/Paket/releases/download/{0}/paket.bootstrapper.exe", $latestVersion)
$client.DownloadFile($bootstrapperUrl, "$pwd\.paket\paket.bootstrapper.exe")
$paketUrl = [System.String]::Format("https://github.com/fsprojects/Paket/releases/download/{0}/paket.exe", $latestVersion)
$client.DownloadFile($paketUrl, "$pwd\.paket\paket.exe")

.\.paket\paket.exe "init"


if (Test-Path ".hgignore")
{
    [System.IO.File]::AppendAllText("$pwd\.hgignore", ".paket/paket.exe`n")
}

if (Test-Path ".gitignore")
{
    [System.IO.File]::AppendAllText("$pwd\.gitignore", ".paket/paket.exe`n")
}


