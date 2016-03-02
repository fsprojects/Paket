namespace Paket

open Paket.Domain
open Paket.Logging
open Paket.PackageSources

open System
open System.IO
open Chessie.ErrorHandling
open InstallProcess

/// Paket API which is optimized for F# Interactive use.
type Dependencies(dependenciesFileName: string) =
    let getLockFile() =
        let lockFileName = DependenciesFile.FindLockfile dependenciesFileName
        LockFile.LoadFrom(lockFileName.FullName)

    let listPackages (packages: System.Collections.Generic.KeyValuePair<GroupName*PackageName, PackageResolver.ResolvedPackage> seq) =
        packages
        |> Seq.map (fun kv ->
                let groupName,packageName = kv.Key
                groupName.ToString(),packageName.ToString(),kv.Value.Version.ToString())
        |> Seq.toList
        
    /// Clears the NuGet cache
    static member ClearCache() = 
        Utils.removeDirContents (DirectoryInfo Constants.UserNuGetPackagesFolder)
        Utils.removeDirContents (DirectoryInfo Constants.NuGetCacheFolder)
        Utils.removeDirContents (DirectoryInfo Constants.GitRepoCacheFolder)

    /// Tries to locate the paket.dependencies file in the current folder or a parent folder.
    static member Locate(): Dependencies = Dependencies.Locate(Environment.CurrentDirectory)

    /// Tries to locate the paket.dependencies file in the given folder or a parent folder.
    static member Locate(path: string): Dependencies =
        let rec findInPath(dir:DirectoryInfo,withError) =
            let path = Path.Combine(dir.FullName,Constants.DependenciesFileName)
            if File.Exists(path) then
                path
            else
                let parent = dir.Parent
                match parent with
                | null ->
                    if withError then
                        failwithf "Could not find '%s'. To use Paket with this solution, please run 'paket init' first." Constants.DependenciesFileName
                    else
                        Constants.DependenciesFileName
                | _ -> findInPath(parent, withError)

        let dependenciesFileName = findInPath(DirectoryInfo path,true)
        verbosefn "found: %s" dependenciesFileName
        Dependencies(dependenciesFileName)

    /// Initialize paket.dependencies file in current directory
    static member Init() = Dependencies.Init(Environment.CurrentDirectory)

    /// Initialize paket.dependencies file in the given directory
    static member Init(directory) =
        let directory = DirectoryInfo(directory)

        Utils.RunInLockedAccessMode(
            directory.FullName,
            fun () ->
                PaketEnv.init directory
                |> returnOrFail
        )

    /// Converts the solution from NuGet to Paket.
    static member ConvertFromNuget(force: bool,installAfter: bool, initAutoRestore: bool,credsMigrationMode: string option, ?directory) : unit =
        let dir = defaultArg directory (DirectoryInfo(Environment.CurrentDirectory))
        let rootDirectory = dir

        Utils.RunInLockedAccessMode(
            rootDirectory.FullName,
            fun () ->
                NuGetConvert.convertR rootDirectory force credsMigrationMode
                |> returnOrFail
                |> NuGetConvert.replaceNuGetWithPaket initAutoRestore installAfter
        )

    /// Converts the current package dependency graph to the simplest dependency graph.
    member this.Simplify(interactive : bool) =
        Utils.RunInLockedAccessMode(
            this.RootPath,
            fun () ->
                PaketEnv.fromRootDirectory this.RootDirectory
                >>= PaketEnv.ensureNotInStrictMode
                >>= Simplifier.simplify interactive
                |> returnOrFail

                |> Simplifier.updateEnvironment
        )

    /// Get path to dependencies file
    member this.DependenciesFile with get() = dependenciesFileName

    /// Get the root path
    member this.RootPath with get() = Path.GetDirectoryName(dependenciesFileName)

    /// Get the root directory
    member private this.RootDirectory with get() = DirectoryInfo(this.RootPath)

    /// Binds the given processing ROP function to current environment and executes it.
    /// Throws on failure.
    member private this.Process f =
        PaketEnv.fromRootDirectory(this.RootDirectory)
        >>= f
        |> returnOrFail

    /// Adds the given package without version requirements to the dependencies file.
    member this.Add(groupName, package: string): unit = this.Add(groupName, package,"")

    /// Adds the given package without version requirements to main dependency group of the dependencies file.
    member this.Add(package: string): unit = this.Add(None, package,"")

    /// Adds the given package with the given version to the dependencies file.
    member this.Add(groupName: string option, package: string,version: string): unit =
        this.Add(groupName, package, version, force = false, hard = false, withBindingRedirects = false,  createNewBindingFiles = false, interactive = false, installAfter = true, semVerUpdateMode = SemVerUpdateMode.NoRestriction)

    /// Adds the given package with the given version to the dependencies file.
    member this.Add(groupName: string option, package: string,version: string,force: bool,hard: bool,withBindingRedirects: bool, createNewBindingFiles:bool, interactive: bool,installAfter: bool, semVerUpdateMode): unit =
        Utils.RunInLockedAccessMode(
            this.RootPath,
            fun () -> AddProcess.Add(dependenciesFileName, groupName, PackageName(package.Trim()), version,
                                     InstallerOptions.CreateLegacyOptions(force, hard, withBindingRedirects, createNewBindingFiles, semVerUpdateMode),
                                     interactive, installAfter))

   /// Adds the given package with the given version to the dependencies file.
    member this.AddToProject(groupName, package: string,version: string,force: bool,hard: bool,withBindingRedirects: bool, createNewBindingFiles:bool, projectName: string,installAfter: bool, semVerUpdateMode): unit =
        Utils.RunInLockedAccessMode(
            this.RootPath,
            fun () -> AddProcess.AddToProject(dependenciesFileName, groupName, PackageName package, version,
                                              InstallerOptions.CreateLegacyOptions(force, hard, withBindingRedirects, createNewBindingFiles, semVerUpdateMode),
                                              projectName, installAfter))

    /// Adds credentials for a Nuget feed
    member this.AddCredentials(source: string, username: string) : unit =
        Utils.RunInLockedAccessMode(
            this.RootPath,
            fun () -> ConfigFile.askAndAddAuth source username |> returnOrFail )
  
    /// Adds a token for a source
    member this.AddToken(source : string, token : string) : unit =
        Utils.RunInLockedAccessMode(this.RootPath, fun () -> ConfigFile.AddToken(source, token) |> returnOrFail)

    /// Installs all dependencies.
    member this.Install(force: bool, hard: bool) = this.Install(force, hard, false, false, SemVerUpdateMode.NoRestriction)

    /// Installs all dependencies.
    member this.Install(force: bool, hard: bool, withBindingRedirects: bool, createNewBindingFiles:bool, semVerUpdateMode): unit =
        this.Install(force, hard, withBindingRedirects, createNewBindingFiles, false, semVerUpdateMode)

    /// Installs all dependencies.
    member this.Install(force: bool, hard: bool, withBindingRedirects: bool, createNewBindingFiles:bool, onlyReferenced: bool, semVerUpdateMode): unit =
        this.Install({ InstallerOptions.CreateLegacyOptions(force, hard, withBindingRedirects, createNewBindingFiles, semVerUpdateMode) with OnlyReferenced = onlyReferenced })

    /// Installs all dependencies.
    member private this.Install(options: InstallerOptions): unit =
        Utils.RunInLockedAccessMode(
            this.RootPath,
            fun () -> UpdateProcess.SmartInstall(
                            DependenciesFile.ReadFromFile(dependenciesFileName), 
                            PackageResolver.UpdateMode.Install,
                            { UpdaterOptions.Default with Common = options }))

    /// Creates a paket.dependencies file with the given text in the current directory and installs it.
    static member Install(dependencies, ?path: string, ?force, ?hard, ?withBindingRedirects, ?createNewBindingFiles, ?semVerUpdateMode) =
        let path = defaultArg path Environment.CurrentDirectory
        let fileName = Path.Combine(path, Constants.DependenciesFileName)
        File.WriteAllText(fileName, dependencies)
        let dependencies = Dependencies.Locate(path)
        dependencies.Install(
            force = defaultArg force false,
            hard = defaultArg hard false,
            withBindingRedirects = defaultArg withBindingRedirects false,
            createNewBindingFiles = defaultArg createNewBindingFiles false, 
            semVerUpdateMode = defaultArg semVerUpdateMode SemVerUpdateMode.NoRestriction)

    /// Updates all dependencies.
    member this.Update(force: bool, hard: bool): unit = this.Update(force, hard, false, false)

    /// Updates all dependencies.
    member this.Update(force: bool, hard: bool, withBindingRedirects:bool, createNewBindingFiles:bool): unit =
        this.Update(force, hard, withBindingRedirects, createNewBindingFiles, true, SemVerUpdateMode.NoRestriction)

    /// Updates all dependencies.
    member this.Update(force: bool, hard: bool, withBindingRedirects: bool, createNewBindingFiles:bool, installAfter: bool, semVerUpdateMode): unit =
        Utils.RunInLockedAccessMode(
            this.RootPath,
            fun () -> UpdateProcess.Update(
                        dependenciesFileName,
                        { UpdaterOptions.Default with
                            Common = InstallerOptions.CreateLegacyOptions(force, hard, withBindingRedirects, createNewBindingFiles, semVerUpdateMode)
                            NoInstall = installAfter |> not }))

    /// Updates dependencies in single group.
    member this.UpdateGroup(groupName, force: bool, hard: bool, withBindingRedirects: bool, createNewBindingFiles:bool, installAfter: bool, semVerUpdateMode:SemVerUpdateMode): unit =
        Utils.RunInLockedAccessMode(
            this.RootPath,
            fun () -> UpdateProcess.UpdateGroup(
                            dependenciesFileName,
                            GroupName groupName,
                            { UpdaterOptions.Default with
                                Common = InstallerOptions.CreateLegacyOptions(force, hard, withBindingRedirects, createNewBindingFiles, semVerUpdateMode)
                                NoInstall = installAfter |> not }))

    /// Update a filtered set of packages
    member this.UpdateFilteredPackages(groupName: string option, package: string, version: string option, force: bool, hard: bool, withBindingRedirects: bool, createNewBindingFiles:bool, installAfter: bool, semVerUpdateMode): unit =
        let groupName = 
            match groupName with
            | None -> Constants.MainDependencyGroup
            | Some name -> GroupName name

        Utils.RunInLockedAccessMode(
            this.RootPath,
            fun () -> UpdateProcess.UpdateFilteredPackages(dependenciesFileName, groupName, PackageName package, version,
                                                  { UpdaterOptions.Default with
                                                      Common = InstallerOptions.CreateLegacyOptions(force, hard, withBindingRedirects, createNewBindingFiles, semVerUpdateMode)
                                                      NoInstall = installAfter |> not }))

    /// Updates the given package.
    member this.UpdatePackage(groupName, package: string, version: string option, force: bool, hard: bool, semVerUpdateMode): unit =
        this.UpdatePackage(groupName, package, version, force, hard, false, false, true, semVerUpdateMode)

    /// Updates the given package.
    member this.UpdatePackage(groupName: string option, package: string, version: string option, force: bool, hard: bool, withBindingRedirects: bool, createNewBindingFiles:bool, installAfter: bool, semVerUpdateMode): unit =
        let groupName = 
            match groupName with
            | None -> Constants.MainDependencyGroup
            | Some name -> GroupName name

        Utils.RunInLockedAccessMode(
            this.RootPath,
            fun () -> UpdateProcess.UpdatePackage(dependenciesFileName, groupName, PackageName package, version,
                                                  { UpdaterOptions.Default with
                                                      Common = InstallerOptions.CreateLegacyOptions(force, hard, withBindingRedirects, createNewBindingFiles, semVerUpdateMode)
                                                      NoInstall = installAfter |> not }))

    /// Restores all dependencies.
    member this.Restore(): unit = this.Restore(false,None,[],false)

    /// Restores the given paket.references files.
    member this.Restore(group: string option, files: string list): unit = this.Restore(false, group, files, false)

    /// Restores the given paket.references files.
    member this.Restore(force: bool, group: string option, files: string list, touchAffectedRefs: bool): unit =
        Utils.RunInLockedAccessMode(
            this.RootPath,
            fun () ->
                if touchAffectedRefs then
                    let packagesToTouch = RestoreProcess.FindPackagesNotExtractedYet(dependenciesFileName)
                    this.Process (FindReferences.TouchReferencesOfPackages packagesToTouch)
                RestoreProcess.Restore(dependenciesFileName,force,Option.map GroupName group,files))

    /// Restores packages for all available paket.references files
    /// (or all packages if onlyReferenced is false)
    member this.Restore(force: bool, group: string option, onlyReferenced: bool, touchAffectedRefs: bool): unit =
        if not onlyReferenced then 
            this.Restore(force,group,[],touchAffectedRefs) 
        else
            let referencesFiles =
                this.RootPath
                |> ProjectType.FindAllProjects
                |> Array.choose (fun p -> p.FindReferencesFile())
            if Array.isEmpty referencesFiles then
                traceWarnfn "No paket.references files found for which packages could be installed."
            else 
                this.Restore(force, group, Array.toList referencesFiles, touchAffectedRefs)

    /// Lists outdated packages.
    member this.ShowOutdated(strict: bool,includePrereleases: bool): unit =
        FindOutdated.ShowOutdated strict includePrereleases |> this.Process

    /// Finds all outdated packages.
    member this.FindOutdated(strict: bool,includePrereleases: bool): (string * string * SemVerInfo) list =
        FindOutdated.FindOutdated strict includePrereleases
        |> this.Process
        |> List.map (fun (g, p,_,newVersion) -> g.ToString(),p.ToString(),newVersion)

    /// Downloads the latest paket.bootstrapper into the .paket folder.
    member this.DownloadLatestBootstrapper() : unit =
        Utils.RunInLockedAccessMode(
            this.RootPath,
            fun () -> Releases.downloadLatestBootstrapperAndTargets |> this.Process)

    /// Pulls new paket.targets and bootstrapper and puts them into .paket folder.
    member this.TurnOnAutoRestore(): unit =
        Utils.RunInLockedAccessMode(
            this.RootPath,
            fun () -> VSIntegration.TurnOnAutoRestore |> this.Process)

    /// Removes paket.targets file and Import section from project files.
    member this.TurnOffAutoRestore(): unit =
        Utils.RunInLockedAccessMode(
            this.RootPath,
            fun () -> VSIntegration.TurnOffAutoRestore |> this.Process)

    /// Returns the installed version of the given package.
    member this.GetInstalledVersion(packageName: string): string option =
        this.GetInstalledVersion(None,packageName)

    /// Returns the installed version of the given package.
    member this.GetInstalledVersion(groupName:string option,packageName: string): string option =
        let groupName = 
            match groupName with
            | None -> Constants.MainDependencyGroup
            | Some name -> GroupName name

        match getLockFile().Groups |> Map.tryFind groupName with
        | None -> None
        | Some group ->
            group.Resolution.TryFind(PackageName packageName)
            |> Option.map (fun package -> package.Version.ToString())

    /// Returns the installed versions of all installed packages.
    member this.GetInstalledPackages(): (string * string * string) list =
        getLockFile().GetGroupedResolution()
        |> listPackages

    /// Returns all sources from the dependencies file.
    member this.GetSources() =
        let dependenciesFile = DependenciesFile.ReadFromFile dependenciesFileName
        dependenciesFile.Groups
        |> Map.map (fun _ g -> g.Sources)

    /// Returns all system-wide defined NuGet feeds. (Can be used for Autocompletion)
    member this.GetDefinedNuGetFeeds() : string list =
        let configured =
            match NuGetConvert.NugetEnv.readNugetConfig(this.RootDirectory) with
            | Result.Ok(config,_) -> config.PackageSources |> Map.toList |> List.map (snd >> fst)
            | _ -> []
        Constants.DefaultNuGetStream :: configured
        |> Set.ofSeq
        |> Set.toList

    /// Returns the installed versions of all installed packages which are referenced in the references file.
    member this.GetInstalledPackages(referencesFile:ReferencesFile): (string * string * string) list =
        let lockFile = getLockFile()
        let resolved = lockFile.GetGroupedResolution()
        referencesFile
        |> lockFile.GetPackageHull
        |> Seq.map (fun kv ->
                        let groupName,packageName = kv.Key
                        groupName.ToString(),packageName.ToString(),resolved.[kv.Key].Version.ToString())
        |> Seq.toList

    /// Returns an InstallModel for the given package.
    member this.GetInstalledPackageModel(groupName,packageName) =
        match this.GetInstalledVersion(groupName,packageName) with
        | None -> failwithf "Package %s is not installed" packageName
        | Some version ->
            let groupName = 
                match groupName with
                | None -> Constants.MainDependencyGroup
                | Some name -> GroupName name

            let groupFolder = if groupName = Constants.MainDependencyGroup then "" else "/" + groupName.ToString()
            let folder = DirectoryInfo(sprintf "%s/packages%s/%s" this.RootPath groupFolder packageName)
            let nuspec = FileInfo(sprintf "%s/packages%s/%s/%s.nuspec" this.RootPath groupFolder packageName packageName)
            let nuspec = Nuspec.Load nuspec.FullName
            let files = NuGetV2.GetLibFiles(folder.FullName)
            let files = files |> Array.map (fun fi -> fi.FullName)
            InstallModel.CreateFromLibs(PackageName packageName, SemVer.Parse version, [], files, [], [], nuspec)

    /// Returns all libraries for the given package and framework.
    member this.GetLibraries(packageName,frameworkIdentifier:FrameworkIdentifier) =
        this
          .GetInstalledPackageModel(packageName)
          .GetLibReferences(frameworkIdentifier)

    /// Returns the installed versions of all direct dependencies which are referenced in the references file.
    member this.GetDirectDependencies(referencesFile:ReferencesFile): (string * string * string) list =
        let dependenciesFile = DependenciesFile.ReadFromFile dependenciesFileName
        let normalizedDependencies =
            dependenciesFile.Groups
            |> Seq.map (fun kv -> dependenciesFile.GetDependenciesInGroup(kv.Value.Name) |> Seq.map (fun kv' -> kv.Key, kv'.Key)  |> Seq.toList)
            |> List.concat

        let normalizedDependendenciesFromRefFile = 
            referencesFile.Groups 
            |> Seq.map (fun kv -> kv.Value.NugetPackages |> List.map (fun p -> kv.Key, p.Name))
            |> List.concat

        getLockFile().GetGroupedResolution()
        |> Seq.filter (fun kv -> normalizedDependendenciesFromRefFile |> Seq.exists ((=) kv.Key))
        |> Seq.filter (fun kv -> normalizedDependencies |> Seq.exists ((=) kv.Key))
        |> listPackages

    /// Returns the installed versions of all direct dependencies.
    member this.GetDirectDependencies(): (string * string * string) list =
        let dependenciesFile = DependenciesFile.ReadFromFile dependenciesFileName
        let normalizedDependencies =
            dependenciesFile.Groups
            |> Seq.map (fun kv -> dependenciesFile.GetDependenciesInGroup(kv.Value.Name) |> Seq.map (fun kv' -> kv.Key, kv'.Key)  |> Seq.toList)
            |> List.concat

        getLockFile().GetGroupedResolution()
        |> Seq.filter (fun kv -> normalizedDependencies |> Seq.exists ((=) kv.Key))
        |> listPackages

    /// Returns all groups.
    member this.GetGroups(): string list =
        let dependenciesFile = DependenciesFile.ReadFromFile dependenciesFileName
        dependenciesFile.Groups
        |> Seq.map (fun kv -> kv.Key.ToString())
        |> Seq.toList

    /// Returns the direct dependencies for the given package.
    member this.GetDirectDependenciesForPackage(groupName,packageName:string): (string * string * string) list =
        let resolvedPackages = getLockFile().GetGroupedResolution()
        let package = resolvedPackages.[groupName, (PackageName packageName)]
        let normalizedDependencies = package.Dependencies |> Seq.map (fun (name,_,_) -> groupName, name) |> Seq.toList

        resolvedPackages
        |> Seq.filter (fun kv -> normalizedDependencies |> Seq.exists ((=) kv.Key))
        |> listPackages

    /// Removes the given package from the main dependency group of the dependencies file.
    member this.Remove(package: string): unit = this.Remove(None, package)

    /// Removes the given package from dependencies file.
    member this.Remove(groupName, package: string): unit = this.Remove(groupName, package, false, false, false, true)

    /// Removes the given package from dependencies file.
    member this.Remove(groupName, package: string, force: bool,hard: bool,interactive: bool,installAfter: bool): unit =
        Utils.RunInLockedAccessMode(
            this.RootPath,
            fun () -> RemoveProcess.Remove(dependenciesFileName, groupName, PackageName package, force, hard, interactive, installAfter))

    /// Removes the given package from the specified project
    member this.RemoveFromProject(groupName,package: string,force: bool,hard: bool,projectName: string,installAfter: bool): unit =
        Utils.RunInLockedAccessMode(
            this.RootPath,
            fun () -> RemoveProcess.RemoveFromProject(dependenciesFileName, groupName, PackageName package, force, hard, projectName, installAfter))

    /// Shows all references files where the given package is referenced.
    member this.ShowReferencesFor(packages: (string * string) list): unit =
        FindReferences.ShowReferencesFor (packages |> List.map (fun (g,p) -> GroupName g,PackageName p)) |> this.Process 

    /// Finds all references files where the given main group package is referenced.
    member this.FindReferencesFor(package:string): string list =
        this.FindReferencesFor(Constants.MainDependencyGroup.ToString(),package)

    /// Finds all references files where the given package is referenced.
    member this.FindReferencesFor(group:string,package:string): string list =
        FindReferences.FindReferencesForPackage (GroupName group) (PackageName package) |> this.Process |> List.map (fun p -> p.FileName)

    static member private FindPackagesByNameAsync(sources:PackageSource seq,searchTerm,?maxResults) =
        let maxResults = defaultArg maxResults 1000
        let sources = sources |> Seq.toList |> List.distinct
        match sources with
        | [] -> [PackageSources.DefaultNuGetSource]
        | _ -> sources
        |> Seq.distinct
        |> Seq.choose (fun source -> 
            match source with 
            | NuGetV2 s ->
                match NuGetV3.getSearchAPI(s.Authentication,s.Url) with
                | Some _ -> Some(NuGetV3.FindPackages(s.Authentication, s.Url, searchTerm, maxResults))
                | None ->  Some(NuGetV2.FindPackages(s.Authentication, s.Url, searchTerm, maxResults))
            | NuGetV3 s -> Some(NuGetV3.FindPackages(s.Authentication, s.Url, searchTerm, maxResults))
            | LocalNuGet s -> 
                Some(async {
                    return
                        Fake.Globbing.search s (sprintf "**/*%s*" searchTerm)
                        |> List.distinctBy (fun s -> 
                            let parts = FileInfo(s).Name.Split('.')
                            let nameParts = parts |> Seq.takeWhile (fun x -> x <> "nupkg" && System.Int32.TryParse x |> fst |> not)
                            String.Join(".",nameParts).ToLower())
                        |> List.map NuGetV2.getPackageNameFromLocalFile
                        |> List.toArray
                }))
   
    static member FindPackagesByName(sources:PackageSource seq,searchTerm,?maxResults) =
        let maxResults = defaultArg maxResults 1000
        Dependencies.FindPackagesByNameAsync(sources,searchTerm,maxResults)
        |> Async.Parallel
        |> Async.RunSynchronously
        |> Seq.concat
        |> Seq.toList
        |> List.distinct

    static member SearchPackagesByName(sources:PackageSource seq,searchTerm,?cancellationToken,?maxResults) : IObservable<string> =
        let cancellationToken = defaultArg cancellationToken (System.Threading.CancellationToken())
        let maxResults = defaultArg maxResults 1000
        Dependencies.FindPackagesByNameAsync(sources,searchTerm,maxResults)
        |> Seq.map (Observable.ofAsyncWithToken cancellationToken)
        |> Seq.reduce Observable.merge
        |> Observable.flatten
        |> Observable.distinct

    member this.SearchPackagesByName(searchTerm,?cancellationToken,?maxResults) : IObservable<string> =
        let cancellationToken = defaultArg cancellationToken (System.Threading.CancellationToken())
        let maxResults = defaultArg maxResults 1000
        Dependencies.SearchPackagesByName(
            this.GetSources() |> Seq.map (fun kv -> kv.Value) |> Seq.concat,
            searchTerm,
            cancellationToken,
            maxResults)

    static member FindPackageVersions(root,sources:PackageSource seq,name:string,?maxResults) =
        let maxResults = defaultArg maxResults 1000
        let sources = 
            match sources |> Seq.toList |> List.distinct with
            | [] -> [PackageSources.DefaultNuGetSource]
            | sources -> sources
            |> List.distinct

        let versions = 
            NuGetV2.GetVersions true root (sources, PackageName name)
            |> List.map (fun (v,_) -> v.ToString())
            |> List.toArray
            |> SemVer.SortVersions

        if versions.Length > maxResults then Array.take maxResults versions else versions
        

    member this.FindPackageVersions(sources:PackageSource seq,name:string,?maxResults) =
        let maxResults = defaultArg maxResults 1000
        Dependencies.FindPackageVersions(this.RootPath,sources,name,maxResults)

    /// Finds all projects where the given package is referenced.
    member this.FindProjectsFor(group:string,package: string): ProjectFile list =
        FindReferences.FindReferencesForPackage (GroupName group) (PackageName package) 
        |> this.Process
        |> List.choose (fun p ->
            match p with
            | ProjectType.Project p -> Some p
            | _ -> None)

    // Packs all paket.template files.
    member this.Pack(outputPath, ?buildConfig, ?buildPlatform, ?version, ?specificVersions, ?releaseNotes, ?templateFile, ?workingDir, ?excludedTemplates, ?lockDependencies, ?minimumFromLockFile, ?symbols, ?includeReferencedProjects, ?projectUrl) =
        let dependenciesFile = DependenciesFile.ReadFromFile dependenciesFileName
        let specificVersions = defaultArg specificVersions Seq.empty
        let workingDir = defaultArg workingDir (dependenciesFile.FileName |> Path.GetDirectoryName)
        let lockDependencies = defaultArg lockDependencies false
        let minimumFromLockFile = defaultArg minimumFromLockFile false
        let symbols = defaultArg symbols false
        let includeReferencedProjects = defaultArg includeReferencedProjects false
        let projectUrl = defaultArg (Some(projectUrl)) None
        PackageProcess.Pack(workingDir, dependenciesFile, outputPath, buildConfig, buildPlatform, version, specificVersions, releaseNotes, templateFile, excludedTemplates, lockDependencies, minimumFromLockFile, symbols, includeReferencedProjects, projectUrl)

    /// Pushes a nupkg file.
    static member Push(packageFileName, ?url, ?apiKey, (?endPoint: string), ?maxTrials) =
        let urlWithEndpoint = RemoteUpload.GetUrlWithEndpoint url endPoint
        let apiKey = defaultArg apiKey (Environment.GetEnvironmentVariable("nugetkey"))
        if String.IsNullOrEmpty apiKey then
            failwithf "Could not push package %s. Please specify a NuGet API key via environment variable \"nugetkey\"." packageFileName
        let maxTrials = defaultArg maxTrials 5
        RemoteUpload.Push maxTrials urlWithEndpoint apiKey packageFileName

    /// Lists all paket.template files in the current solution.
    member this.ListTemplateFiles() : TemplateFile list =
        let lockFile = getLockFile()
        ProjectType.FindAllProjects(this.RootPath)
        |> Array.choose (fun proj -> proj.FindTemplatesFile())
        |> Array.choose (fun path ->
                         try
                           Some(TemplateFile.Load(path, lockFile, None, Map.empty))
                         with
                           | _ -> None)
        |> Array.toList
