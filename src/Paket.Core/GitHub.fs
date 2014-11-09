module Paket.GitHub

open Paket
open Newtonsoft.Json.Linq
open System.IO
open Ionic.Zip
open Paket.Logging

[<Literal>]
let FullProjectSourceFileName = "FULLPROJECT"

// Gets the sha1 of a branch
let getSHA1OfBranch owner project branch = 
    async { 
        let! document = getFromUrl(None,sprintf "https://api.github.com/repos/%s/%s/commits/%s" owner project branch)
        let json = JObject.Parse(document)
        return json.["sha"].ToString()
    }

let private rawFileUrl owner project branch fileName =
    sprintf "https://github.com/%s/%s/raw/%s/%s" owner project branch fileName

/// Gets a dependencies file from github.
let downloadDependenciesFile(rootPath,remoteFile:ModuleResolver.ResolvedSourceFile) = async {
    let fi = FileInfo(remoteFile.Name)

    let dependenciesFileName = remoteFile.Name.Replace(fi.Name,Constants.DependenciesFileName)

    let url = rawFileUrl remoteFile.Owner remoteFile.Project remoteFile.Commit dependenciesFileName
    let! result = safeGetFromUrl(None,url)

    match result with
    | Some text ->
        let destination = Path.Combine(rootPath, remoteFile.ComputeFilePath(dependenciesFileName))

        Directory.CreateDirectory(destination |> Path.GetDirectoryName) |> ignore
        File.WriteAllText(destination, text)
        return text
    | None -> return "" }


let ExtractZip(fileName : string, targetFolder) = 
    let zip = ZipFile.Read(fileName)
    Directory.CreateDirectory(targetFolder) |> ignore
    for zipEntry in zip do
        zipEntry.Extract(targetFolder, ExtractExistingFileAction.OverwriteSilently)

let rec DirectoryCopy(sourceDirName, destDirName, copySubDirs) =
    let dir = new DirectoryInfo(sourceDirName)
    let dirs = dir.GetDirectories()


    if not <| Directory.Exists(destDirName) then
        Directory.CreateDirectory(destDirName) |> ignore

    for file in dir.GetFiles() do
        file.CopyTo(Path.Combine(destDirName, file.Name), false) |> ignore

    // If copying subdirectories, copy them and their contents to new location. 
    if copySubDirs then
        for subdir in dirs do
            DirectoryCopy(subdir.FullName, Path.Combine(destDirName, subdir.Name), copySubDirs)

/// Gets a single file from github.
let downloadGithubFiles(remoteFile:ModuleResolver.ResolvedSourceFile,destination) = async {
    match remoteFile.Name with
    | FullProjectSourceFileName ->
        let fi = FileInfo(destination)
        let projectPath = fi.Directory.FullName
        let zipFile = Path.Combine(projectPath,sprintf "%s.zip" remoteFile.Commit)
        do! downloadFromUrl(None,sprintf "https://github.com/%s/%s/archive/%s.zip" remoteFile.Owner remoteFile.Project remoteFile.Commit) zipFile

        ExtractZip(zipFile,projectPath)

        let source = Path.Combine(projectPath, sprintf "%s-%s" remoteFile.Project remoteFile.Commit)
        DirectoryCopy(source,projectPath,true)

        Directory.Delete(source,true)

    | _ ->  return! downloadFromUrl(None,rawFileUrl remoteFile.Owner remoteFile.Project remoteFile.Commit remoteFile.Name) destination
}

let DownloadSourceFile(rootPath, source:ModuleResolver.ResolvedSourceFile) = 
    async { 
        let path = FileInfo(Path.Combine(rootPath, source.FilePath)).Directory.FullName
        let versionFile = FileInfo(Path.Combine(path, "paket.version"))
        let destination = Path.Combine(rootPath, source.FilePath)
        
        let isInRightVersion = 
            if not <| versionFile.Exists then false
            else source.Commit = File.ReadAllText(versionFile.FullName)

        if isInRightVersion then 
            verbosefn "Sourcefile %s is already there." (source.ToString())
        else 
            tracefn "Downloading %s to %s" (source.ToString()) destination
            
            Directory.CreateDirectory(destination |> Path.GetDirectoryName) |> ignore
            do! downloadGithubFiles(source,destination)
            File.WriteAllText(versionFile.FullName, source.Commit)
    }