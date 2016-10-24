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
open System.Reflection
open Paket.PackagesConfigFile
open Paket.Requirements
open System.Collections.Generic
open Xml
open System.Xml

let updatePackagesConfigFile (model: Map<GroupName*PackageName,SemVerInfo*InstallSettings>) packagesConfigFileName =
    let packagesInConfigFile = PackagesConfigFile.Read packagesConfigFileName

    let packagesInModel =
        model
        |> Seq.filter (fun kv -> defaultArg (snd kv.Value).IncludeVersionInPath false)
        |> Seq.map (fun kv ->
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
    let targetFolder = getTargetFolder root groupName packageName version includeVersionInPath
    let direct = DirectoryInfo targetFolder
    if direct.Exists then 
        direct 
    else
        let lowerName = packageName.ToString() + if includeVersionInPath then "." + version.ToString() else ""
        let di =
            if groupName = Constants.MainDependencyGroup then
                DirectoryInfo(Path.Combine(root, Constants.PackagesFolderName))
            else
                let groupName = groupName.GetCompareString()
                let di = DirectoryInfo(Path.Combine(root, Constants.PackagesFolderName, groupName))
                if di.Exists then di else

                match di.GetDirectories() |> Seq.tryFind (fun subDir -> String.endsWithIgnoreCase groupName subDir.FullName) with
                | Some x -> x
                | None ->
                    traceWarnfn "The following directories exists:" 
                    di.GetDirectories() |> Seq.iter (fun d -> traceWarnfn "  %s" d.FullName)

                    failwithf "Group directory for group %s was not found." groupName

        match di.GetDirectories() |> Seq.tryFind (fun subDir -> String.endsWithIgnoreCase lowerName subDir.FullName) with
        | Some x -> x
        | None ->
            traceWarnfn "The following directories exists:" 
            di.GetDirectories() |> Seq.iter (fun d -> traceWarnfn "  %s" d.FullName)

            failwithf "Package directory for package %O was not found." packageName


let contentFileBlackList : list<(FileInfo -> bool)> = [
    fun f -> f.Name = "_._"
    fun f -> f.Name.EndsWith ".transform"
    fun f -> f.Name.EndsWith ".pp"
    fun f -> f.Name.EndsWith ".tt"
    fun f -> f.Name.EndsWith ".ttinclude"
    fun f -> f.Name.EndsWith ".install.xdt"
    fun f -> f.Name.EndsWith ".uninstall.xdt"
]

let processContentFiles root project (usedPackages:Map<_,_>) gitRemoteItems options =
    let contentFiles = System.Collections.Generic.HashSet<_>()
    let nuGetFileItems =
        let packageDirectoriesWithContent =
            usedPackages
            |> Seq.map (fun kv -> 
                let contentCopySettings = defaultArg (snd kv.Value).OmitContent ContentCopySettings.Overwrite
                let contentCopyToOutputSettings = (snd kv.Value).CopyContentToOutputDirectory
                kv.Key,kv.Value,contentCopySettings,contentCopyToOutputSettings)
            |> Seq.filter (fun (_,_,contentCopySettings,_) -> contentCopySettings <> ContentCopySettings.Omit)
            |> Seq.map (fun (key,v,s,s') -> s,s',findPackageFolder root key v)
            |> Seq.choose (fun (contentCopySettings,contentCopyToOutputSettings,packageDir) ->
                packageDir.GetDirectories "Content"
                |> Array.append (packageDir.GetDirectories "content")
                |> Array.tryFind (fun _ -> true)
                |> Option.map (fun x -> x,contentCopySettings,contentCopyToOutputSettings))
            |> Seq.toList

        let copyContentFiles (project : ProjectFile, packagesWithContent) =
            let onBlackList (fi : FileInfo) = contentFileBlackList |> List.exists (fun rule -> rule(fi))

            let rec copyDirContents (fromDir : DirectoryInfo, contentCopySettings, toDir : Lazy<DirectoryInfo>) =
                fromDir.GetDirectories() |> Array.toList
                |> List.collect (fun subDir -> copyDirContents(subDir, contentCopySettings, lazy toDir.Force().CreateSubdirectory(subDir.Name)))
                |> List.append
                    (fromDir.GetFiles()
                        |> Array.toList
                        |> List.filter (fun file ->
                            if onBlackList file then false else
                            if file.Name = "paket.references" then traceWarnfn "You can't use paket.references as a content file in the root of a project. Please take a look at %s" file.FullName; false else true)
                        |> List.map (fun file ->
                            let overwrite = contentCopySettings = ContentCopySettings.Overwrite
                            let target = FileInfo(Path.Combine(toDir.Force().FullName, file.Name))
                            contentFiles.Add(target.FullName) |> ignore
                            if overwrite || not target.Exists then
                                file.CopyTo(target.FullName, true)
                            else target))
                

            packagesWithContent
            |> List.collect (fun (packageDir,contentCopySettings,contentCopyToOutputSettings) -> 
                copyDirContents (packageDir, contentCopySettings, lazy (DirectoryInfo(Path.GetDirectoryName(project.FileName))))
                |> List.map (fun x -> x,contentCopySettings,contentCopyToOutputSettings))

        copyContentFiles(project, packageDirectoriesWithContent)
        |> List.map (fun (file,contentCopySettings,contentCopyToOutputSettings) ->
                            let createSubNodes = contentCopySettings <> ContentCopySettings.OmitIfExisting
                            { BuildAction = project.DetermineBuildAction file.Name
                              Include = createRelativePath project.FileName file.FullName
                              WithPaketSubNode = createSubNodes
                              CopyToOutputDirectory = contentCopyToOutputSettings
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

    project.UpdateFileItems(gitRemoteItems @ nuGetFileItems)


let CreateInstallModel(root, groupName, sources, caches, force, package) =
    async {
        let! (package, files, targetsFiles, analyzerFiles) = RestoreProcess.ExtractPackage(root, groupName, sources, caches, force, package, false)
        let nuspec = Nuspec.Load(root,groupName,package.Version,defaultArg package.Settings.IncludeVersionInPath false,package.Name)
        let files = files |> Array.map (fun fi -> fi.FullName)
        let targetsFiles = targetsFiles |> Array.map (fun fi -> fi.FullName) |> Array.toList
        let analyzerFiles = analyzerFiles |> Array.map (fun fi -> fi.FullName)
        let model = InstallModel.CreateFromLibs(package.Name, package.Version, package.Settings.FrameworkRestrictions |> getRestrictionList, files, targetsFiles, analyzerFiles, nuspec)
        return (groupName,package.Name), (package,model)
    }

/// Restores the given packages from the lock file.
let CreateModel(root, force, dependenciesFile:DependenciesFile, lockFile : LockFile, packages:Set<GroupName*PackageName>, updatedGroups:Map<_,_>) =
    for kv in lockFile.Groups do
         let files = if updatedGroups |> Map.containsKey kv.Key then [] else kv.Value.RemoteFiles
         if List.isEmpty files |> not then
             RemoteDownload.DownloadSourceFiles(root, kv.Key, force, files)

    lockFile.Groups
    |> Seq.map (fun kv' -> 
        let sources = dependenciesFile.Groups.[kv'.Key].Sources
        let caches = dependenciesFile.Groups.[kv'.Key].Caches
        kv'.Value.Resolution
        |> Map.filter (fun name _ -> packages.Contains(kv'.Key,name))
        |> Seq.map (fun kv -> CreateInstallModel(root,kv'.Key,sources,caches,force,kv.Value))
        |> Seq.toArray
        |> Async.Parallel
        |> Async.RunSynchronously)
    |> Seq.concat
    |> Seq.toArray

let inline private getOrAdd (key: 'key) (getValue: 'key -> 'value) (d: Dictionary<'key, 'value>) : 'value =
    let value: 'value ref = ref Unchecked.defaultof<_>
    if d.TryGetValue(key, value) then !value
    else
        let value = getValue key
        d.[key] <- value
        value

/// Applies binding redirects for all strong-named references to all app. and web.config files.
let private applyBindingRedirects isFirstGroup createNewBindingFiles redirects cleanBindingRedirects
                                  root groupName findDependencies allKnownLibs 
                                  (projectCache: Dictionary<string, ProjectFile option>) 
                                  extractedPackages =

    let dependencyGraph = Dictionary<_,Set<_>>()
    let referenceFiles = Dictionary<_,ReferencesFile option>()

    let referenceFile (projectFile : ProjectFile) =
        let referenceFile (projectFile : ProjectFile) =
            projectFile.FindReferencesFile()
            |> Option.map ReferencesFile.FromFile
        referenceFiles |> getOrAdd projectFile referenceFile

    let rec dependencies (projectFile : ProjectFile) =
        match referenceFile projectFile with
        | Some referenceFile -> 
            projectFile.GetInterProjectDependencies()
            |> Seq.map (fun r -> projectCache |> getOrAdd r.Path ProjectFile.TryLoad)
            |> Seq.choose id
            |> Seq.collect (fun p -> dependencyGraph |> getOrAdd p dependencies)
            |> Seq.append (
                referenceFile.Groups
                |> Seq.filter (fun g -> g.Key = groupName)
                |> Seq.collect (fun g -> g.Value.NugetPackages |> List.map (fun p -> (groupName,p.Name)))
                |> Seq.collect(fun (g,p) -> findDependencies(g,p,projectFile.FileName))
                |> Seq.map (fun x -> x, projectFile.GetTargetProfile()))
            |> Set.ofSeq
        | None -> Set.empty

    let bindingRedirects (projectFile : ProjectFile) =
        let referenceFile = referenceFile projectFile
        let dependencies = dependencyGraph |> getOrAdd projectFile dependencies
        let redirectsFromReference packageName =
            referenceFile
            |> Option.bind (fun r ->
                r.Groups
                |> Seq.filter (fun g -> g.Key = groupName)
                |> Seq.collect (fun g -> g.Value.NugetPackages)
                |> Seq.tryFind (fun p -> p.Name = packageName)
                |> Option.bind (fun p -> p.Settings.CreateBindingRedirects))

        let targetProfile = projectFile.GetTargetProfile()

        let assemblies =
            extractedPackages
            |> Seq.map (fun (model,redirects) -> (model, redirectsFromReference model.PackageName |> Option.fold (fun _ x -> Some x) redirects))
            |> Seq.collect (fun (model,redirects) -> 
                dependencies
                |> Set.filter (fst >> ((=) model.PackageName))
                |> Seq.collect (fun (_,profile) ->
                    model.GetLibReferences profile
                    |> Seq.map (fun x -> x, redirects, profile)))
            |> Seq.groupBy (fun (p,_,profile) -> profile,FileInfo(p).Name)
            |> Seq.choose(fun (_,librariesForPackage) ->
                librariesForPackage
                |> Seq.choose(fun (library,redirects,profile) ->
                    try
                        let assembly = Mono.Cecil.AssemblyDefinition.ReadAssembly(library)
                        Some (assembly, BindingRedirects.getPublicKeyToken assembly, assembly.MainModule.AssemblyReferences, redirects, profile)
                    with _ -> None)
                |> Seq.sortBy(fun (assembly,_,_,_,_) -> assembly.Name.Version)
                |> Seq.toList
                |> List.rev
                |> function | head :: _ -> Some head | _ -> None)
            |> Seq.cache

        let referencesDifferentProfiles (assemblyName : Mono.Cecil.AssemblyNameDefinition) profile =
            profile = targetProfile
            && assemblies
            |> Seq.filter (fun (_,_,_,_,p) -> p <> profile)
            |> Seq.map (fun (a,_,_,_,_) -> a.Name)
            |> Seq.filter (fun a -> a.Name = assemblyName.Name)
            |> Seq.exists (fun a -> a.Version <> assemblyName.Version)

        assemblies
        |> Seq.choose (fun (assembly,token,refs,redirects,profile) -> token |> Option.map (fun token -> (assembly,token,refs,redirects,profile)))
        |> Seq.filter (fun (_,_,_,packageRedirects,_) -> defaultArg ((packageRedirects |> Option.map ((<>) Off)) ++ redirects) false)
        |> Seq.filter (fun (assembly,_,_,redirects,profile) ->
            let assemblyName = assembly.Name 
            redirects = Some Force
            || referencesDifferentProfiles assemblyName profile
            || assemblies
            |> Seq.collect (fun (_,_,refs,_,_) -> refs)
            |> Seq.filter (fun a -> assemblyName.Name = a.Name)
            |> Seq.exists (fun a -> assemblyName.Version > a.Version))
        |> Seq.map(fun (assembly, token,_,_,_) ->
            { BindingRedirect.AssemblyName = assembly.Name.Name
              Version = assembly.Name.Version.ToString()
              PublicKeyToken = token
              Culture = None })
        |> Seq.sort

    applyBindingRedirectsToFolder isFirstGroup createNewBindingFiles cleanBindingRedirects root allKnownLibs bindingRedirects

let installForDotnetSDK (project:ProjectFile) = 
    let paketTargetsPath = RestoreProcess.extractBuildTask.Force()
    let relativePath = createRelativePath project.FileName paketTargetsPath    
    project.AddImportForPaketTargets(relativePath)

/// Installs all packages from the lock file.
let InstallIntoProjects(options : InstallerOptions, forceTouch, dependenciesFile, lockFile : LockFile, projectsAndReferences : (ProjectFile * ReferencesFile) list, updatedGroups) =
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
    let projectCache = Dictionary<string, ProjectFile option>();

    for project, referenceFile in projectsAndReferences do
        verbosefn "Installing to %s" project.FileName
        let directDependencies =
            referenceFile.Groups
            |> Seq.map (fun kv ->
                lockFile.GetRemoteReferencedPackages(referenceFile,kv.Value) @ kv.Value.NugetPackages
                |> Seq.map (fun ps ->
                    let group = 
                        match lockFile.Groups |> Map.tryFind kv.Key with
                        | Some g -> g
                        | None -> failwithf "%s uses the group %O, but this group was not found in paket.lock." referenceFile.FileName kv.Key

                    let package = 
                        match model |> Map.tryFind (kv.Key, ps.Name) with
                        | Some (p,_) -> p
                        | None -> failwithf "%s uses NuGet package %O, but it was not found in the paket.lock file in group %O.%s" referenceFile.FileName ps.Name kv.Key (lockFile.CheckIfPackageExistsInAnyGroup ps.Name)

                    let resolvedSettings = 
                        [package.Settings; group.Options.Settings] 
                        |> List.fold (+) ps.Settings
                    (kv.Key,ps.Name), (package.Version,resolvedSettings)))
            |> Seq.concat
            |> Map.ofSeq

        let usedPackages =
            let d = ref directDependencies

            /// we want to treat the settings from the references file through the computation so that it can be used as the base that 
            /// the other settings modify. In this way we ensure that references files can override the dependencies file, which in turn overrides the lockfile.
            let usedPackageDependencies = 
                directDependencies 
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

        let usedPackages =
            let dict = System.Collections.Generic.Dictionary<_,_>()
            usedPackages
            |> Map.filter (fun (_groupName,packageName) (v,_) -> 
                match dict.TryGetValue packageName with
                | true,v' -> 
                    if v' = v then false else
                    failwithf "Package %O is referenced in different versions in %s" packageName project.FileName
                | _ ->
                    dict.Add(packageName,v)
                    true)

        if project.GetToolsVersion() >= "15.0" then 
            installForDotnetSDK project  
        else
            project.UpdateReferences(root, model, directDependencies, usedPackages)
    
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
                          WithPaketSubNode = true
                          CopyToOutputDirectory = None
                          Link = Some link }
                    else
                        { BuildAction = buildAction
                          WithPaketSubNode = true
                          CopyToOutputDirectory = None
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
        project.Save forceTouch
        projectCache.[project.FileName] <- Some project

        let first = ref true

        let redirects =
            match options.Redirects with
            | true -> Some true
            | false -> None

        let allKnownLibs =
            model
            |> Seq.map (fun kv -> (snd kv.Value).GetLibReferencesLazy.Force())
            |> Set.unionMany

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
            |> applyBindingRedirects 
                !first 
                options.CreateNewBindingFiles
                (g.Value.Options.Redirects ++ redirects) 
                options.CleanBindingRedirects
                (FileInfo project.FileName).Directory.FullName 
                g.Key 
                lockFile.GetAllDependenciesOf
                allKnownLibs
                projectCache
            first := false

/// Installs all packages from the lock file.
let Install(options : InstallerOptions, forceTouch, dependenciesFile, lockFile : LockFile, updatedGroups) =    
    let root = FileInfo(lockFile.FileName).Directory.FullName

    let projects = RestoreProcess.findAllReferencesFiles root |> returnOrFail
    InstallIntoProjects(options, forceTouch, dependenciesFile, lockFile, projects, updatedGroups)