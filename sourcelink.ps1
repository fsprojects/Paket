Push-Location buildtools
if (-not (Test-Path 'obj/project.assets.json')) 
{
    dotnet restore
}
dotnet sourcelink @args
Pop-Location