/// Contains methods for the restore process.
module Paket.NugetDownload

open Paket
open System.IO
open Paket.Logging
open Paket.ModuleResolver
open Paket.PackageResolver
open Paket.PackageSources
open FSharp.Control.AsyncExtensions


/// Downloads and extracts a package.
let ExtractPackage(sources, force, package : ResolvedPackage) = 
    async { 
        let v = package.Version.ToString()
        match package.Source with
        | Nuget source -> 
            let auth = 
                sources |> List.tryPick (fun s -> 
                               match s with
                               | Nuget s -> s.Auth
                               | _ -> None)
            try 
                let! folder = Nuget.DownloadPackage(auth, source.Url, package.Name, v, force)
                return package, Nuget.GetLibFiles folder
            with _ when force = false -> 
                tracefn "Something went wrong with the download of %s %s - automatic retry with --force." package.Name v
                let! folder = Nuget.DownloadPackage(auth, source.Url, package.Name, v, true)
                return package, Nuget.GetLibFiles folder
        | LocalNuget path -> 
            let packageFile = Path.Combine(path, sprintf "%s.%s.nupkg" package.Name v)
            let! folder = Nuget.CopyFromCache(packageFile, package.Name, v, force)
            return package, Nuget.GetLibFiles folder
    }

let DownloadSourceFile(rootPath, source:ResolvedSourceFile) = 
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
            do! GitHub.downloadGithubFiles(source,destination)
            File.WriteAllText(versionFile.FullName, source.Commit)
    }