/// Contains methods for the install process.
module Paket.InstallProcess

open Paket
open Paket.Logging
open Paket.ModuleResolver
open Paket.PackageResolver
open System.IO
open System.Collections.Generic
open Paket.PackageSources

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
    |> List.filter (fun fi -> not <| fi.FullName.Contains("paket-files"))
    |> removeFilesAndTrimDirs

let CreateInstallModel(sources, force, package) = 
    async { 
        let! (package, files) = ExtractPackage(sources, force, package)
        let nuspec = FileInfo(sprintf "./packages/%s/%s.nuspec" package.Name package.Name)
        let references = Nuspec.GetReferences nuspec.FullName
        let files = files |> Seq.map (fun fi -> fi.FullName)
        return Some(package, InstallModel.CreateFromLibs(package.Name, package.Version, files, references))
    }

/// Installs the given packageFile.
let Install(sources,force, hard, lockFile:LockFile) = 
    let extractedPackages = 
        lockFile.ResolvedPackages
        |> Seq.map (fun kv -> CreateInstallModel(sources,force,kv.Value))
        |> Seq.append (DownloadSourceFiles(Path.GetDirectoryName lockFile.FileName, lockFile.SourceFiles))
        |> Async.Parallel
        |> Async.RunSynchronously
        |> Array.choose id 

    let applicableProjects =
        ProjectFile.FindAllProjects(".") 
        |> List.choose (fun p -> ProjectFile.FindReferencesFile (FileInfo(p.FileName))
                                 |> Option.map (fun r -> p, ReferencesFile.FromFile(r)))

    for project,referenceFile in applicableProjects do    
        verbosefn "Installing to %s" project.FileName

        let usedPackages = new Dictionary<_,_>()

        let allPackages =
            extractedPackages
            |> Array.map (fun (p,_) -> p.Name.ToLower(),p)
            |> Map.ofArray

        let rec addPackage directly (name:string) =
            let identity = name.ToLower()
            match allPackages |> Map.tryFind identity with
            | Some package ->
                match usedPackages.TryGetValue name with
                | false,_ ->
                    usedPackages.Add(name,directly)
                    if not lockFile.Options.Strict then
                        for d,_ in package.Dependencies do
                            addPackage false d
                | true,v -> usedPackages.[name] <- v || directly
            | None -> failwithf "Project %s references package %s, but it was not found in the paket.lock file." project.FileName name

        referenceFile.NugetPackages
        |> List.iter (addPackage true)

        project.UpdateReferences(extractedPackages,usedPackages,hard)
        
        removeCopiedFiles project

        let getGitHubFilePath name = 
            (lockFile.SourceFiles |> List.find (fun f -> Path.GetFileName(f.Name) = name)).FilePath

        let gitHubFileItems =
            referenceFile.GitHubFiles
            |> List.map (fun file -> 
                             { BuildAction = project.DetermineBuildAction file.Name 
                               Include = createRelativePath project.FileName (getGitHubFilePath file.Name)
                               Link = Some(if file.Link = "." then Path.GetFileName(file.Name)
                                           else Path.Combine(file.Link, Path.GetFileName(file.Name))) })
        
        let nuGetFileItems =
            if not lockFile.Options.OmitContent 
            then
                let files = copyContentFiles(project, findPackagesWithContent usedPackages)
                files |> List.map (fun file -> 
                                       { BuildAction = project.DetermineBuildAction file.Name
                                         Include = createRelativePath project.FileName file.FullName
                                         Link = None })
            else []

        project.UpdateFileItems(gitHubFileItems @ nuGetFileItems, hard)

        project.Save()
