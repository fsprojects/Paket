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

/// Downloads the given files of the running Paket's release to the destination dir.
/// Not the latest release: from Paket 12 on, it no longer carries paket.bootstrapper.exe, and its
/// paket.targets looks for the .NET tool instead of paket.exe.
let private downloadReleaseVersionOf files destDir =
    let auth = Environment.GetEnvironmentVariable "PAKET_GITHUB_API_TOKEN"
               |> fun x -> if isNull(x) then None else Some(Token(x))

    let client = createHttpClient(Constants.GitHubUrl, auth)

    trial {
        let version = releaseVersion paketVersion

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

/// Downloads the paket.bootstrapper and paket.targets of the running Paket's release to the .paket dir
let downloadLatestBootstrapperAndTargets environment =
    let exeDir = Path.Combine(environment.RootDirectory.FullName, Constants.PaketFolderName)
    downloadReleaseVersionOf [Constants.TargetsFileName; Constants.BootstrapperFileName] exeDir

/// Downloads the paket.targets of the running Paket's release to the .paket dir
let downloadLatestTargets environment =
    let exeDir = Path.Combine(environment.RootDirectory.FullName, Constants.PaketFolderName)
    downloadReleaseVersionOf [Constants.TargetsFileName] exeDir

