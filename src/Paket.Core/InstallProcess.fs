/// Contains methods for the install process.
module Paket.InstallProcess

open Paket
open Paket.Domain
open Paket.Logging
open Paket.ModuleResolver
open Paket.PackageResolver
open System.IO
open System.Collections.Generic
open FSharp.Polyfill

let private findPackagesWithContent (root,usedPackages:Dictionary<_,_>) = 
    usedPackages
    |> Seq.map (fun kv -> DirectoryInfo(Path.Combine(root, "packages", (|PackageName|) kv.Key)))
    |> Seq.choose (fun packageDir -> packageDir.GetDirectories("Content") |> Array.tryFind (fun _ -> true))
    |> Seq.toList

let private copyContentFiles (project : ProjectFile, packagesWithContent) = 

    let rules : list<(FileInfo -> bool)> = [
            fun f -> f.Name = "_._"
            fun f -> f.Name.EndsWith(".transform")
            fun f -> f.Name.EndsWith(".pp")
            fun f -> f.Name.EndsWith(".tt")
            fun f -> f.Name.EndsWith(".ttinclude")
        ]

    let onBlackList (fi : FileInfo) = rules |> List.exists (fun rule -> rule(fi))

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

let private removeCopiedFiles (project: ProjectFile) =
    let rec removeEmptyDirHierarchy (dir : DirectoryInfo) =
        if dir.Exists && dir.EnumerateFileSystemInfos() |> Seq.isEmpty then
            dir.Delete()
            removeEmptyDirHierarchy dir.Parent

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
            removeEmptyDirHierarchy (DirectoryInfo dirPath)

    project.GetPaketFileItems() 
    |> List.filter (fun fi -> not <| fi.FullName.Contains(Constants.PaketFilesFolderName))
    |> removeFilesAndTrimDirs

let CreateInstallModel(root, sources, force, package) = 
    async { 
        let! (package, files) = RestoreProcess.ExtractPackage(root, sources, force, package)
        let (PackageName name) = package.Name
        let nuspec = FileInfo(sprintf "%s/packages/%s/%s.nuspec" root name name)
        let nuspec = Nuspec.Load nuspec.FullName
        let files = files |> Seq.map (fun fi -> fi.FullName)
        return package, InstallModel.CreateFromLibs(package.Name, package.Version, files, nuspec)
    }

/// Restores the given packages from the lock file.
let createModel(root, sources,force, lockFile:LockFile) = 
    let sourceFileDownloads =
        lockFile.SourceFiles
        |> Seq.map (fun file -> RemoteDownload.DownloadSourceFile(root, file))
        |> Async.Parallel

    let packageDownloads = 
        lockFile.ResolvedPackages
        |> Seq.map (fun kv -> CreateInstallModel(root,sources,force,kv.Value))
        |> Async.Parallel

    let _,extractedPackages =
        Async.Parallel(sourceFileDownloads,packageDownloads)
        |> Async.RunSynchronously

    extractedPackages

/// Installs the given all packages from the lock file.
let Install(sources,force, hard, lockFile:LockFile) = 
    let root = FileInfo(lockFile.FileName).Directory.FullName 
    let extractedPackages = createModel(root,sources,force, lockFile)

    let model =
        extractedPackages
        |> Array.map (fun (p,m) -> NormalizedPackageName p.Name,m)
        |> Map.ofArray

    let applicableProjects =
        root
        |> ProjectFile.FindAllProjects
        |> List.choose (fun p -> ProjectFile.FindReferencesFile(FileInfo(p.FileName))
                                 |> Option.map (fun r -> p, ReferencesFile.FromFile(r)))

    for project,referenceFile in applicableProjects do    
        verbosefn "Installing to %s" project.FileName

        let usedPackages = lockFile.GetPackageHull(referenceFile)

        project.UpdateReferences(model,usedPackages,hard)
        
        removeCopiedFiles project

        let getSingleRemoteFilePath name = 
            printf "\nFilename %s " name
            lockFile.SourceFiles |> List.iter (fun i -> printf "\n %s %s " i.Name  i.FilePath)
            (lockFile.SourceFiles |> List.find (fun f -> Path.GetFileName(f.Name) = name)).FilePath

        let gitRemoteItems =
            referenceFile.RemoteFiles
            |> List.map (fun file -> 
                             { BuildAction = project.DetermineBuildAction file.Name 
                               Include = createRelativePath project.FileName (getSingleRemoteFilePath file.Name)
                               Link = Some(if file.Link = "." then Path.GetFileName(file.Name)
                                           else Path.Combine(file.Link, Path.GetFileName(file.Name))) })
        
        let nuGetFileItems =
            if lockFile.Options.OmitContent then [] else
            let files = copyContentFiles(project, findPackagesWithContent(root,usedPackages))
            files |> List.map (fun file -> 
                                    { BuildAction = project.DetermineBuildAction file.Name
                                      Include = createRelativePath project.FileName file.FullName
                                      Link = None })

        project.UpdateFileItems(gitRemoteItems @ nuGetFileItems, hard)

        project.Save()
