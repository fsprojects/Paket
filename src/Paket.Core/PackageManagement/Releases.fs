module Paket.Releases

open System.IO
open Pri.LongPath
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

/// Downloads the latest version of the given files to the destination dir
let private downloadLatestVersionOf files destDir =
    use client = createHttpClient(Constants.GitHubUrl, None)

    trial {
        let latest = "https://github.com/fsprojects/Paket/releases/latest";
        let! data = client |> downloadStringSync latest
        let title = data.Substring(data.IndexOf("<title>") + 7, (data.IndexOf("</title>") + 8 - data.IndexOf("<title>") + 7)) // grabs everything in the <title> tag
        let version = title.Split(' ').[1] // Release, 1.34.0, etc, etc, etc <-- the release number is the second part fo this split string
        let latestVersion = SemVer.Parse version

        let files =
            files
            |> List.map (fun file -> FileInfo(Path.Combine(destDir, file)))

        let isOudated =
            files
            |> List.exists (fun file -> doesNotExistsOrIsNewer file latestVersion)

        let! downloads =
            if isOudated then files else []
            |> List.map (fun file -> download latestVersion file client)
            |> collect
        
        ()
    }

/// Downloads the latest version of the paket.bootstrapper and paket.targets to the .paket dir
let downloadLatestBootstrapperAndTargets environment =
    let exeDir = Path.Combine(environment.RootDirectory.FullName, Constants.PaketFolderName)
    downloadLatestVersionOf [Constants.TargetsFileName; Constants.BootstrapperFileName] exeDir

