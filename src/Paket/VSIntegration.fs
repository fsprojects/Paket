module Paket.VSIntegration

open System
open System.IO
open Logging

let InitAutoRestore() = 
    if not <| Directory.Exists(".paket") then Directory.CreateDirectory(".paket") |> ignore
    let version = AssemblyVersionInformation.Version
    CleanDir(".paket")
    use client = createWebClient()
    for file in ["paket.targets"; "paket.bootstrapper.exe"] do
        try 
            client.DownloadFile(sprintf "https://github.com/fsprojects/Paket/releases/download/%s/%s" version file, 
                        Path.Combine(".paket", file))
            tracefn "Downloaded %s" file
        with _ -> traceErrorfn "Unable to download %s for version %s" file version

    for proj in ProjectFile.FindAllProjects(".") do
        let project = ProjectFile.Load(proj.FullName)
        project.AddImportForPaketTargets()
        project.Save()