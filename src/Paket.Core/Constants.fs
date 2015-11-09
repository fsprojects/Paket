module Paket.Constants

open System
open System.IO
open Paket.Domain

[<Literal>]
let GitHubUrl = "https://github.com"

[<Literal>]
let DefaultNugetStream = "https://nuget.org/api/v2"

[<Literal>]
let DefaultNugetV3Stream = "http://api.nuget.org/v3/index.json"

[<Literal>]
let GitHubReleasesUrl = "https://api.github.com/repos/fsprojects/Paket/releases"

[<Literal>]
let GithubReleaseDownloadUrl = "https://github.com/fsprojects/Paket/releases/download"

[<Literal>]
let LockFileName = "paket.lock"

[<Literal>]
let DependenciesFileName = "paket.dependencies"

[<Literal>]
let PaketFolderName = ".paket"

[<Literal>]
let BootstrapperFileName = "paket.bootstrapper.exe"

[<Literal>]
let TargetsFileName = "paket.targets"

[<Literal>]
let ReferencesFile = "paket.references"

[<Literal>]
let AccessLockFileName = "paket.locked"

[<Literal>]
let PaketFilesFolderName = "paket-files"

[<Literal>]
let PackagesFolderName = "packages"

[<Literal>] 
let SolutionFolderProjectGuid = "2150E333-8FDC-42A3-9474-1A3956D46DE8"

[<Literal>]
let PaketVersionFileName = "paket.version"

[<Literal>]
let TemplateFile = "paket.template"

[<Literal>]
let PackagesConfigFile = "packages.config"

[<Literal>]
let FullProjectSourceFileName = "FULLPROJECT"

let MainDependencyGroup = GroupName "Main"

[<Literal>]
let ProjectDefaultNameSpace = "http://schemas.microsoft.com/developer/msbuild/2003"

let AppDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)

let PaketConfigFolder = Path.Combine(AppDataFolder, "Paket")

let PaketConfigFile = Path.Combine(PaketConfigFolder, "paket.config")

/// The magic unpublished date is 1900-01-01T00:00:00
let MagicUnlistingDate = DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.FromHours(-8.)).DateTime