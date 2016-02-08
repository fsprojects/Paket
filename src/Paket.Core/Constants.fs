module Paket.Constants

open System
open System.IO
open Paket.Domain


let [<Literal>] GitHubUrl                 = "https://github.com"
let [<Literal>] DefaultNuGetStream        = "https://www.nuget.org/api/v2"
let [<Literal>] DefaultNuGetV3Stream      = "http://api.nuget.org/v3/index.json"
let [<Literal>] GitHubReleasesUrl         = "https://api.github.com/repos/fsprojects/Paket/releases"
let [<Literal>] GithubReleaseDownloadUrl  = "https://github.com/fsprojects/Paket/releases/download"
let [<Literal>] LockFileName              = "paket.lock"
let [<Literal>] DependenciesFileName      = "paket.dependencies"
let [<Literal>] PaketFolderName           = ".paket"
let [<Literal>] BootstrapperFileName      = "paket.bootstrapper.exe"
let [<Literal>] TargetsFileName           = "paket.targets"
let [<Literal>] ReferencesFile            = "paket.references"
let [<Literal>] AccessLockFileName        = "paket.locked"
let [<Literal>] PaketFilesFolderName      = "paket-files"
let [<Literal>] PackagesFolderName        = "packages"
let [<Literal>] SolutionFolderProjectGuid = "2150E333-8FDC-42A3-9474-1A3956D46DE8"
let [<Literal>] PaketVersionFileName      = "paket.version"
let [<Literal>] TemplateFile              = "paket.template"
let [<Literal>] PackagesConfigFile        = "packages.config"
let [<Literal>] NuGetConfigFile           = "NuGet.Config"
let [<Literal>] FullProjectSourceFileName = "FULLPROJECT"
let [<Literal>] ProjectDefaultNameSpace   = "http://schemas.microsoft.com/developer/msbuild/2003"

let MainDependencyGroup = GroupName "Main"
let AppDataFolder       = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
let PaketConfigFolder   = Path.Combine(AppDataFolder, "Paket")
let PaketConfigFile     = Path.Combine(PaketConfigFolder, "paket.config")

let UserProfile = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile)
let GitRepoCacheFolder = Path.Combine(UserProfile,".paket","git","db")
let UserNuGetPackagesFolder = Path.Combine(UserProfile,".nuget","packages")

/// The magic unpublished date is 1900-01-01T00:00:00
let MagicUnlistingDate = DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.FromHours(-8.)).DateTime
