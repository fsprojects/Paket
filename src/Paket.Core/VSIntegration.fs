module Paket.VSIntegration

open System.IO
open Logging
open System
open Rop
open Domain

let downloadString (url : string) (client : System.Net.WebClient) = 
    try 
        client.DownloadString url |> succeed
    with _ ->
        DownloadError url |> fail 

let downloadFile (url : string) (fileName : string) (client : System.Net.WebClient) = 
    try 
        client.DownloadFile(url, fileName) |> succeed
    with _ ->
        DownloadError url |> fail 

let deleteFile (fileName : string) =
    try 
        File.Delete fileName |> succeed
    with _ ->
        FileDeleteError fileName |> fail

let getLatestVersionFromJson (data : string) =
    try 
        let start = data.IndexOf("tag_name") + 11
        let end' = data.IndexOf("\"", start)
        succeed (data.Substring(start, end' - start))
    with _ ->
        fail ReleasesJsonParseError

let InitAutoRestoreR(rootDirectory : DirectoryInfo) =
    let exeDir = Path.Combine(rootDirectory.FullName, ".paket")
    CreateDir(exeDir)
    use client = createWebClient None

    let download version file = 
        rop {
            let fileName = Path.Combine(exeDir, file)
            do! deleteFile fileName
            let url = sprintf "https://github.com/fsprojects/Paket/releases/download/%s/%s" version file
            do! downloadFile url fileName client
        }

    rop { 
        let releasesUrl = "https://api.github.com/repos/fsprojects/Paket/releases";
        let! data = client |> downloadString releasesUrl
        let! latestVersion = getLatestVersionFromJson data

        let! downloads = 
            ["paket.targets"; "paket.bootstrapper.exe"] 
            |> List.map (download latestVersion)
            |> collect

        let projectsUnderPaket =
            ProjectFile.FindAllProjects rootDirectory.FullName
            |> Array.filter (fun project -> ProjectFile.FindReferencesFile(FileInfo(project.FileName)).IsSome)

        projectsUnderPaket
        |> Array.iter (fun project ->
            let relativePath = 
                createRelativePath project.FileName (Path.Combine(root, ".paket", "paket.targets")) 
            project.AddImportForPaketTargets(relativePath)
            project.Save()
        )

        return ()
    } 

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