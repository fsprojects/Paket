/// Contains methods for the install process.
module Paket.InstallProcess

open Paket
open Chessie.ErrorHandling
open Paket.Domain
open Paket.Logging
open Paket.BindingRedirects
open System.IO
open Paket.PackageSources
open Paket.Requirements
open System.Collections.Generic
open System
open ProviderImplementation.AssemblyReader

let updatePackagesConfigFile (model: Map<GroupName*PackageName,SemVerInfo*InstallSettings>) packagesConfigFileName =
    let packagesInConfigFile = PackagesConfigFile.Read packagesConfigFileName

    let packagesInModel =
        model
        |> Seq.filter (fun kv -> defaultArg (snd kv.Value).IncludeVersionInPath false)
        |> Seq.map (fun kv ->
            let v = fst kv.Value
            { NugetPackage.Id = (snd kv.Key).ToString()
              VersionRequirement = VersionRequirement(VersionRange.Specific v,PreReleaseStatus.No)
              Kind = NugetPackageKind.Package
              TargetFramework = None })
        |> Seq.toList

    if packagesInModel <> [] then
        packagesInConfigFile
        |> Seq.filter (fun p -> packagesInModel |> Seq.exists (fun p' -> p'.Id = p.Id) |> not)
        |> Seq.append packagesInModel
        |> PackagesConfigFile.Save packagesConfigFileName

let findPackageFolder root (groupName,packageName) (version,settings) =
    let includeVersionInPath = defaultArg settings.IncludeVersionInPath false
    let storageOption = defaultArg settings.StorageConfig PackagesFolderGroupConfig.Default
    match storageOption.Resolve root groupName packageName version includeVersionInPath with
    | ResolvedPackagesFolder.SymbolicLink targetFolder
    | ResolvedPackagesFolder.ResolvedFolder targetFolder ->
        let direct = DirectoryInfo targetFolder
        if direct.Exists then
            direct
        else
            let lowerName = packageName.ToString() + if includeVersionInPath then "." + version.ToString() else ""
            let di =
                if groupName = Constants.MainDependencyGroup then
                    DirectoryInfo(Path.Combine(root, Constants.DefaultPackagesFolderName))
                else
                    let groupName = groupName.CompareString
                    let di = DirectoryInfo(Path.Combine(root, Constants.DefaultPackagesFolderName, groupName))
                    if di.Exists then di else

                    match di.GetDirectories() |> Seq.tryFind (fun subDir -> String.endsWithIgnoreCase groupName subDir.FullName) with
                    | Some x -> x
                    | None ->
                        traceWarnfn "The following directories exists:"
                        for d in di.GetDirectories() do
                            traceWarnfn "  %s" d.FullName

                        failwithf "Group directory for group %s was not found." groupName

            let found =
                di.GetDirectories()
                |> Seq.tryFind (fun subDir -> String.endsWithIgnoreCase lowerName subDir.FullName)

            match found with
            | Some x -> x
            | None -> failwithf "Package directory for package %O was not found in %s." packageName di.FullName

    | ResolvedPackagesFolder.NoPackagesFolder ->
        let d = DirectoryInfo(NuGetCache.GetTargetUserFolder packageName version)
        if not d.Exists then
            failwithf "Package directory for package %O was not found in %s. Storage mode is \"none\"." packageName d.FullName
        d

let contentFileBlackList : list<FileInfo -> bool> = [
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
                let settings = snd kv.Value
                let contentCopySettings = defaultArg settings.OmitContent ContentCopySettings.Overwrite
                let contentCopyToOutputSettings = settings.CopyContentToOutputDirectory
                kv.Key,kv.Value,contentCopySettings,contentCopyToOutputSettings)
            |> Seq.filter (fun (_,_,contentCopySettings,_) -> contentCopySettings <> ContentCopySettings.Omit)
            |> Seq.map (fun ((group, packName),(v,i),s,s') ->
                    let packageDir =
                        try
                            findPackageFolder root (group, packName) (v,i)
                        with
                        | _ ->
                            findPackageFolder root (group, packName) (v,{ i with IncludeVersionInPath = None })
                    s,s',packageDir)
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
        |> List.filter (fun fi ->
                not (fi.FullName.Contains Constants.PaketFilesFolderName) &&
                 not (contentFiles.Contains fi.FullName) &&
                 fi.Name <> "paket.references")
        |> removeFilesAndTrimDirs

    removeCopiedFiles project

    project.UpdateFileItems(gitRemoteItems @ nuGetFileItems)


/// Restores the given packages from the lock file.
let CreateModel(alternativeProjectRoot, root, force, dependenciesFile:DependenciesFile, lockFile : LockFile, packages:Set<GroupName*PackageName>, updatedGroups:Map<_,_>) =
    for kv in lockFile.Groups do
         let files = if updatedGroups |> Map.containsKey kv.Key then [] else kv.Value.RemoteFiles
         if List.isEmpty files |> not then
             RemoteDownload.DownloadSourceFiles(root, kv.Key, force, files)

    lockFile.Groups
    |> Seq.collect (fun kv' ->
        let sources = dependenciesFile.Groups.[kv'.Key].Sources
        let caches = dependenciesFile.Groups.[kv'.Key].Caches
        kv'.Value.Resolution
        |> Map.filter (fun name _ -> packages.Contains(kv'.Key,name))
        |> Seq.map (fun kv -> RestoreProcess.CreateInstallModel(alternativeProjectRoot, root,kv'.Key,sources,caches,force,kv'.Value.GetPackage kv.Key))
        |> Seq.splitInto 5
        |> Seq.map (fun tasks -> async {
            let results = new System.Collections.Generic.List<_>()
            for t in tasks do
                let! r = t
                results.Add(r)
            return results })
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

// HashSet to prevent repeating the same "Broken project dependency" warning
let brokenDeps = HashSet<_>()

/// Applies binding redirects for all strong-named references to all app. and web.config files.
let private applyBindingRedirects isFirstGroup createNewBindingFiles cleanBindingRedirects
                                  root groupName findDependencies allKnownLibNames
                                  (projectCache: Dictionary<string, ProjectFile option>)
                                  (extractedPackages:seq<InstallModel * _>) =

    let dependencyGraph = Dictionary<_,Set<_>>()
    let referenceFiles = Dictionary<_,ReferencesFile option>()

    let referenceFile (projectFile : ProjectFile) =
        let referenceFile (projectFile : ProjectFile) =
            projectFile.FindReferencesFile()
            |> Option.map ReferencesFile.FromFile
        referenceFiles |> getOrAdd projectFile referenceFile


    let rec dependencies (projectFile : ProjectFile) =
        let reportBrokenDep (src:string) (target:string) =
            if brokenDeps.Add (src,target) then
                traceWarnfn "Broken project dependency: '%s' -> '%s'" src target

        match referenceFile projectFile with
        | Some referenceFile ->
            projectFile.GetInterProjectDependencies()
            |> Seq.map (fun r ->
                let found = getOrAdd r.Path ProjectFile.TryLoad projectCache
                match found with
                | Some prj -> Some prj
                | None ->
                    reportBrokenDep projectFile.FileName r.Path
                    None)

            |> Seq.choose id
            |> Seq.map (fun p -> dependencyGraph |> getOrAdd p dependencies)
            |> Set.unionMany
            |> Set.union (
                referenceFile.Groups
                |> Seq.filter (fun g -> g.Key = groupName)
                |> Seq.collect (fun g -> g.Value.NugetPackages |> List.map (fun p -> (groupName,p.Name)))
                |> Seq.collect(fun (g,p) -> findDependencies(g,p,projectFile.FileName))
                |> Seq.collect (fun x -> projectFile.GetTargetProfiles() |> Seq.map (fun p -> x, p))
                |> Set.ofSeq)
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

        let targetProfiles = projectFile.GetTargetProfiles()

        let assemblies =
            extractedPackages
            |> Seq.filter (fun (model,_redirects) ->
                match model.Kind with
                | InstallModelKind.Package ->
                    true
                | InstallModelKind.DotnetCliTool ->
                    false )
            |> Seq.map (fun (model,redirects) -> (model, redirectsFromReference model.PackageName |> Option.fold (fun _ x -> Some x) redirects))
            |> Seq.collect (fun (model,redirects) ->
                dependencies
                |> Set.filter (fst >> ((=) model.PackageName))
                |> Seq.collect (fun (_,profile) ->
                    model.GetLegacyReferences profile
                    |> Seq.map (fun x -> x, redirects, profile)))
            |> Seq.groupBy (fun (p,_,profile) -> profile,FileInfo(p.Path).Name)
            |> Seq.choose(fun (_,librariesForPackage) ->
                librariesForPackage
                |> Seq.choose(fun (library,redirects,profile) ->
                    try
                        let assemblyReader =
                            ILModuleReaderAfterReadingAllBytes(library.Path, mkILGlobals EcmaMscorlibScopeRef)
                        let assembly = assemblyReader.ILModuleDef.ManifestOfAssembly
                        let assemblyName = assembly.GetName()
                        Some (assembly, BindingRedirects.getPublicKeyToken assemblyName, assemblyReader.ILAssemblyRefs, redirects, profile)
                    with _ -> None)
                |> Seq.sortBy(fun (assembly,_,_,_,_) -> assembly.Version)
                |> Seq.toList
                |> List.rev
                |> function | head :: _ -> Some head | _ -> None)
            |> Seq.cache

        let referencesDifferentProfiles assemblyName assemblyVersion profile =
            (targetProfiles |> Seq.exists ((=) profile))
            && assemblies
            |> Seq.filter (fun (_,_,_,_,p) -> p <> profile)
            |> Seq.map (fun (a,_,_,_,_) -> a)
            |> Seq.filter (fun a -> a.Name = assemblyName)
            |> Seq.exists (fun a -> a.Version <> assemblyVersion)

        assemblies
        |> Seq.choose (fun (assembly,token,refs,redirects,profile) ->
                token |> Option.map (fun token -> (assembly,token,refs,redirects,profile)))
        |> Seq.filter (fun (_,_,_,redirects,_) ->
            match redirects with
            | Some BindingRedirectsSettings.On
            | Some BindingRedirectsSettings.Force -> true
            | Some BindingRedirectsSettings.Off -> false
            | _ -> false)
        |> Seq.filter (fun (assembly,_,_,redirects,profile) ->
            redirects = Some BindingRedirectsSettings.Force
            || referencesDifferentProfiles assembly.Name assembly.Version profile
            || assemblies
            |> Seq.collect (fun (_,_,refs,_,_) -> refs)
            |> Seq.filter (fun a -> assembly.Name = a.Name)
            |> Seq.exists (fun a -> assembly.Version > a.Version))
        |> Seq.map(fun (assembly, token,_,_,_) ->
            { BindingRedirect.AssemblyName = assembly.Name
              Version = Option.fold (fun _ x -> x.ToString()) "0.0.0.0" assembly.Version
              PublicKeyToken = token
              Culture = None })
        |> Seq.sort

    applyBindingRedirectsToFolder isFirstGroup createNewBindingFiles cleanBindingRedirects root allKnownLibNames bindingRedirects

let installForDotnetSDK root (project:ProjectFile) =
    let paketTargetsPath = RestoreProcess.extractRestoreTargets root
    let relativePath = createRelativePath project.FileName paketTargetsPath

    Paket.ProjectFile.removePaketNodes project
    project.RemoveImportForPaketTargets()
    project.AddImportForPaketTargets(relativePath)

/// Installs all packages from the lock file.
let InstallIntoProjects(options : InstallerOptions, forceTouch, dependenciesFile, lockFile : LockFile, projectsAndReferences : (ProjectFile * ReferencesFile) list, updatedGroups, touchedPackages:((GroupName * PackageName * (SemVerInfo option) * (SemVerInfo option)) list) option) =
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
        |> Set

    let touchedProjects = System.Collections.Generic.HashSet<_>()
    tracefn "Created dependency graph (%d packages in total)" packagesToInstall.Count
    let root = Path.GetDirectoryName lockFile.FileName
    let model = CreateModel(options.AlternativeProjectRoot, root, options.Force, dependenciesFile, lockFile, packagesToInstall, updatedGroups) |> Map.ofArray
    let lookup = lockFile.GetDependencyLookupTable()
    let projectCache = Dictionary<string, ProjectFile option>();

    let prefix = dependenciesFile.Directory.Length + 1
    let norm (s:string) = (s.Substring prefix).Replace('\\', '/')

    if projectsAndReferences.Length > 0 then
        verbosefn " - Installing to %d projects" projectsAndReferences.Length

    for project, referenceFile in projectsAndReferences do
        let toolsVersion = project.GetToolsVersion()
        verbosefn "   - %s -> %s (MSBuild %O)" (norm referenceFile.FileName) (norm project.FileName) toolsVersion

        let directDependencies, errorMessages =
            referenceFile.Groups
            |> Seq.collect (fun kv ->
                lockFile.GetRemoteReferencedPackages(referenceFile,kv.Value) @ kv.Value.NugetPackages
                |> Seq.map (fun ps ->
                    let group =
                        match lockFile.Groups |> Map.tryFind kv.Key with
                        | Some g -> Choice1Of2 g
                        | None -> Choice2Of2 (sprintf " - %s uses the group %O, but this group was not found in paket.lock." referenceFile.FileName kv.Key)

                    let package =
                        match model |> Map.tryFind (kv.Key, ps.Name) with
                        | Some (p,_) -> Choice1Of2 p
                        | None -> Choice2Of2 (sprintf " - %s uses NuGet package %O, but it was not found in the paket.lock file in group %O.%s" referenceFile.FileName ps.Name kv.Key (lockFile.CheckIfPackageExistsInAnyGroup ps.Name))

                    match group, package with
                    | Choice1Of2 _, Choice1Of2 package ->
                        ((kv.Key,package.Name), (package.Version, ps.Settings + package.Settings))
                        |> Choice1Of2
                    | Choice2Of2 error1, Choice2Of2 error2 -> Choice2Of2 (error1 + "\n" + error2)
                    | Choice2Of2 error, _ | _, Choice2Of2 error -> Choice2Of2 error
                    ))
            |> Seq.partitionAndChoose
                    (function Choice1Of2 _ -> true | Choice2Of2 _ -> false)
                    (function Choice1Of2 resolvedPackage -> Some resolvedPackage | _ -> None)
                    (function Choice2Of2 errorMessage -> Some errorMessage | _ -> None)
            |> fun (resolvedPackages, dependencyErrors) ->
                    Map.ofSeq resolvedPackages, dependencyErrors

        let usedPackages, errorMessages =
            let mutable d = directDependencies

            /// we want to treat the settings from the references file through the computation so that it can be used as the base that
            /// the other settings modify. In this way we ensure that references files can override the dependencies file, which in turn overrides the lockfile.
            let usedPackageDependencies, groupErrors =
                directDependencies
                |> Seq.collect (fun u -> lookup.[u.Key] |> Seq.map (fun i -> fst u.Key, u.Value, i))
                |> Seq.partitionAndChoose
                    (fun (groupName,_, _) -> lockFile.Groups |> Map.containsKey groupName)
                    (fun (groupName,(_,parentSettings), dep) ->
                        let group = lockFile.Groups.[groupName]
                        match group.TryFind dep with
                        | None -> None
                        | Some p -> Some ((groupName,p.Name), (p.Version,parentSettings + p.Settings)) )
                    (fun (groupName,_, _) ->
                        Some (sprintf " - %s uses the group %O, but this group was not found in paket.lock." referenceFile.FileName groupName)
                    )
            for key,settings in usedPackageDependencies do
                if d.ContainsKey key |> not then
                    d <- Map.add key settings d
            d, Seq.append errorMessages groupErrors

        let usedPackages, errorMessages =
            let dict = System.Collections.Generic.Dictionary<PackageName,SemVerInfo*bool*FrameworkRestrictions>()
            let errors = ResizeArray ()
            usedPackages
            |> Map.filter (fun (_groupName,packageName) (v,settings) ->
                let hasCondition = settings.ReferenceCondition.IsSome
                match dict.TryGetValue packageName with
                | true,(_,true,_) when hasCondition ->
                    true
                | true,(v',_,restrictions') ->
                    let filtered = filterRestrictions settings.FrameworkRestrictions restrictions'
                    dict.[packageName] <- (v,hasCondition,filtered)
                    if v' = v then
                        traceWarnfn "Package %O is referenced through multiple groups in %s (inspect lockfile for details). To resolve this warning use a single group for this project to get a unified dependency resolution or use conditions on the groups." packageName project.FileName
                        false
                    else
                        match settings.FrameworkRestrictions, restrictions' with
                        | ExplicitRestriction r, ExplicitRestriction r2 when not (r.IsSubsetOf r2 || r = r2) ->
                            true
                        | _ ->
                            errors.Add (sprintf "Package %O is referenced in different versions in %s (%O vs %O), (inspect the lockfile for details) to resolve this either add all dependencies to a single group (to get a unified resolution) or use a condition on both groups and control compilation yourself." packageName project.FileName v' v)
                            false
                | _ ->
                    dict.Add(packageName,(v,hasCondition,settings.FrameworkRestrictions))
                    true)
            |> fun usedPackages -> usedPackages, Seq.append errorMessages errors

        let gitRemotePathPairs, errorMessages =
            ((Seq.empty,Seq.empty),referenceFile.Groups)
            ||> Seq.fold (fun (pathAcc,errorAcc) kv ->
                let refpaths, errors =
                    kv.Value.RemoteFiles
                    |> Seq.partitionAndChoose
                        // reject files with missing group names or who can't be found in the group specified
                        (fun remoteFile ->
                            match lockFile.Groups |> Map.tryFind kv.Key with
                            | None -> false
                            | Some group ->
                                group.RemoteFiles
                                |> Seq.exists (fun f -> Path.GetFileName(f.Name) = remoteFile.Name))
                        // get the full path of the remote item
                        (fun remoteFile ->
                            let group = lockFile.Groups.[kv.Key]
                            group.RemoteFiles
                            |> Seq.find (fun f -> Path.GetFileName(f.Name) = remoteFile.Name)
                            |> fun  file -> Some (remoteFile, (file.FilePath(root,kv.Key))))
                        (fun remoteFile ->
                            Some (sprintf "%s references file %s in group %O, but it was not found in the paket.lock file." referenceFile.FileName remoteFile.Name kv.Key)
                        )
                (Seq.append pathAcc refpaths),(Seq.append errorAcc errors)
            )|> fun (refpaths,errors) -> Seq.toArray refpaths, Seq.concat [| errorMessages ; errors |] |> Seq.toArray

        if toolsVersion >= 15.0 then
            // HACK: just validate that the list of packages contains one named FSharp.Core, if it is a *.fsproj
            if project.Name.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase) then
                let hasFSharpCore =
                    usedPackages |> Seq.exists (fun kv ->
                        let _, x = kv.Key
                        x.CompareString = "fsharp.core")
                if not hasFSharpCore then
                    traceWarnfn "F# project %s does not reference FSharp.Core." project.FileName

        // if any errors have been found during the installation process thus far, fail and print all errors collected
        if not (Seq.isEmpty errorMessages) then
            failwithf "Installation Errors :\n%s" (String.concat "\n" errorMessages)
        else // start the installation process
            if toolsVersion >= 15.0 then
                installForDotnetSDK root project
                if forceTouch then
                    touchedProjects.Add project |> ignore
                else
                    match touchedPackages with
                    | Some touchedPackages ->
                        let packageInstalled =
                            touchedPackages
                            |> Seq.exists (fun (g,p,_,_) -> project.HasPackageInstalled (g,p))
                        if packageInstalled then
                            touchedProjects.Add project |> ignore
                    | _ ->
                        touchedProjects.Add project |> ignore
            else
                project.UpdateReferences(model, directDependencies, usedPackages)

                Path.Combine(FileInfo(project.FileName).Directory.FullName, Constants.PackagesConfigFile)
                |> updatePackagesConfigFile usedPackages

            let gitRemoteItems =
                gitRemotePathPairs
                |> Seq.map (fun (file,remoteFilePath) ->
                    let link = if file.Link = "." then Path.GetFileName file.Name else Path.Combine(file.Link, Path.GetFileName file.Name)
                    verbosefn "FileName: %s " file.Name

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

                                File.Copy(remoteFilePath,targetFile.FullName,true)
                                createRelativePath project.FileName targetFile.FullName
                          Link = None }
                ) |> Seq.toList

            if toolsVersion >= 15.0 then
                let resolved = lazy(model |> Map.map (fun k v -> fst v))
                let _referencesFile = Paket.RestoreProcess.RestoreNewSdkProject lockFile resolved lockFile.Groups project None
                processContentFiles root project Map.empty gitRemoteItems options
            else
                processContentFiles root project usedPackages gitRemoteItems options

            project.Save forceTouch
            projectCache.[project.FileName] <- Some project

            let mutable first = true

            let allKnownLibNames =
                model
                |> Seq.map (fun kv -> (snd kv.Value).GetAllLegacyReferenceAndFrameworkReferenceNames())
                |> Set.unionMany

            let commandRedirects =
                if options.Redirects = BindingRedirectsSettings.Off then None
                else Some options.Redirects

            for g in lockFile.Groups do
                let group = g.Value

                let groupRedirects =
                    g.Value.Options.Redirects ++ commandRedirects

                model
                |> Seq.filter (fun kv -> (fst kv.Key) = g.Key)
                |> Seq.map (fun kv ->
                    let packageRedirects =
                        group.Resolution
                        |> Map.tryFind (snd kv.Key)
                        |> Option.bind (fun p -> p.Settings.CreateBindingRedirects ++ groupRedirects)

                    (snd kv.Value,packageRedirects))
                |> applyBindingRedirects
                    first
                    options.CreateNewBindingFiles
                    options.CleanBindingRedirects
                    (FileInfo project.FileName).Directory.FullName
                    g.Key
                    lockFile.GetAllDependenciesOf
                    allKnownLibNames
                    projectCache
                first <- false

    let restoreCacheFile = Path.Combine(root, Constants.PaketRestoreHashFilePath)
    let hash = Paket.RestoreProcess.getLockFileHashFromContent (lockFile.ToString())
    // NIT: We probably need to check if we have really fully restored (could be partial install)
    // Lets see if users report issues
    Paket.RestoreProcess.writeRestoreCache restoreCacheFile { PackagesDownloadedHash = hash; ProjectsRestoredHash = hash }
    Paket.RestoreProcess.writeGitignore restoreCacheFile

    for project in touchedProjects |> Seq.distinctBy (fun p -> p.FileName) |> Seq.sortBy (fun p -> p.FileName) do
        tracefn " - Project %O needs to be restored" project.Name
        let di = (FileInfo project.FileName).Directory
        for objDir in Directory.EnumerateDirectories(di.FullName,"obj", SearchOption.AllDirectories) do
            for file in Directory.EnumerateFiles(objDir,"project.assets.json", SearchOption.AllDirectories) do
                try
                    File.Delete file
                with
                | _ -> ()

            for file in Directory.EnumerateFiles(objDir,"*.references", SearchOption.AllDirectories) do
                try
                    File.Delete file
                with
                | _ -> ()

            for file in Directory.EnumerateFiles(objDir,"*.paket.*", SearchOption.AllDirectories) do
                try
                    File.Delete file
                with
                | _ -> ()

/// Installs all packages from the lock file.
let Install(options : InstallerOptions, forceTouch, dependenciesFile, lockFile : LockFile, updatedGroups, touchedPackages) =
    let root = FileInfo(lockFile.FileName).Directory.FullName

    let projects = RestoreProcess.findAllReferencesFiles root |> returnOrFail
    InstallIntoProjects(options, forceTouch, dependenciesFile, lockFile, projects, updatedGroups,touchedPackages)
