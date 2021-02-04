module Paket.Constants

open System
open System.IO
open Paket.Domain


let [<Literal>] GitHubUrl                 = "https://github.com"
let [<Literal>] DefaultNuGetStream        = "https://www.nuget.org/api/v2"
let [<Literal>] DefaultNuGetV3Stream      = "https://api.nuget.org/v3/index.json"
let [<Literal>] GitHubReleasesUrl         = "https://api.github.com/repos/fsprojects/Paket/releases"
let [<Literal>] GithubReleaseDownloadUrl  = "https://github.com/fsprojects/Paket/releases/download"
/// 'paket.lock'
let [<Literal>] LockFileName              = "paket.lock"
/// 'paket.local'
let [<Literal>] LocalFileName             = "paket.local"
/// 'paket.restore.sha512'
let [<Literal>] RestoreHashFile           = "paket.restore.cached"
/// 'paket.dependencies'
let [<Literal>] DependenciesFileName      = "paket.dependencies"
/// '.paket'
let [<Literal>] PaketFolderName           = ".paket"
let [<Literal>] BootstrapperFileName      = "paket.bootstrapper.exe"
let [<Literal>] PaketFileName             = "paket.exe"
let [<Literal>] TargetsFileName           = "paket.targets"
let [<Literal>] ReferencesFile            = "paket.references"
let [<Literal>] AccessLockFileName        = "paket.processlock"
let [<Literal>] PaketFilesFolderName      = "paket-files"
let [<Literal>] DefaultPackagesFolderName = "packages"
let [<Literal>] SolutionFolderProjectGuid = "2150E333-8FDC-42A3-9474-1A3956D46DE8"
let [<Literal>] PaketVersionFileName      = "paket.version"
let [<Literal>] TemplateFile              = "paket.template"
let [<Literal>] PackagesConfigFile        = "packages.config"
let [<Literal>] NuGetConfigFile           = "NuGet.Config"
let [<Literal>] FullProjectSourceFileName = "FULLPROJECT"
let [<Literal>] ProjectDefaultNameSpace   = "http://schemas.microsoft.com/developer/msbuild/2003"
let [<Literal>] ProjectDefaultNameSpaceCore  = "http://schemas.microsoft.com/developer/msbuild/2003"
let [<Literal>] NuGetProtocolVersion  = "4.1.0"

#if DOTNETCORE
module Environment =
    type SpecialFolder =
        | ApplicationData
        | UserProfile
        | LocalApplicationData
        | ProgramFiles
        | ProgramFilesX86
    let GetFolderPath sf =
        let envVar, monoPathSuffix =
            match sf with
            | ApplicationData -> "APPDATA", ".config"
            | UserProfile -> "USERPROFILE", ""
            | LocalApplicationData -> "LocalAppData", ".local/share"
            | ProgramFiles -> "PROGRAMFILES", ".programfiles"
            | ProgramFilesX86 -> "PROGRAMFILES(X86)", ".programfilesX86"

        let isWindows =
            System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Windows)
        let homePath =
            if isWindows then
                let defaultPath = Environment.GetEnvironmentVariable("USERPROFILE")
                if System.String.IsNullOrEmpty defaultPath then
                    Environment.GetEnvironmentVariable("HOME")
                else defaultPath
            else Environment.GetEnvironmentVariable("HOME")
        if isWindows then
            let res = Environment.GetEnvironmentVariable(envVar)
            if System.String.IsNullOrEmpty res then
                System.IO.Path.Combine(homePath, monoPathSuffix)
            else res
        else
            System.IO.Path.Combine(homePath, monoPathSuffix)
#endif

let MainDependencyGroup = GroupName "Main"

let getEnVar variable =
    let envar = Environment.GetEnvironmentVariable variable
    if String.IsNullOrEmpty envar then None else Some envar

let getEnvDir specialPath =
    let dir = Environment.GetFolderPath specialPath
    if String.IsNullOrEmpty dir then None else Some dir

let AppDataFolder =
    getEnvDir Environment.SpecialFolder.ApplicationData
    |> Option.defaultWith (fun _ ->
        let fallback = Path.GetFullPath ".paket"
        Logging.traceWarnfn "Could not find AppDataFolder, try to set the APPDATA environment variable. Using '%s' instead." fallback
        if not (Directory.Exists fallback) then
            Directory.CreateDirectory fallback |> ignore
        fallback)


let PaketConfigFolder   = Path.Combine(AppDataFolder, "Paket")
let PaketConfigFile     = Path.Combine(PaketConfigFolder, "paket.config")

let PaketRestoreHashFilePath = Path.Combine(PaketFilesFolderName, RestoreHashFile)

let LocalRootForTempData =
    getEnvDir Environment.SpecialFolder.UserProfile
    |> Option.orElse (getEnvDir Environment.SpecialFolder.LocalApplicationData)
    |> Option.defaultWith (fun _ ->
        let fallback = Path.GetFullPath ".paket"
        Logging.traceWarnfn "Could not detect a root for our (user specific) temporary files. Try to set the 'HOME' or 'LocalAppData' environment variable!. Using '%s' instead." fallback
        if not (Directory.Exists fallback) then
            Directory.CreateDirectory fallback |> ignore
        fallback
    )

let GitRepoCacheFolder = Path.Combine(LocalRootForTempData,".paket","git","db")

let [<Literal>] GlobalPackagesFolderEnvironmentKey = "NUGET_PACKAGES"

let UserNuGetPackagesFolder =
    getEnVar GlobalPackagesFolderEnvironmentKey
    |> Option.map (fun path ->
        path.Replace (Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
    ) |> Option.defaultWith (fun _ ->
        Path.Combine (LocalRootForTempData,".nuget","packages")
    )

/// The magic unpublished date is 1900-01-01T00:00:00
let MagicUnlistingDate = DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.FromHours(-8.)).DateTime

/// The NuGet cache folder.
let NuGetCacheFolder =
    getEnVar "NuGetCachePath"
    |> Option.bind (fun cachePath ->
        let di = DirectoryInfo cachePath
        if not di.Exists then di.Create()
        Some di.FullName
    ) |> Option.orElseWith (fun _ ->
        getEnvDir Environment.SpecialFolder.LocalApplicationData
        |> Option.bind (fun appData ->
            let di = DirectoryInfo (Path.Combine (appData, "Nuget", "Cache"))
            if not di.Exists then
                di.Create ()
            Some di.FullName
    ))|> Option.defaultWith (fun _ ->
        let fallback = Path.GetFullPath ".paket"
        Logging.traceWarnfn "Could not find LocalApplicationData folder, try to set the 'LocalAppData' environment variable. Using '%s' instead" fallback
        fallback
    )
