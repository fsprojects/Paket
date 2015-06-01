module Paket.Releases

open System.IO
open System.Diagnostics
open Logging
open System
open Chessie.ErrorHandling
open Paket.Domain

let private getLatestVersionFromJson (data : string) =
    try 
        let start = data.IndexOf("tag_name") + 11
        let end' = data.IndexOf("\"", start)
        (data.Substring(start, end' - start)) |> SemVer.Parse |> ok
    with _ ->
        fail ReleasesJsonParseError

let private download version (file:FileInfo) client = 
    trial {
        tracen (sprintf "%A" file)

        do! createDir(file.DirectoryName)
        let url = sprintf "https://github.com/fsprojects/Paket/releases/download/%s/%s" (string version) file.Name
        
        do! downloadFileSync url file.FullName client
    }

let private existsNotOrIsNewer (file:FileInfo) latest =
    if (not <| file.Exists) then true
    else
        let verInfo = FileVersionInfo.GetVersionInfo file.FullName
        let currentVersion = SemVer.Parse verInfo.FileVersion
        currentVersion < latest

let downloadLatestVersionOf files destDir =
    let releasesUrl = "https://api.github.com/repos/fsprojects/Paket/releases";
    use client = createWebClient("https://github.com",None)

    trial {
        let! data = client |> downloadStringSync releasesUrl
        let! latestVersion = getLatestVersionFromJson data

        let! downloads = 
            files
            |> List.map (fun file -> FileInfo(Path.Combine(destDir, file)))
            |> List.filter (fun file -> existsNotOrIsNewer file latestVersion)
            |> List.map (fun file -> download latestVersion file client)
            |> collect
        
        ignore downloads
    }

let downloadLatestBootstrapper environment =        
    let exeDir = Path.Combine(environment.RootDirectory.FullName, ".paket")

    downloadLatestVersionOf ["paket.bootstrapper.exe"] exeDir

let downloadLatestBootstrapperAndTargets environment =        
    let exeDir = Path.Combine(environment.RootDirectory.FullName, ".paket")

    downloadLatestVersionOf ["paket.targets"; "paket.bootstrapper.exe"] exeDir
