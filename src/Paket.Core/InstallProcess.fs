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
open System.Collections.Generic
open System.Collections.Concurrent

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

let findPackageFolder root (groupName,packageName) (version,settings) =
    let includeVersionInPath = defaultArg settings.IncludeVersionInPath false
    let lowerName = (packageName.ToString() + if includeVersionInPath then "." + version.ToString() else "").ToLower()
    let di = DirectoryInfo(Path.Combine(root, Constants.PackagesFolderName))
    let targetFolder = getTargetFolder root groupName packageName version includeVersionInPath
    let direct = DirectoryInfo targetFolder
    if direct.Exists then direct else
    match di.GetDirectories() |> Seq.tryFind (fun subDir -> String.endsWithIgnoreCase lowerName subDir.FullName) with
    | Some x -> x
    | None -> failwithf "Package directory for package %O was not found." packageName


let contentFileBlackList : list<(FileInfo -> bool)> = [
    fun f -> f.Name = "_._"
    fun f -> f.Name.EndsWith ".transform"
    fun f -> f.Name.EndsWith ".pp"
    fun f -> f.Name.EndsWith ".tt"
    fun f -> f.Name.EndsWith ".ttinclude"
]

let processContentFiles root project (usedPackages:Map<_,_>) gitRemoteItems options =
    let contentFiles = System.Collections.Generic.HashSet<_>()
    let nuGetFileItems =
        let packageDirectoriesWithContent =
            usedPackages
            |> Seq.map (fun kv -> kv.Key,kv.Value,defaultArg (snd kv.Value).OmitContent ContentCopySettings.Overwrite)
            |> Seq.filter (fun (_,_,setting) -> setting <> ContentCopySettings.Omit)
            |> Seq.map (fun (key,v,s) -> s,findPackageFolder root key v)
            |> Seq.choose (fun (settings,packageDir) ->
                packageDir.GetDirectories "Content"
                |> Array.append (packageDir.GetDirectories "content")
                |> Array.tryFind (fun _ -> true)
                |> Option.map (fun x -> x,settings))
            |> Seq.toList

        let copyContentFiles (project : ProjectFile, packagesWithContent) =
            let onBlackList (fi : FileInfo) = contentFileBlackList |> List.exists (fun rule -> rule(fi))

            let rec copyDirContents (fromDir : DirectoryInfo, settings, toDir : Lazy<DirectoryInfo>) =
                fromDir.GetDirectories() |> Array.toList
                |> List.collect (fun subDir -> copyDirContents(subDir, settings, lazy toDir.Force().CreateSubdirectory(subDir.Name)))
                |> List.append
                    (fromDir.GetFiles()
                        |> Array.toList
                        |> List.filter (fun file ->
                            if onBlackList file then false else
                            if file.Name = "paket.references" then traceWarnfn "You can't use paket.references as a content file in the root of a project. Please take a look at %s" file.FullName; false else true)
                        |> List.map (fun file ->
                            let overwrite = settings = ContentCopySettings.Overwrite
                            let target = FileInfo(Path.Combine(toDir.Force().FullName, file.Name))
                            contentFiles.Add(target.FullName) |> ignore
                            if overwrite || not target.Exists then
                                file.CopyTo(target.FullName, true)
                            else target))

            packagesWithContent
            |> List.collect (fun (packageDir,settings) -> 
                copyDirContents (packageDir, settings, lazy (DirectoryInfo(Path.GetDirectoryName(project.FileName)))))

        copyContentFiles(project, packageDirectoriesWithContent)
        |> List.map (fun file ->
                            { BuildAction = project.DetermineBuildAction file.Name
                              Include = createRelativePath project.FileName file.FullName
                              Link = None })

    let removeCopiedFiles (project: ProjectFile) =
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
        |> List.filter (fun fi -> not <| fi.FullName.Contains(Constants.PaketFilesFolderName) && not (contentFiles.Contains(fi.FullName)) && fi.Name <> "paket.references")
        |> removeFilesAndTrimDirs

    removeCopiedFiles project

    project.UpdateFileItems(gitRemoteItems @ nuGetFileItems, options.Hard)


let CreateInstallModel(root, groupName, sources, force, package) =
    async {
        let! (package, files, targetsFiles, analyzerFiles) = RestoreProcess.ExtractPackage(root, groupName, sources, force, package)
        let nuspec = Nuspec.Load(root,groupName,package.Version,defaultArg package.Settings.IncludeVersionInPath false,package.Name)
        let files = files |> Array.map (fun fi -> fi.FullName)
        let targetsFiles = targetsFiles |> Array.map (fun fi -> fi.FullName)
        let analyzerFiles = analyzerFiles |> Array.map (fun fi -> fi.FullName)
        let model = InstallModel.CreateFromLibs(package.Name, package.Version, package.Settings.FrameworkRestrictions |> getRestrictionList, files, targetsFiles, analyzerFiles, nuspec)
        return (groupName,package.Name), (package,model)
    }

/// Restores the given packages from the lock file.
let CreateModel(root, force, dependenciesFile:DependenciesFile, lockFile : LockFile, packages:Set<GroupName*PackageName>, updatedGroups:Map<_,_>) =
    [|for kv in lockFile.Groups do
            let files = if updatedGroups |> Map.containsKey kv.Key then [] else kv.Value.RemoteFiles
            if List.isEmpty files |> not then
                yield RemoteDownload.DownloadSourceFiles(root, kv.Key, force, files) |]
    |> Async.Parallel
    |> Async.RunSynchronously
    |> ignore

    lockFile.Groups
    |> Seq.map (fun kv' -> 
        let sources = dependenciesFile.Groups.[kv'.Key].Sources
        kv'.Value.Resolution
        |> Map.filter (fun name _ -> packages.Contains(kv'.Key,name))
        |> Seq.map (fun kv -> CreateInstallModel(root,kv'.Key,sources,force,kv.Value))
        |> Seq.toArray
        |> Async.Parallel
        |> Async.RunSynchronously)
    |> Seq.concat
    |> Seq.toArray


/// Applies binding redirects for all strong-named references to all app. and web.config files.
let private applyBindingRedirects (loadedLibs:Dictionary<_,_>) isFirstGroup createNewBindingFiles cleanBindingRedirects redirects root groupName findDependencies extractedPackages =
    let dependencyGraph = ConcurrentDictionary<_,Set<_>>()
    let projects = ConcurrentDictionary<_,ProjectFile option>();
    let referenceFiles = ConcurrentDictionary<_,ReferencesFile option>();
    let referenceFile (projectFile : ProjectFile) =
        let referenceFile (projectFile : ProjectFile) =
            ProjectFile.FindReferencesFile (FileInfo projectFile.FileName)
            |> Option.map ReferencesFile.FromFile
        referenceFiles.GetOrAdd(projectFile, referenceFile)

    let rec dependencies (projectFile : ProjectFile) =
        match referenceFile projectFile with
        | Some referenceFile -> 
            projectFile.GetInterProjectDependencies()
            |> Seq.map (fun r -> projects.GetOrAdd(r.Path, ProjectFile.TryLoad))
            |> Seq.choose id
            |> Seq.collect (fun p -> dependencyGraph.GetOrAdd(p, dependencies))
            |> Seq.append (
                referenceFile.Groups
                |> Seq.filter (fun g -> g.Key = groupName)
                |> Seq.collect (fun g -> g.Value.NugetPackages |> List.map (fun p -> (groupName,p.Name)))
                |> Seq.collect findDependencies)
            |> Set.ofSeq
        | None -> Set.empty

    let bindingRedirects (projectFile : ProjectFile) =
        let referenceFile = referenceFile projectFile
        let dependencies = dependencyGraph.GetOrAdd(projectFile, dependencies)
        let redirectsFromReference packageName =
            referenceFile
            |> Option.bind (fun r ->
                r.Groups
                |> Seq.filter (fun g -> g.Key = groupName)
                |> Seq.collect (fun g -> g.Value.NugetPackages)
                |> Seq.tryFind (fun p -> p.Name = packageName)
                |> Option.bind (fun p -> p.Settings.CreateBindingRedirects))

        let assemblies =
            extractedPackages
            |> Seq.map (fun (model,redirects) -> (model, redirectsFromReference model.PackageName |> Option.fold (fun _ x -> Some x) redirects))
            |> Seq.filter (fun (model,_) -> dependencies |> Set.contains model.PackageName)
            |> Seq.collect (fun (model,redirects) -> model.GetLibReferences(projectFile.GetTargetProfile()) |> Seq.map (fun lib -> lib,redirects))
            |> Seq.groupBy (fun (p,_) -> FileInfo(p).Name)
            |> Seq.choose(fun (_,librariesForPackage) ->
                librariesForPackage
                |> Seq.choose(fun (library,redirects) ->
                    try
                        let key = FileInfo(library).FullName.ToLowerInvariant()
                        let assembly = 
                            match loadedLibs.TryGetValue key with
                            | true,v -> v
                            | _ -> 
                                let v = Assembly.ReflectionOnlyLoadFrom library
                                loadedLibs.Add(key,v)
                                v

                        Some (assembly, BindingRedirects.getPublicKeyToken assembly, assembly.GetReferencedAssemblies(), redirects)
                    with exn -> None)
                |> Seq.sortBy(fun (assembly,_,_,_) -> assembly.GetName().Version)
                |> Seq.toList
                |> List.rev
                |> function | head :: _ -> Some head | _ -> None)
            |> Seq.cache

        assemblies
        |> Seq.choose (fun (assembly,token,refs,redirects) -> token |> Option.map (fun token -> (assembly,token,refs,redirects)))
        |> Seq.filter (fun (_,_,_,packageRedirects) -> defaultArg ((packageRedirects |> Option.map ((<>) Off)) ++ redirects) false)
        |> Seq.filter (fun (assembly,_,refs,redirects) -> 
            redirects = Some Force
            || assemblies
            |> Seq.collect (fun (_,_,refs,_) -> refs)
            |> Seq.filter (fun a -> assembly.GetName().Name = a.Name)
            |> Seq.exists (fun a -> assembly.GetName().Version > a.Version))
        |> Seq.map(fun (assembly, token,_,_) ->
            { BindingRedirect.AssemblyName = assembly.GetName().Name
              Version = assembly.GetName().Version.ToString()
              PublicKeyToken = token
              Culture = None })
        |> Seq.sort

    if redirects <> Some false then
        applyBindingRedirectsToFolder isFirstGroup createNewBindingFiles cleanBindingRedirects root bindingRedirects

let findAllReferencesFiles root =
    root
    |> ProjectFile.FindAllProjects
    |> Array.map (fun p ->
        match ProjectFile.FindReferencesFile(FileInfo p.FileName) with
        | Some fileName -> 
                try
                    ok <| (p, ReferencesFile.FromFile fileName)
                with _ ->
                    fail <| ReferencesFileParseError (FileInfo fileName)
        | None ->
            let fileName = 
                let fi = FileInfo(p.FileName)
                Path.Combine(fi.Directory.FullName,Constants.ReferencesFile)

            ok <| (p, ReferencesFile.New fileName))
     |> collect

/// Installs all packages from the lock file.
let InstallIntoProjects(options : InstallerOptions, dependenciesFile, lockFile : LockFile, projectsAndReferences : (ProjectFile * ReferencesFile) list, updatedGroups) =
    let packagesToInstall =
        if options.OnlyReferenced then
            projectsAndReferences
            |> List.map (fun (_, referencesFile)->
                referencesFile
                |> lockFile.GetPackageHull
                |> Seq.map (fun p -> p.Key))
            |> Seq.concat
        else
            lockFile.GetGroupedResolution()
            |> Seq.map (fun kv -> kv.Key)

    let root = Path.GetDirectoryName lockFile.FileName
    let model = CreateModel(root, options.Force, dependenciesFile, lockFile, Set.ofSeq packagesToInstall, updatedGroups) |> Map.ofArray
    let lookup = lockFile.GetDependencyLookupTable()

    for project, referenceFile in projectsAndReferences do
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
  
                    let buildAction = project.DetermineBuildActionForRemoteItems file.Name
                    if buildAction <> BuildAction.Reference && linked then
                        { BuildAction = buildAction
                          Include = createRelativePath project.FileName remoteFilePath
                          Link = Some link }
                    else
                        { BuildAction = buildAction
                          Include =
                            if buildAction = BuildAction.Reference then
                                 createRelativePath project.FileName remoteFilePath
                            else
                                let toDir = Path.GetDirectoryName(project.FileName)
                                let targetFile = FileInfo(Path.Combine(toDir,link))
                                if targetFile.Directory.Exists |> not then
                                    targetFile.Directory.Create()
    
                                File.Copy(remoteFilePath,targetFile.FullName)
                                createRelativePath project.FileName targetFile.FullName
                          Link = None }))
            |> List.concat

        processContentFiles root project usedPackages gitRemoteItems options
        project.Save()
        let loadedLibs = new Dictionary<_,_>()

        let first = ref true

        let redirects =
            match options.Redirects with
            | true -> Some true
            | false -> None

        for g in lockFile.Groups do
            let group = g.Value
            model
            |> Seq.filter (fun kv -> (fst kv.Key) = g.Key)
            |> Seq.map (fun kv ->
                let packageRedirects =
                    group.Resolution
                    |> Map.tryFind (snd kv.Key)
                    |> Option.bind (fun p -> p.Settings.CreateBindingRedirects)

                (snd kv.Value,packageRedirects))
            |> applyBindingRedirects loadedLibs !first options.CreateNewBindingFiles options.Hard (g.Value.Options.Redirects ++ redirects) (FileInfo project.FileName).Directory.FullName g.Key lockFile.GetAllDependenciesOf
            first := false

/// Installs all packages from the lock file.
let Install(options : InstallerOptions, dependenciesFile, lockFile : LockFile, updatedGroups) =
    let root = FileInfo(lockFile.FileName).Directory.FullName
    let projects = findAllReferencesFiles root |> returnOrFail
    InstallIntoProjects(options, dependenciesFile, lockFile, projects, updatedGroups)
