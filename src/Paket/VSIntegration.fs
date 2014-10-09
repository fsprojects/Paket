module Paket.VSIntegration

open System.IO
open Logging
open System

let InitAutoRestore() = 
    if not <| Directory.Exists(".paket") then Directory.CreateDirectory(".paket") |> ignore
    CleanDir(".paket")
    use client = createWebClient None

    let releasesUrl = "https://api.github.com/repos/fsprojects/Paket/releases";
    let data = client.DownloadString(releasesUrl)
    let start = data.IndexOf("tag_name") + 11
    let end' = data.IndexOf("\"", start)
    let latestVersion = data.Substring(start, end' - start);
    
    for file in ["paket.targets"; "paket.bootstrapper.exe"] do
        try 
            client.DownloadFile(sprintf "https://github.com/fsprojects/Paket/releases/download/%s/%s" latestVersion file, 
                        Path.Combine(".paket", file))
            tracefn "Downloaded %s" file
        with _ -> traceErrorfn "Unable to download %s for version %s" file latestVersion

    for project in ProjectFile.FindAllProjects(".") do
        let relativePath = 
            createRelativePath project.FileName (Path.Combine(Environment.CurrentDirectory, ".paket\\paket.targets")) 
        project.AddImportForPaketTargets(relativePath)
        project.Save()