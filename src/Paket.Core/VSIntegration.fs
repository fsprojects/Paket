module Paket.VSIntegration

open System.IO
open Logging
open System
open Chessie.ErrorHandling
open Domain


let private getLatestVersionFromJson (data : string) =
    try 
        let start = data.IndexOf("tag_name") + 11
        let end' = data.IndexOf("\"", start)
        (data.Substring(start, end' - start)) |> SemVer.Parse |> ok
    with _ ->
        fail ReleasesJsonParseError

let TurnOnAutoRestore environment =
    let exeDir = Path.Combine(environment.RootDirectory.FullName, ".paket")
    
    use client = createWebClient("https://github.com",None)

    let download version file = 
        trial {
            do! createDir(exeDir)
            let fileName = Path.Combine(exeDir, file)
            let url = sprintf "https://github.com/fsprojects/Paket/releases/download/%s/%s" (string version) file
            
            do! downloadFileSync url fileName client
        }

    trial { 
        let releasesUrl = "https://api.github.com/repos/fsprojects/Paket/releases";
     
        let! data = client |> downloadStringSync releasesUrl
        let! latestVersion = getLatestVersionFromJson data

        let! downloads = 
            ["paket.targets"; "paket.bootstrapper.exe"] 
            |> List.map (download latestVersion)
            |> collect

        environment.Projects
        |> List.map fst
        |> List.iter (fun project ->
            let relativePath = createRelativePath project.FileName (Path.Combine(exeDir, "paket.targets")) 
            project.AddImportForPaketTargets(relativePath)
            project.Save()
        )
    } 

let TurnOffAutoRestore environment = 
    let exeDir = Path.Combine(environment.RootDirectory.FullName, ".paket")
    
    trial {
        let paketTargetsPath = Path.Combine(exeDir, "paket.targets")
        do! removeFile paketTargetsPath

        environment.Projects
        |> List.map fst
        |> List.iter (fun project ->
            let relativePath = createRelativePath project.FileName paketTargetsPath
            project.RemoveImportForPaketTargets(relativePath)
            project.Save()
        )
    }