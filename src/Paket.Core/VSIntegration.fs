module Paket.VSIntegration

open System.IO
open Logging
open System

let InitAutoRestore(dependenciesFileName) =
    let root =
        if dependenciesFileName = Constants.DependenciesFileName then
            "."
        else
            Path.GetDirectoryName dependenciesFileName

    CreateDir(Path.Combine(root,".paket"))
    use client = createWebClient None

    let releasesUrl = "https://api.github.com/repos/fsprojects/Paket/releases";
    let data = client.DownloadString(releasesUrl)
    let start = data.IndexOf("tag_name") + 11
    let end' = data.IndexOf("\"", start)
    let latestVersion = data.Substring(start, end' - start);
    
    for file in ["paket.targets"; "paket.bootstrapper.exe"] do
        try
            File.Delete(Path.Combine(root, ".paket", file))
        with _ -> traceErrorfn "Unable to delete %s" file
        try 
            client.DownloadFile(sprintf "https://github.com/fsprojects/Paket/releases/download/%s/%s" latestVersion file, 
                        Path.Combine(root, ".paket", file))
            tracefn "Downloaded %s" file
        with _ -> traceErrorfn "Unable to download %s for version %s" file latestVersion

    let projectsUnderPaket =
        ProjectFile.FindAllProjects root
        |> Array.filter (fun project -> ProjectFile.FindReferencesFile(FileInfo(project.FileName)).IsSome)

    for project in projectsUnderPaket do
        let relativePath = 
            createRelativePath project.FileName (Path.Combine(root, ".paket", "paket.targets")) 
        project.AddImportForPaketTargets(relativePath)
        project.Save()