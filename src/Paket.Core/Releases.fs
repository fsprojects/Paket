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

let private download version file destDir client = 
    trial {
        do! createDir(destDir)
        let fileName = Path.Combine(destDir, file)
        let url = sprintf "https://github.com/fsprojects/Paket/releases/download/%s/%s" (string version) file
        
        do! downloadFileSync url fileName client
    }

let downloadLatestVersionOf files destDir =
    let releasesUrl = "https://api.github.com/repos/fsprojects/Paket/releases";
    use client = createWebClient("https://github.com",None)

    trial {
        let! data = client |> downloadStringSync releasesUrl
        let! latestVersion = getLatestVersionFromJson data

        files
        |> List.filter (fun f -> not <| File.Exists(f))
        |> List.map (fun file -> download latestVersion file destDir client)
        |> collect
        |> ignore
    }

let downloadLatestBootstrapper environment =        
    let exeDir = Path.Combine(environment.RootDirectory.FullName, ".paket")

    downloadLatestVersionOf ["paket.bootstrapper.exe"] exeDir

let downloadLatestBootstrapperAndTargets environment =        
    let exeDir = Path.Combine(environment.RootDirectory.FullName, ".paket")

    downloadLatestVersionOf ["paket.targets"; "paket.bootstrapper.exe"] exeDir
