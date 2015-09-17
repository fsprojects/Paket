/// Contains methods for the install process.
module Paket.InstallProcess

open Paket
open Chessie.ErrorHandling
open Paket.Domain
open Paket.Logging
open Paket.BindingRedirects
open Paket.ModuleResolver
open Paket.PackageResolver
open System.IO
open FSharp.Polyfill
open System.Reflection
open Paket.PackagesConfigFile
open Paket.Requirements

let updatePackagesConfigFile (model: Map<GroupName*PackageName,SemVerInfo*InstallSettings>) packagesConfigFileName =
    let packagesInConfigFile = PackagesConfigFile.Read packagesConfigFileName

    let packagesInModel =
        model
        |> Seq.filter (fun kv -> defaultArg (snd kv.Value).IncludeVersionInPath false)
        |> Seq.map (fun kv ->
            let settings,version = kv.Value
            { Id = (snd kv.Key).ToString()
              Version = fst kv.Value
              TargetFramework = None })
        |> Seq.toList

    if packagesInModel <> [] then
        packagesInConfigFile
        |> Seq.filter (fun p -> packagesInModel |> Seq.exists (fun p' -> p'.Id = p.Id) |> not)
        |> Seq.append packagesInModel
        |> PackagesConfigFile.Save packagesConfigFileName

let findPackageFolder root (groupName,PackageName name) (version,settings) =
    let includeVersionInPath = defaultArg settings.IncludeVersionInPath false
    let lowerName = (name + if includeVersionInPath then "." + version.ToString() else "").ToLower()
    let di = DirectoryInfo(Path.Combine(root, Constants.PackagesFolderName))
    let targetFolder = getTargetFolder root groupName name version includeVersionInPath
    let direct = DirectoryInfo(targetFolder)
    if direct.Exists then direct else
    match di.GetDirectories() |> Seq.tryFind (fun subDir -> subDir.FullName.ToLower().EndsWith(lowerName)) with
    | Some x -> x
    | None -> failwithf "Package directory for package %s was not found." name

let private findPackagesWithContent (root,usedPackages:Map<GroupName*PackageName,SemVerInfo*InstallSettings>) =
    usedPackages
    |> Seq.filter (fun kv -> defaultArg (snd kv.Value).OmitContent false |> not)
    |> Seq.map (fun kv -> findPackageFolder root kv.Key kv.Value)
    |> Seq.choose (fun packageDir ->
            packageDir.GetDirectories("Content")
            |> Array.append (packageDir.GetDirectories("content"))
            |> Array.tryFind (fun _ -> true))
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
            |> List.map (fun f -> f.Directory.FullName)
            |> List.distinct
            |> List.rev

        for dirPath in dirsPathsDeepestFirst do
            removeEmptyDirHierarchy (DirectoryInfo dirPath)

    project.GetPaketFileItems()
    |> List.filter (fun fi -> not <| fi.FullName.Contains(Constants.PaketFilesFolderName))
    |> removeFilesAndTrimDirs

let CreateInstallModel(root, groupName, sources, force, package) =
    async {
        let! (package, files, targetsFiles, analyzerFiles) = RestoreProcess.ExtractPackage(root, groupName, sources, force, package)
        let (PackageName name) = package.Name
        let nuspec = Nuspec.Load(root,groupName,package.Version,defaultArg package.Settings.IncludeVersionInPath false,package.Name)
        let files = files |> Array.map (fun fi -> fi.FullName)
        let targetsFiles = targetsFiles |> Array.map (fun fi -> fi.FullName)
        let analyzerFiles = analyzerFiles |> Array.map (fun fi -> fi.FullName)
        return (groupName,package.Name), (package,InstallModel.CreateFromLibs(package.Name, package.Version, package.Settings.FrameworkRestrictions, files, targetsFiles, analyzerFiles, nuspec))
    }

/// Restores the given packages from the lock file.
let createModel(root, force, dependenciesFile:DependenciesFile, lockFile : LockFile, packages:Set<GroupName*PackageName>) =
    let sourceFileDownloads = 
        [|for kv in lockFile.Groups -> RemoteDownload.DownloadSourceFiles(root, kv.Key, force, kv.Value.RemoteFiles) |]
        |> Async.Parallel

    let packageDownloads =
        lockFile.Groups
        |> Seq.map (fun kv' -> 
            let sources = dependenciesFile.Groups.[kv'.Key].Sources
            kv'.Value.Resolution
            |> Map.filter (fun name _ -> packages.Contains(kv'.Key,name))
            |> Seq.map (fun kv -> CreateInstallModel(root,kv'.Key,sources,force,kv.Value)))
        |> Seq.concat
        |> Seq.toArray
        |> Async.Parallel

    let _,extractedPackages =
        Async.Parallel(sourceFileDownloads,packageDownloads)
        |> Async.RunSynchronously

    extractedPackages

/// Applies binding redirects for all strong-named references to all app. and web. config files.
let private applyBindingRedirects createNewBindingFiles root (extractedPackages:seq<_*InstallModel>) =
    extractedPackages
    |> Seq.map (fun (package, model:InstallModel) -> model.GetLibReferencesLazy.Force())
    |> Set.unionMany
    |> Seq.choose(function | Reference.Library path -> Some path | _-> None)
    |> Seq.groupBy (fun p -> FileInfo(p).Name)
    |> Seq.choose(fun (_,librariesForPackage) ->
        librariesForPackage
        |> Seq.choose(fun library ->
            try
                let assembly = Assembly.ReflectionOnlyLoadFrom library
                assembly
                |> BindingRedirects.getPublicKeyToken
                |> Option.map(fun token -> assembly, token)
            with exn -> None)
        |> Seq.sortBy(fun (assembly,_) -> assembly.GetName().Version)
        |> Seq.toList
        |> List.rev
        |> function | head :: _ -> Some head | _ -> None)
    |> Seq.map(fun (assembly, token) ->
        { BindingRedirect.AssemblyName = assembly.GetName().Name
          Version = assembly.GetName().Version.ToString()
          PublicKeyToken = token
          Culture = None })
    |> applyBindingRedirectsToFolder createNewBindingFiles root

let findAllReferencesFiles root =
    root
    |> ProjectFile.FindAllProjects
    |> Array.choose (fun p -> ProjectFile.FindReferencesFile(FileInfo(p.FileName))
                                |> Option.map (fun r -> p, r))
    |> Array.map (fun (project,file) ->
        try
            ok <| (project, ReferencesFile.FromFile(file))
        with _ ->
            fail <| ReferencesFileParseError (FileInfo(file)))
    |> collect

/// Installs all packages from the lock file.
let InstallIntoProjects(options : InstallerOptions, dependenciesFile, lockFile : LockFile, projects : (ProjectFile * ReferencesFile) list) =    
    let packagesToInstall =
        if options.OnlyReferenced then
            projects
            |> List.map (fun (_, referencesFile)->
                referencesFile
                |> lockFile.GetPackageHull
                |> Seq.map (fun p -> fst p.Key, snd p.Key))
            |> Seq.concat
        else
            lockFile.GetGroupedResolution()
            |> Seq.map (fun kv -> kv.Key)

    let root = Path.GetDirectoryName lockFile.FileName
    let model = createModel(root, options.Force, dependenciesFile, lockFile, Set.ofSeq packagesToInstall) |> Map.ofArray
    let lookup = lockFile.GetDependencyLookupTable()

    for project : ProjectFile, referenceFile in projects do
        verbosefn "Installing to %s" project.FileName
        
        let usedPackages =
            referenceFile.Groups
            |> Seq.map (fun kv ->
                kv.Value.NugetPackages
                |> Seq.map (fun ps ->
                    let group = 
                        match lockFile.Groups |> Map.tryFind kv.Key with
                        | Some g -> g
                        | None -> failwithf "%s uses the group %O, but this group was not found in paket.lock." referenceFile.FileName kv.Key

                    let package = 
                        match model |> Map.tryFind (kv.Key, ps.Name) with
                        | Some (p,_) -> p
                        | None -> failwithf "%s uses NuGet package %O, but it was not found in the paket.lock file in group %O." referenceFile.FileName ps.Name kv.Key

                    let resolvedSettings = 
                        [package.Settings; group.Options.Settings] 
                        |> List.fold (+) ps.Settings
                    (kv.Key,ps.Name), (package.Version,resolvedSettings)))
            |> Seq.concat
            |> Map.ofSeq

        let usedPackages =
            let d = ref usedPackages

            /// we want to treat the settings from the references file through the computation so that it can be used as the base that 
            /// the other settings modify. In this way we ensure that references files can override the dependencies file, which in turn overrides the lockfile.
            let usedPackageDependencies = 
                usedPackages 
                |> Seq.collect (fun u -> lookup.[u.Key] |> Seq.map (fun i -> fst u.Key, u.Value, i))
                |> Seq.choose (fun (groupName,(_,parentSettings), dep) -> 
                    let group = 
                        match lockFile.Groups |> Map.tryFind groupName with
                        | Some g -> g
                        | None -> failwithf "%s uses the group %O, but this group was not found in paket.lock." referenceFile.FileName groupName

                    match group.Resolution |> Map.tryFind dep with
                    | None -> None
                    | Some p -> 
                        let resolvedSettings = 
                            [p.Settings; group.Options.Settings] 
                            |> List.fold (+) parentSettings
                        Some ((groupName,p.Name), (p.Version,resolvedSettings)) )

            for key,settings in usedPackageDependencies do
                if (!d).ContainsKey key |> not then
                  d := Map.add key settings !d

            !d

        project.UpdateReferences(model, usedPackages, options.Hard)
        
        Path.Combine(FileInfo(project.FileName).Directory.FullName, Constants.PackagesConfigFile)
        |> updatePackagesConfigFile usedPackages 

        removeCopiedFiles project

        let gitRemoteItems =
            referenceFile.Groups
            |> Seq.map (fun kv ->
                kv.Value.RemoteFiles
                |> List.map (fun file ->
                    let link = if file.Link = "." then Path.GetFileName file.Name else Path.Combine(file.Link, Path.GetFileName file.Name)
                    let remoteFilePath = 
                        if verbose then
                            tracefn "FileName: %s " file.Name 
    
                        let lockFileReference =
                            match lockFile.Groups |> Map.tryFind kv.Key with
                            | None -> None
                            | Some group ->
                                group.RemoteFiles
                                |> Seq.tryFind (fun f -> Path.GetFileName(f.Name) = file.Name)
    
                        match lockFileReference with
                        | Some file -> file.FilePath(root,kv.Key)
                        | None -> failwithf "%s references file %s in group %O, but it was not found in the paket.lock file." referenceFile.FileName file.Name kv.Key
    
                    let linked = defaultArg file.Settings.Link true
    
                    if linked then
                        { BuildAction = project.DetermineBuildAction file.Name
                          Include = createRelativePath project.FileName remoteFilePath
                          Link = Some link }
                    else
                        let toDir = Path.GetDirectoryName(project.FileName)
                        let targetFile = FileInfo(Path.Combine(toDir,link))
                        if targetFile.Directory.Exists |> not then
                            targetFile.Directory.Create()
    
                        File.Copy(remoteFilePath,targetFile.FullName)
    
                        { BuildAction = project.DetermineBuildAction file.Name
                          Include = createRelativePath project.FileName targetFile.FullName
                          Link = None }))
            |> List.concat

        let nuGetFileItems =
            copyContentFiles(project, findPackagesWithContent(root,usedPackages))
            |> List.map (fun file ->
                                { BuildAction = project.DetermineBuildAction file.Name
                                  Include = createRelativePath project.FileName file.FullName
                                  Link = None })

        project.UpdateFileItems(gitRemoteItems @ nuGetFileItems, options.Hard)

        project.Save()

    for g in lockFile.Groups do
        let group = g.Value   
        model
        |> Seq.filter (fun kv -> 
            let packageName = snd kv.Key
            let packageRedirects =
                match group.Resolution |> Map.tryFind packageName with
                | None -> None
                | Some p -> p.Settings.CreateBindingRedirects

            let isEnabled = options.Redirects || g.Value.Options.Redirects || defaultArg packageRedirects false
            isEnabled && (fst kv.Key) = g.Key)
        |> Seq.map (fun kv -> kv.Value)        
        |> applyBindingRedirects options.CreateNewBindingFiles root 

/// Installs all packages from the lock file.
let Install(options : InstallerOptions, dependenciesFile, lockFile : LockFile) =
    let root = FileInfo(lockFile.FileName).Directory.FullName
    let projects = findAllReferencesFiles root |> returnOrFail
    InstallIntoProjects(options, dependenciesFile, lockFile, projects)
