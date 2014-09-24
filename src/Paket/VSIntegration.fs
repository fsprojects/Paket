module Paket.VSIntegration

open System.IO
open Logging

let InitAutoRestore() = 
    if not <| Directory.Exists(".paket") then Directory.CreateDirectory(".paket") |> ignore
    CleanDir(".paket")
    use client = createWebClient()

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

    for proj in ProjectFile.FindAllProjects(".") do
        let project = ProjectFile.Load(proj.FullName)
        project.AddImportForPaketTargets()
        project.Save()