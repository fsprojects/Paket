module Paket.Releases

open System.IO
open System.Diagnostics
open Logging
open System
open Chessie.ErrorHandling
open Paket.Domain


let private download version (file:FileInfo) client = 
    trial {
        tracen (sprintf "%A" file)

        do! createDir(file.DirectoryName)
        let url = sprintf "%s/%s/%s" Constants.GithubReleaseDownloadUrl (string version) file.Name
        
        do! downloadFileSync url file.FullName client
    }

let private doesNotExistsOrIsNewer (file : FileInfo) latest = 
    if not file.Exists then true else 
    let verInfo = FileVersionInfo.GetVersionInfo file.FullName
    if isNull verInfo || isNull verInfo.FileVersion  then false else
    let currentVersion = SemVer.Parse verInfo.FileVersion
    currentVersion < latest

/// The release of the given informational version: its tag is the version without the build metadata.
let releaseVersion (informationalVersion : string) =
    let plus = informationalVersion.IndexOf '+'
    SemVer.Parse(if plus < 0 then informationalVersion else informationalVersion.Substring(0, plus))

let private lastSupportedMajor = 11u

let private lastSupportedFallback = SemVer.Parse "11.0.0"

let private paketVersionsUrl = "https://api.nuget.org/v3-flatcontainer/paket/index.json"

/// The release to download files from: the latest published one up to the running Paket.
/// Not the latest release: from Paket 12 on, it no longer carries paket.bootstrapper.exe, and its
/// paket.targets looks for the .NET tool instead of paket.exe.
/// Nor the running Paket's own release, which doesn't exist yet while the release build runs its
/// tests. A running version below Paket 11 is a build without a version, which bounds nothing.
let releaseToDownload (running : SemVerInfo) (published : SemVerInfo seq) =
    let isVersioned = running.Major = lastSupportedMajor
    let candidates =
        published
        |> Seq.filter (fun v ->
            v.Major <= lastSupportedMajor
            && (v.PreRelease.IsNone || (isVersioned && running.PreRelease.IsSome))
            && (not isVersioned || v <= running))
        |> Seq.toList
    match candidates with
    | [] -> if isVersioned then running else lastSupportedFallback
    | _ -> List.max candidates

/// The Paket versions published on NuGet, which the release publishes on GitHub too.
let private publishedVersions () =
    // A client of its own, so that PAKET_GITHUB_API_TOKEN never reaches NuGet.
    let client = createHttpClient(paketVersionsUrl, None)
    try
        Newtonsoft.Json.Linq.JObject.Parse(client.DownloadString paketVersionsUrl).["versions"]
        |> Seq.choose (fun v -> try Some (SemVer.Parse(string v)) with _ -> None)
        |> Seq.toList
    with exn ->
        verbosefn "Could not list the Paket versions from %s: %s" paketVersionsUrl exn.Message
        []

/// Downloads the given files of the release to download to the destination dir.
let private downloadReleaseVersionOf files destDir =
    let auth = Environment.GetEnvironmentVariable "PAKET_GITHUB_API_TOKEN"
               |> fun x -> if isNull(x) then None else Some(Token(x))

    let client = createHttpClient(Constants.GitHubUrl, auth)

    trial {
        let version = releaseToDownload (releaseVersion paketVersion) (publishedVersions ())

        let files =
            files
            |> List.map (fun file -> FileInfo(Path.Combine(destDir, file)))

        let isOudated =
            files
            |> List.exists (fun file -> doesNotExistsOrIsNewer file version)

        let! downloads =
            if isOudated then files else []
            |> List.map (fun file -> download version file client)
            |> collect
        
        ()
    }

/// Downloads the paket.bootstrapper and paket.targets of the latest Paket 11 release up to the running one to the .paket dir
let downloadLatestBootstrapperAndTargets environment =
    let exeDir = Path.Combine(environment.RootDirectory.FullName, Constants.PaketFolderName)
    downloadReleaseVersionOf [Constants.TargetsFileName; Constants.BootstrapperFileName] exeDir

/// Downloads the paket.targets of the latest Paket 11 release up to the running one to the .paket dir
let downloadLatestTargets environment =
    let exeDir = Path.Combine(environment.RootDirectory.FullName, Constants.PaketFolderName)
    downloadReleaseVersionOf [Constants.TargetsFileName] exeDir

