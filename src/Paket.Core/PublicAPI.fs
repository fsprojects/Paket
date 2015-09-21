namespace Paket

open Paket.Domain
open Paket.Logging
open Paket.PackageSources

open System
open System.IO
open Chessie.ErrorHandling

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
                if parent = null then
                    if withError then
                        failwithf "Could not find '%s'. To use Paket with this solution, please run 'paket init' first." Constants.DependenciesFileName
                    else
                        Constants.DependenciesFileName
                else
                   findInPath(parent, withError)

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
        let rootDirectory = defaultArg (PaketEnv.locatePaketRootDirectory(dir)) dir

        Utils.RunInLockedAccessMode(
            rootDirectory.FullName,
            fun () ->
                NuGetConvert.convertR rootDirectory force credsMigrationMode
                |> returnOrFail
                |> NuGetConvert.replaceNugetWithPaket initAutoRestore installAfter
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

    /// Adds the given package with the given version to the dependencies file.
    member this.Add(groupName, package: string,version: string): unit =
        this.Add(groupName, package, version, force = false, hard = false, withBindingRedirects = false,  createNewBindingFiles = false, interactive = false, installAfter = true)

    /// Adds the given package with the given version to the dependencies file.
    member this.Add(groupName, package: string,version: string,force: bool,hard: bool,withBindingRedirects: bool, createNewBindingFiles:bool, interactive: bool,installAfter: bool): unit =
        Utils.RunInLockedAccessMode(
            this.RootPath,
            fun () -> AddProcess.Add(dependenciesFileName, groupName, PackageName(package.Trim()), version,
                                     InstallerOptions.createLegacyOptions(force, hard, withBindingRedirects, createNewBindingFiles),
                                     interactive, installAfter))

   /// Adds the given package with the given version to the dependencies file.
    member this.AddToProject(groupName, package: string,version: string,force: bool,hard: bool,withBindingRedirects: bool, createNewBindingFiles:bool, projectName: string,installAfter: bool): unit =
        Utils.RunInLockedAccessMode(
            this.RootPath,
            fun () -> AddProcess.AddToProject(dependenciesFileName, groupName, PackageName package, version,
                                              InstallerOptions.createLegacyOptions(force, hard, withBindingRedirects, createNewBindingFiles),
                                              projectName, installAfter))

    /// Adds credentials for a Nuget feed
    member this.AddCredentials(source: string, username: string) : unit =
        Utils.RunInLockedAccessMode(
            this.RootPath,
            fun () -> ConfigFile.askAndAddAuth source username |> returnOrFail )

    /// Installs all dependencies.
    member this.Install(force: bool, hard: bool) = this.Install(force, hard, false, false)

    /// Installs all dependencies.
    member this.Install(force: bool, hard: bool, withBindingRedirects: bool, createNewBindingFiles:bool): unit =
        this.Install(force, hard, withBindingRedirects, createNewBindingFiles, false)

    /// Installs all dependencies.
    member this.Install(force: bool, hard: bool, withBindingRedirects: bool, createNewBindingFiles:bool, onlyReferenced: bool): unit =
        this.Install({ InstallerOptions.createLegacyOptions(force, hard, withBindingRedirects, createNewBindingFiles) with OnlyReferenced = onlyReferenced })

    /// Installs all dependencies.
    member private this.Install(options: InstallerOptions): unit =
        Utils.RunInLockedAccessMode(
            this.RootPath,
            fun () -> UpdateProcess.SmartInstall(DependenciesFile.ReadFromFile(dependenciesFileName), false, None,
                                                 { UpdaterOptions.Default with Common = options }))

    /// Creates a paket.dependencies file with the given text in the current directory and installs it.
    static member Install(dependencies, ?path: string, ?force, ?hard, ?withBindingRedirects, ?createNewBindingFiles) =
        let path = defaultArg path Environment.CurrentDirectory
        let fileName = Path.Combine(path, Constants.DependenciesFileName)
        File.WriteAllText(fileName, dependencies)
        let dependencies = Dependencies.Locate(path)
        dependencies.Install(
            force = defaultArg force false,
            hard = defaultArg hard false,
            withBindingRedirects = defaultArg withBindingRedirects false,
            createNewBindingFiles = defaultArg createNewBindingFiles false)

    /// Updates all dependencies.
    member this.Update(force: bool, hard: bool): unit = this.Update(force, hard, false, false)

    /// Updates all dependencies.
    member this.Update(force: bool, hard: bool, withBindingRedirects:bool, createNewBindingFiles:bool): unit =
        this.Update(force, hard, withBindingRedirects, createNewBindingFiles, true)

    /// Updates all dependencies.
    member this.Update(force: bool, hard: bool, withBindingRedirects: bool, createNewBindingFiles:bool, installAfter: bool): unit =
        Utils.RunInLockedAccessMode(
            this.RootPath,
            fun () -> UpdateProcess.Update(dependenciesFileName,
                                           { UpdaterOptions.Default with
                                               Common = InstallerOptions.createLegacyOptions(force, hard, withBindingRedirects, createNewBindingFiles)
                                               NoInstall = installAfter |> not }))

    /// Updates the given package.
    member this.UpdatePackage(groupName, package: string, version: string option, force: bool, hard: bool): unit =
        this.UpdatePackage(groupName, package, version, force, hard, false, false, true)

    /// Updates the given package.
    member this.UpdatePackage(groupName: string option, package: string, version: string option, force: bool, hard: bool, withBindingRedirects: bool, createNewBindingFiles:bool, installAfter: bool): unit =
        let groupName = 
            match groupName with
            | None -> Constants.MainDependencyGroup
            | Some name -> GroupName name

        Utils.RunInLockedAccessMode(
            this.RootPath,
            fun () -> UpdateProcess.UpdatePackage(dependenciesFileName, groupName, PackageName package, version,
                                                  { UpdaterOptions.Default with
                                                      Common = InstallerOptions.createLegacyOptions(force, hard, withBindingRedirects, createNewBindingFiles)
                                                      NoInstall = installAfter |> not }))

    /// Restores all dependencies.
    member this.Restore(): unit = this.Restore(false,None,[])

    /// Restores the given paket.references files.
    member this.Restore(group: string option, files: string list): unit = this.Restore(false, group, files)

    /// Restores the given paket.references files.
    member this.Restore(force: bool, group: string option, files: string list): unit =
        Utils.RunInLockedAccessMode(
            this.RootPath,
            fun () -> RestoreProcess.Restore(dependenciesFileName,force,Option.map GroupName group,files))

    /// Restores packages for all available paket.references files
    /// (or all packages if onlyReferenced is false)
    member this.Restore(force: bool, group: string option, onlyReferenced: bool): unit =
        if not onlyReferenced then 
            this.Restore(force,group,[]) 
        else
            let referencesFiles =
                this.RootPath
                |> ProjectFile.FindAllProjects
                |> Array.choose (fun p -> ProjectFile.FindReferencesFile(FileInfo(p.FileName)))
            if Array.isEmpty referencesFiles then
                traceWarnfn "No paket.references files found for which packages could be installed."
            else 
                this.Restore(force, group, Array.toList referencesFiles)

    /// Lists outdated packages.
    member this.ShowOutdated(strict: bool,includePrereleases: bool): unit =
        FindOutdated.ShowOutdated strict includePrereleases |> this.Process

    /// Finds all outdated packages.
    member this.FindOutdated(strict: bool,includePrereleases: bool): (string * string * SemVerInfo) list =
        FindOutdated.FindOutdated strict includePrereleases
        |> this.Process
        |> List.map (fun (GroupName g, PackageName p,_,newVersion) -> g,p,newVersion)

    /// Downloads the latest paket.bootstrapper into the .paket folder.
    member this.DownloadLatestBootstrapper() : unit =
        Utils.RunInLockedAccessMode(
            this.RootPath,
            fun () -> Releases.downloadLatestBootstrapper |> this.Process)

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
            | Result.Ok(config,_) -> config.PackageSources |> List.map fst
            | _ -> []
        Constants.DefaultNugetStream :: configured
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

    /// Returns the direct dependencies for the given package.
    member this.GetDirectDependenciesForPackage(groupName,packageName:string): (string * string * string) list =
        let resolvedPackages = getLockFile().GetGroupedResolution()
        let package = resolvedPackages.[groupName, (PackageName packageName)]
        let normalizedDependencies = package.Dependencies |> Seq.map (fun (name,_,_) -> groupName, name) |> Seq.toList

        resolvedPackages
        |> Seq.filter (fun kv -> normalizedDependencies |> Seq.exists ((=) kv.Key))
        |> listPackages

    /// Removes the given package from dependencies file.
    member this.Remove(groupName, package: string): unit = this.Remove(groupName, package, false, false, false, true)

    /// Removes the given package from dependencies file.
    member this.Remove(groupName, package: string,force: bool,hard: bool,interactive: bool,installAfter: bool): unit =
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

    /// Finds all references files where the given package is referenced.
    member this.FindReferencesFor(group:string,package:string): string list =
        FindReferences.FindReferencesForPackage (GroupName group) (PackageName package) |> this.Process |> List.map (fun p -> p.FileName)

    member this.SearchPackagesByName(searchTerm,?cancellationToken,?maxResults) : IObservable<string> =
        let cancellationToken = defaultArg cancellationToken (System.Threading.CancellationToken())
        let maxResults = defaultArg maxResults 1000
        let sources = this.GetSources() |> Seq.map (fun kv -> kv.Value) |> List.concat |> List.distinct
        if sources = [] then [PackageSources.DefaultNugetSource] else sources
        |> List.choose (fun x -> match x with | Nuget s -> Some s.Url | _ -> None)
        |> Seq.distinct
        |> Seq.map (fun url ->
                    NuGetV3.FindPackages(None, url, searchTerm, maxResults)
                    |> Observable.ofAsyncWithToken cancellationToken)
        |> Seq.reduce Observable.merge
        |> Observable.flatten
        |> Observable.distinct

    /// Finds all projects where the given package is referenced.
    member this.FindProjectsFor(group:string,package: string): ProjectFile list =
        FindReferences.FindReferencesForPackage (GroupName group) (PackageName package) |> this.Process

    // Packs all paket.template files.
    member this.Pack(outputPath, ?buildConfig, ?version, ?releaseNotes, ?templateFile, ?workingDir, ?lockDependencies) =
        let dependenciesFile = DependenciesFile.ReadFromFile dependenciesFileName
        let workingDir = defaultArg workingDir (dependenciesFile.FileName |> Path.GetDirectoryName)
        let lockDependencies = defaultArg lockDependencies false
        PackageProcess.Pack(workingDir, dependenciesFile, outputPath, buildConfig, version, releaseNotes, templateFile, lockDependencies)

    /// Pushes a nupkg file.
    static member Push(packageFileName, ?url, ?apiKey, (?endPoint: string), ?maxTrials) =
        let urlWithEndpoint = RemoteUpload.GetUrlWithEndpoint url endPoint
        let apiKey = defaultArg apiKey (Environment.GetEnvironmentVariable("nugetkey"))
        if String.IsNullOrEmpty apiKey then
            failwithf "Could not push package %s. Please specify a NuGet API key via environment variable \"nugetkey\"." packageFileName
        let maxTrials = defaultArg maxTrials 5
        RemoteUpload.Push maxTrials urlWithEndpoint apiKey packageFileName
