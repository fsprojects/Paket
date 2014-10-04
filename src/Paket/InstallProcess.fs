/// Contains methods for the install process.
module Paket.InstallProcess

open Paket
open Paket.Logging
open Paket.ModuleResolver
open Paket.PackageResolver
open System.IO
open System.Collections.Generic
open Paket.PackageSources

/// Downloads and extracts all packages.
let ExtractPackages(sources,force, packages:PackageResolution) = 
    packages
    |> Seq.map (fun kv -> 
        async { 
            let package = kv.Value
            let v = package.Version.ToString()
            match package.Source with
            |  Nuget source ->
                let auth =
                    sources 
                    |> List.tryPick (fun s -> 
                                        match s with
                                        | Nuget s -> s.Auth
                                        | _ -> None)
                try
                    let! folder = Nuget.DownloadPackage(auth, source.Url, package.Name, v, force)
                    return Some(package, Nuget.GetLibraries folder)
                with
                | _ when force = false ->
                    tracefn "Something went wrong with the download of %s %s - automatic retry with --force." package.Name v
                    let! folder = Nuget.DownloadPackage(auth, source.Url, package.Name, v, true)
                    return Some(package, Nuget.GetLibraries folder)
            | LocalNuget path -> 
                let packageFile = Path.Combine(path, sprintf "%s.%s.nupkg" package.Name v)
                let! folder = Nuget.CopyFromCache(packageFile, package.Name, v, force)
                return Some(package, Nuget.GetLibraries folder)
        })

let DownloadSourceFiles(rootPath,sourceFiles) = 
    Seq.map (fun (source : ResolvedSourceFile) -> 
        async {
            let path = FileInfo(Path.Combine(rootPath, source.FilePath)).Directory.FullName
            let versionFile = FileInfo(Path.Combine(path,"paket.version"))
            let destination = Path.Combine(rootPath, source.FilePath)

            let isInRightVersion = 
                if not <| File.Exists destination then false else
                if not <| versionFile.Exists then false else
                source.Commit = File.ReadAllText(versionFile.FullName) 

            if isInRightVersion then
                verbosefn "Sourcefile %s is already there." (source.ToString())
                return None
            else
                tracefn "Downloading %s to %s" (source.ToString()) destination
                let! file = GitHub.downloadSourceFile source
                Directory.CreateDirectory(destination |> Path.GetDirectoryName) |> ignore
                File.WriteAllText(destination, file)
                File.WriteAllText(versionFile.FullName, source.Commit)

                return None
        }) sourceFiles

let private findPackagesWithContent (usedPackages:Dictionary<_,_>) = 
    usedPackages
    |> Seq.map (fun kv -> DirectoryInfo(Path.Combine("packages", kv.Key)))
    |> Seq.choose (fun packageDir -> packageDir.GetDirectories("Content") |> Array.tryFind (fun _ -> true))
    |> Seq.toList

let private copyContentFiles (project : ProjectFile, packagesWithContent) = 

    let onBlackList (fi : FileInfo) = 
        let rules : list<(FileInfo -> bool)> = [
            fun f -> f.Name = "_._"
            fun f -> f.Name.EndsWith(".transform")
            fun f -> f.Name.EndsWith(".pp")
            fun f -> f.Name.EndsWith(".tt")
            fun f -> f.Name.EndsWith(".ttinclude")
        ]
        rules
        |> List.exists (fun rule -> rule(fi))

    let rec copyDirContents (fromDir : DirectoryInfo, toDir : Lazy<DirectoryInfo>) =
        fromDir.GetDirectories() |> Array.toList
        |> List.collect (fun subDir -> copyDirContents(subDir, lazy toDir.Force().CreateSubdirectory(subDir.Name)))
        |> List.append
            (fromDir.GetFiles() 
                |> Array.toList
                |> List.filter (onBlackList >> not)
                |> List.map (fun file -> file.CopyTo(Path.Combine(toDir.Force().FullName, file.Name), true)))

    packagesWithContent
    |> List.collect (fun packageDir -> copyDirContents (packageDir, lazy (DirectoryInfo(Path.GetDirectoryName(project.FileName)))))

let private removeContentFiles (project: ProjectFile) =
    let removeFilesAndTrimDirs (files: FileInfo list) =
        for f in files do 
            if f.Exists then 
                f.Delete()

        let dirsPathsDeepestFirst = 
            files
            |> Seq.map (fun f -> f.Directory.FullName)
            |> Seq.distinct
            |> List.ofSeq
            |> List.rev
        
        for dirPath in dirsPathsDeepestFirst do
            let dir = DirectoryInfo dirPath
            if dir.Exists && dir.EnumerateFileSystemInfos() |> Seq.isEmpty then
               dir.Delete()

    project.GetContentFiles() 
    |> removeFilesAndTrimDirs

let extractReferencesFromListFile projectFile = 
    match ProjectFile.FindReferencesFile <| FileInfo(projectFile) with 
    | Some file -> File.ReadAllLines file
    | None -> [||]
    |> Array.map (fun s -> s.Trim())
    |> Array.filter (fun s -> System.String.IsNullOrWhiteSpace s |> not)


/// Installs the given packageFile.
let Install(sources,force, hard, lockFile:LockFile) = 
    let extractedPackages = 
        ExtractPackages(sources,force, lockFile.ResolvedPackages)
        |> Seq.append (DownloadSourceFiles(Path.GetDirectoryName lockFile.FileName, lockFile.SourceFiles))
        |> Async.Parallel
        |> Async.RunSynchronously
        |> Array.choose id

    for proj in ProjectFile.FindAllProjects(".") do    
        verbosefn "Installing to %s" proj.FullName
        let directPackages = extractReferencesFromListFile proj.FullName
        let project = ProjectFile.Load proj.FullName

        if directPackages |> Array.isEmpty |> not then verbosefn "  - direct packages: %A" directPackages
        let usedPackages = new Dictionary<_,_>()
        let usedSourceFiles = new HashSet<_>()

        let allPackages =
            extractedPackages
            |> Array.map (fun (p,_) -> p.Name.ToLower(),p)
            |> Map.ofArray

        let rec addPackage directly (name:string) =
            let identity = name.ToLower()
            if identity.StartsWith "file:" then
                let sourceFile = name.Split(':').[1]
                usedSourceFiles.Add sourceFile |> ignore
            else
                match allPackages |> Map.tryFind identity with
                | Some package ->
                    match usedPackages.TryGetValue name with
                    | false,_ ->
                        usedPackages.Add(name,directly)
                        if not lockFile.Options.Strict then
                            for d,_ in package.Dependencies do
                                addPackage false d
                    | true,v -> usedPackages.[name] <- v || directly
                | None -> failwithf "Project %s references package %s, but it was not found in the paket.lock file." proj.FullName name

        directPackages
        |> Array.iter (addPackage true)

        project.UpdateReferences(extractedPackages,usedPackages,hard)

        lockFile.SourceFiles 
        |> List.filter (fun file -> usedSourceFiles.Contains(file.Name))
        |> project.UpdateSourceFiles

        removeContentFiles project
        project.DeletePaketNodes("Content")
        
        if not lockFile.Options.OmitContent then
            let packagesWithContent = findPackagesWithContent usedPackages
            let contentFiles = copyContentFiles(project, packagesWithContent)
            project.UpdateContentFiles(contentFiles, hard)

        project.Save()
