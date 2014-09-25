/// Contains methods for the install process.
module Paket.InstallProcess

open Paket
open Paket.Logging
open System.IO
open System.Collections.Generic

/// Downloads and extracts all packages.
let ExtractPackages(force, packages:PackageResolution) = 
    packages
    |> Seq.map (fun kv -> 
        async { 
            let package = kv.Value
            match package.Source with
            | Nuget source -> 
                let! packageFile = Nuget.DownloadPackage(source.Auth, source.Url, package.Name, package.Version.ToString(), force)
                let! folder = Nuget.CopyFromCache(packageFile, package.Name, package.Version.ToString(), force)
                return Some(package, Nuget.GetLibraries folder)
            | LocalNuget path -> 
                let packageFile = Path.Combine(path, sprintf "%s.%s.nupkg" package.Name (package.Version.ToString()))
                let! folder = Nuget.CopyFromCache(packageFile, package.Name, package.Version.ToString(), force)
                return Some(package, Nuget.GetLibraries folder)
        })

let DownloadSourceFiles(rootPath,sourceFiles) = 
    Seq.map (fun (source : SourceFile) -> 
        async { 
            let destination = Path.Combine(rootPath, source.FilePath)
            if File.Exists destination then
                return None
            else
                tracefn "Downloading %s..." (source.ToString())
                let! file = GitHub.downloadFile source
                Directory.CreateDirectory(destination |> Path.GetDirectoryName) |> ignore
                File.WriteAllText(destination, file)
                return None
        }) sourceFiles

let private findPackagesWithContent (usedPackages:Dictionary<_,_>) = 
    usedPackages
    |> Seq.map (fun kv -> DirectoryInfo(Path.Combine("packages", kv.Key)))
    |> Seq.choose (fun packageDir -> packageDir.GetDirectories("Content") |> Array.tryFind (fun _ -> true))
    |> Seq.toList

let private copyContentFilesToProject (project : ProjectFile) packagesWithContent = 

    let rec copyDirContents (fromDir : DirectoryInfo, toDir : DirectoryInfo) =
        fromDir.GetDirectories() |> Array.toList
        |> List.collect (fun subDir -> copyDirContents(subDir, toDir.CreateSubdirectory(subDir.Name)))
        |> List.append
            (fromDir.GetFiles() 
                |> Array.toList
                |> List.map (fun file -> file.CopyTo(Path.Combine(toDir.FullName, file.Name), true)))

    packagesWithContent
    |> List.collect (fun packageDir -> copyDirContents (packageDir, (DirectoryInfo(Path.GetDirectoryName(project.FileName)))))

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
let Install(force, hard, lockFile:LockFile) = 
    let extractedPackages = 
        ExtractPackages(force, lockFile.ResolvedPackages)
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
                        if not lockFile.Strict then
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
        let packagesWithContent = findPackagesWithContent usedPackages
        let contentFiles = copyContentFilesToProject project packagesWithContent
        project.UpdateContentFiles(contentFiles)

        project.Save()
