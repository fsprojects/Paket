namespace Paket

open Paket.Domain
open Paket.Logging
open Paket.PackageSources

open System
open System.Xml
open System.IO
open Chessie.ErrorHandling
open PackageResolver
open Requirements

/// Paket API which is optimized for F# Interactive use.
type Dependencies(dependenciesFileName: string) =
    let listPackages (packages: System.Collections.Generic.KeyValuePair<GroupName*PackageName, PackageResolver.PackageInfo> seq) =
        packages
        |> Seq.map (fun kv ->
                let groupName,packageName = kv.Key
                groupName.ToString(),packageName.ToString(),kv.Value.Version.ToString())
        |> Seq.toList

    /// Clears the NuGet cache
    static member ClearCache(?clearLocalCache) =
        let emptyDir path =
            tracefn "  - %s" path
            emptyDir (DirectoryInfo path)

        if clearLocalCache |> Option.defaultValue false then
            match Dependencies.TryLocate() with
            | None -> ()
            | Some dependencies ->
                RunInLockedAccessMode(
                    Path.Combine(dependencies.RootPath,Constants.PaketFilesFolderName),
                    fun () ->
                        emptyDir (Path.Combine(dependencies.RootPath,Constants.DefaultPackagesFolderName))
                        emptyDir (Path.Combine(dependencies.RootPath,Constants.PaketFilesFolderName))
                        false
                )

        emptyDir Constants.UserNuGetPackagesFolder
        emptyDir Constants.NuGetCacheFolder
        emptyDir Constants.GitRepoCacheFolder

    /// Tries to locate the paket.dependencies file in the current folder or a parent folder, throws an exception if unsuccessful.
    static member Locate(): Dependencies = Dependencies.Locate(Directory.GetCurrentDirectory())

    /// Tries to locate the paket.dependencies file in the current folder or a parent folder.
    static member TryLocate(): Dependencies option = Dependencies.TryLocate(Directory.GetCurrentDirectory())

    /// Returns an instance of the paket.lock file.
    member __.GetLockFile() =
        let lockFileName = DependenciesFile.FindLockfile dependenciesFileName
        LockFile.LoadFrom(lockFileName.FullName)

    /// Returns an instance of the paket.dependencies file.
    member __.GetDependenciesFile() = DependenciesFile.ReadFromFile dependenciesFileName

    /// Tries to locate the paket.dependencies file in the given folder or a parent folder, throws an exception if unsuccessful.
    static member Locate(path: string): Dependencies =
        match Dependencies.TryLocate path with
        | None ->
            failwithf "Could not find '%s'. To use Paket with this solution, please run 'paket init' first.%sIf you have already run 'paket.init' then ensure that '%s' is located in the top level directory of your repository.%sLike this:%sMySourceDir%s  .paket%s  paket.dependencies"
                Constants.DependenciesFileName Environment.NewLine Constants.DependenciesFileName Environment.NewLine Environment.NewLine Environment.NewLine Environment.NewLine
        | Some d ->
            d

    /// Tries to locate the paket.dependencies file in the given folder or a parent folder.
    static member TryLocate(path: string): Dependencies option =
        let rec findInPath(dir:DirectoryInfo) =
            let path = Path.Combine(dir.FullName,Constants.DependenciesFileName)
            match File.Exists path, dir.Parent with
            | true, _ -> Some path
            | false, null -> None
            | false, parent -> findInPath parent

        findInPath (DirectoryInfo path)
        |> Option.map (fun dependenciesFileName ->
            if verbose then verbosefn "found: %s" dependenciesFileName
            Dependencies(dependenciesFileName))

    /// Initialize paket.dependencies file in current directory
    static member Init() = Dependencies.Init(Directory.GetCurrentDirectory())

    /// Initialize paket.dependencies file in the given directory
    static member Init(directory) =
        let directory = DirectoryInfo(directory)

        RunInLockedAccessMode(
            Path.Combine(directory.FullName,Constants.PaketFilesFolderName),
            fun () ->
                PaketEnv.init directory
                |> returnOrFail
                false
        )

#if !NO_BOOTSTRAPPER
        let deps = Dependencies.Locate()
        deps.DownloadLatestBootstrapper()
#endif

    /// Initialize paket.dependencies file in the given directory
    static member Init(directory, sources, additional, downloadBootstrapper) =
        let directory = DirectoryInfo(directory)

        RunInLockedAccessMode(
            Path.Combine(directory.FullName,Constants.PaketFilesFolderName),
            fun () ->
                PaketEnv.initWithContent sources additional directory
                |> returnOrFail
                false
        )

#if !NO_BOOTSTRAPPER
        if downloadBootstrapper then
            let deps = Dependencies.Locate(directory.FullName)
            deps.DownloadLatestBootstrapper()
#else
        ignore downloadBootstrapper
#endif

    /// Converts the solution from NuGet to Paket.
    static member ConvertFromNuget(force: bool,installAfter: bool, initAutoRestore: bool,credsMigrationMode: string option, ?directory: DirectoryInfo) : unit =
        let dir = defaultArg directory (DirectoryInfo(Directory.GetCurrentDirectory()))
        let rootDirectory = dir

        RunInLockedAccessMode(
            Path.Combine(rootDirectory.FullName,Constants.PaketFilesFolderName),
            fun () ->
                NuGetConvert.convertR rootDirectory force credsMigrationMode
                |> returnOrFail
                |> NuGetConvert.replaceNuGetWithPaket initAutoRestore installAfter
        )

    /// Converts the current package dependency graph to the simplest dependency graph.
    member this.Simplify(interactive : bool) =
        RunInLockedAccessMode(
            Path.Combine(this.RootPath,Constants.PaketFilesFolderName),
            fun () ->
                PaketEnv.fromRootDirectory this.RootDirectory
                >>= PaketEnv.ensureNotInStrictMode
                >>= Simplifier.simplify interactive
                |> returnOrFail
                |> Simplifier.updateEnvironment
                false
        )

    /// Get path to dependencies file
    member __.DependenciesFile with get() = dependenciesFileName

    /// Get the root path
    member __.RootPath with get() = Path.GetDirectoryName(dependenciesFileName)

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
        this.Add(groupName, package, version, force = false,  withBindingRedirects = false, cleanBindingRedirects = false, createNewBindingFiles = false, interactive = false, installAfter = true, semVerUpdateMode = SemVerUpdateMode.NoRestriction, touchAffectedRefs = false)

    /// Adds the given package with the given version to the dependencies file.
    member this.Add(groupName: string option, package: string,version: string,force: bool, withBindingRedirects: bool, cleanBindingRedirects: bool,  createNewBindingFiles:bool, interactive: bool, installAfter: bool, semVerUpdateMode, touchAffectedRefs): unit =
        this.Add(groupName, package,version,force, withBindingRedirects, cleanBindingRedirects,  createNewBindingFiles, interactive, installAfter, semVerUpdateMode, touchAffectedRefs, true)

    /// Adds the given package with the given version to the dependencies file.
    member this.Add(groupName: string option, package: string,version: string,force: bool, withBindingRedirects: bool, cleanBindingRedirects: bool,  createNewBindingFiles:bool, interactive: bool, installAfter: bool, semVerUpdateMode, touchAffectedRefs, runResolver:bool): unit =
        this.Add(groupName, package,version,force, withBindingRedirects, cleanBindingRedirects,  createNewBindingFiles, interactive, installAfter, semVerUpdateMode, touchAffectedRefs, runResolver, Requirements.PackageRequirementKind.Package)

    /// Adds the given package with the given version to the dependencies file.
    member this.Add(groupName: string option, package: string,version: string,force: bool, withBindingRedirects: bool, cleanBindingRedirects: bool,  createNewBindingFiles:bool, interactive: bool, installAfter: bool, semVerUpdateMode, touchAffectedRefs, runResolver:bool, packageKind:Requirements.PackageRequirementKind): unit =
        let withBindingRedirects = if withBindingRedirects then BindingRedirectsSettings.On else BindingRedirectsSettings.Off
        RunInLockedAccessMode(
            Path.Combine(this.RootPath,Constants.PaketFilesFolderName),
            fun () ->
                    AddProcess.Add(dependenciesFileName, groupName, PackageName(package.Trim()), version,
                                     InstallerOptions.CreateLegacyOptions(force, withBindingRedirects, cleanBindingRedirects, createNewBindingFiles, semVerUpdateMode, touchAffectedRefs, false, [], [], None),
                                     interactive, installAfter, runResolver, packageKind))

    /// Adds the given github repository to the dependencies file.
    member this.AddGithub(groupName, repository, file) =
        this.AddGithub(groupName, repository, file, "")

    /// Adds the given github repository to the dependencies file.
    member this.AddGithub(groupName, repository, file, version) =
        this.AddGithub(groupName, repository, file, version, InstallerOptions.Default)

    /// Adds the given github repository to the dependencies file.
    member this.AddGithub(groupName, repository, file, version, options) =
        RunInLockedAccessMode(
            Path.Combine(this.RootPath,Constants.PaketFilesFolderName),
            fun () ->
                AddProcess.AddGithub(dependenciesFileName, groupName, repository, file, version, options); false)

    /// Adds the given git repository to the dependencies file
    member this.AddGit(groupName, repository) =
        this.AddGit(groupName, repository, "")

    /// Adds the given git repository to the dependencies file
    member this.AddGit(groupName, repository, version) =
        this.AddGit(groupName, repository, version, InstallerOptions.Default);

    /// Adds the given git repository to the dependencies file
    member this.AddGit(groupName, repository, version, options) =
        RunInLockedAccessMode(
            this.RootPath,
            fun () ->
                AddProcess.AddGit(dependenciesFileName, groupName, repository, version, options); false)

   /// Adds the given package with the given version to the dependencies file.
    member this.AddToProject(groupName, package: string,version: string,force: bool, withBindingRedirects: bool, cleanBindingRedirects: bool, createNewBindingFiles:bool, projectName: string, installAfter: bool, semVerUpdateMode, touchAffectedRefs): unit =
        this.AddToProject(groupName, package,version,force, withBindingRedirects, cleanBindingRedirects, createNewBindingFiles, projectName, installAfter, semVerUpdateMode, touchAffectedRefs, true)

    /// Adds the given package with the given version to the dependencies file.
    member this.AddToProject(groupName, package: string,version: string,force: bool, withBindingRedirects: bool, cleanBindingRedirects: bool, createNewBindingFiles:bool, projectName: string, installAfter: bool, semVerUpdateMode, touchAffectedRefs, runResolver:bool): unit =
        this.AddToProject(groupName, package,version,force, withBindingRedirects, cleanBindingRedirects, createNewBindingFiles, projectName, installAfter, semVerUpdateMode, touchAffectedRefs, runResolver, Requirements.PackageRequirementKind.Package)

    /// Adds the given package with the given version to the dependencies file.
    member this.AddToProject(groupName, package: string,version: string,force: bool, withBindingRedirects: bool, cleanBindingRedirects: bool, createNewBindingFiles:bool, projectName: string, installAfter: bool, semVerUpdateMode, touchAffectedRefs, runResolver:bool, packageKind:Requirements.PackageRequirementKind): unit =
        let withBindingRedirects = if withBindingRedirects then BindingRedirectsSettings.On else BindingRedirectsSettings.Off
        RunInLockedAccessMode(
            Path.Combine(this.RootPath,Constants.PaketFilesFolderName),
            fun () -> AddProcess.AddToProject(dependenciesFileName, groupName, PackageName package, version,
                                              InstallerOptions.CreateLegacyOptions(force, withBindingRedirects, cleanBindingRedirects, createNewBindingFiles, semVerUpdateMode, touchAffectedRefs, false, [], [], None),
                                              projectName, installAfter, runResolver, packageKind))

    /// Adds credentials for a Nuget feed
    member this.AddCredentials(source: string, username: string, password : string, authType : string) : unit =
        RunInLockedAccessMode(
            Path.Combine(this.RootPath,Constants.PaketFilesFolderName),
            fun () -> ConfigFile.askAndAddAuth source username password authType false |> returnOrFail; false)

     /// Adds credentials for a Nuget feed
    member this.AddCredentials(source: string, username: string, password : string, authType : string, verify : bool) : unit =
        RunInLockedAccessMode(
            Path.Combine(this.RootPath,Constants.PaketFilesFolderName),
            fun () -> ConfigFile.askAndAddAuth source username password authType verify |> returnOrFail; false)

    /// Adds a token for a source
    member this.AddToken(source : string, token : string) : unit =
        RunInLockedAccessMode(
            Path.Combine(this.RootPath,Constants.PaketFilesFolderName),
            fun () -> ConfigFile.AddToken(source, token) |> returnOrFail; false)

    /// Installs all dependencies.
    member this.Install(force: bool) = this.Install(force, false, false, false, false, SemVerUpdateMode.NoRestriction, false, false, [], [], None)

    /// Installs all dependencies.
    member this.Install(force: bool, withBindingRedirects: bool, cleanBindingRedirects: bool, createNewBindingFiles:bool, onlyReferenced: bool, semVerUpdateMode, touchAffectedRefs, generateLoadScripts, providedFrameworks, providedScriptTypes, alternativeProjectRoot): unit =
        let withBindingRedirects = if withBindingRedirects then BindingRedirectsSettings.On else BindingRedirectsSettings.Off
        this.Install({ InstallerOptions.CreateLegacyOptions(force, withBindingRedirects, cleanBindingRedirects, createNewBindingFiles, semVerUpdateMode, touchAffectedRefs, generateLoadScripts, providedFrameworks, providedScriptTypes, alternativeProjectRoot) with OnlyReferenced = onlyReferenced })

    /// Installs all dependencies.
    member private this.Install(options: InstallerOptions): unit =
        RunInLockedAccessMode(
            Path.Combine(this.RootPath,Constants.PaketFilesFolderName),
            fun () -> UpdateProcess.SmartInstall(
                            DependenciesFile.ReadFromFile(dependenciesFileName),
                            PackageResolver.UpdateMode.Install,
                            { UpdaterOptions.Default with Common = options }))

    /// Creates a paket.dependencies file with the given text in the current directory and installs it.
    static member Install(dependencies, ?path: string, ?force, ?withBindingRedirects, ?cleanBindingRedirects, ?createNewBindingFiles, ?onlyReferenced, ?semVerUpdateMode, ?touchAffectedRefs, ?generateLoadScripts, ?providedFrameworks, ?providedScriptTypes) =
        let path = defaultArg path (Directory.GetCurrentDirectory())
        let fileName = Path.Combine(path, Constants.DependenciesFileName)
        File.WriteAllText(fileName, dependencies)
        let dependencies = Dependencies.Locate(path)
        dependencies.Install(
            force = defaultArg force false,
            withBindingRedirects = defaultArg withBindingRedirects false,
            cleanBindingRedirects = defaultArg cleanBindingRedirects false,
            createNewBindingFiles = defaultArg createNewBindingFiles false,
            onlyReferenced = defaultArg onlyReferenced false,
            semVerUpdateMode = defaultArg semVerUpdateMode SemVerUpdateMode.NoRestriction,
            touchAffectedRefs = defaultArg touchAffectedRefs false,
            generateLoadScripts = defaultArg generateLoadScripts false,
            providedFrameworks = defaultArg providedFrameworks [],
            providedScriptTypes = defaultArg providedScriptTypes [],
            alternativeProjectRoot = None)


    member __.GenerateLoadScriptData (paketDependencies:string) (groups:string list) (frameworks:string list) (scriptTypes:string list) =
        let depCache = DependencyCache paketDependencies
        LoadingScripts.ScriptGeneration.constructScriptsFromData depCache (groups|>List.map GroupName) frameworks scriptTypes
        |> List.ofSeq

    member this.GenerateLoadScripts (groups:string list) (frameworks:string list) (scriptTypes:string list)  =
        for sd in this.GenerateLoadScriptData this.DependenciesFile groups frameworks scriptTypes do
            let rootDir = this.RootDirectory
            Directory.CreateDirectory (Path.Combine(Constants.PaketFolderName,"load")) |> ignore
            let scriptPath = Path.Combine (rootDir.FullName , sd.PartialPath)
            if verbose then
                verbosefn "scriptpath - %s" scriptPath
            if verbose then
                verbosefn "created - '%s'" (Path.Combine(rootDir.FullName , sd.PartialPath))
            sd.Save rootDir

    /// Updates all dependencies.
    member this.Update(force: bool): unit =
        this.Update(force, false, false, false)

    /// Updates all dependencies.
    member this.Update(force: bool, withBindingRedirects:bool, cleanBindingRedirects: bool, createNewBindingFiles:bool): unit =
        this.Update(force, withBindingRedirects, cleanBindingRedirects, createNewBindingFiles, true, SemVerUpdateMode.NoRestriction, false)

    /// Updates all dependencies.
    member this.Update(force: bool, withBindingRedirects: bool, cleanBindingRedirects: bool, createNewBindingFiles:bool, installAfter: bool, semVerUpdateMode, touchAffectedRefs): unit =
        let withBindingRedirects = if withBindingRedirects then BindingRedirectsSettings.On else BindingRedirectsSettings.Off
        RunInLockedAccessMode(
            Path.Combine(this.RootPath,Constants.PaketFilesFolderName),
            fun () ->
            UpdateProcess.Update(
                dependenciesFileName,
                    { UpdaterOptions.Default with
                        Common = InstallerOptions.CreateLegacyOptions(force, withBindingRedirects, cleanBindingRedirects, createNewBindingFiles, semVerUpdateMode, touchAffectedRefs, false, [], [], None)
                        NoInstall = installAfter |> not
                    }
            )
        )

    /// Updates dependencies in single group.
    member this.UpdateGroup(groupName, force: bool, withBindingRedirects: bool, cleanBindingRedirects: bool, createNewBindingFiles:bool, installAfter: bool, semVerUpdateMode:SemVerUpdateMode, touchAffectedRefs): unit =
        let withBindingRedirects = if withBindingRedirects then BindingRedirectsSettings.On else BindingRedirectsSettings.Off
        RunInLockedAccessMode(
            Path.Combine(this.RootPath,Constants.PaketFilesFolderName),
            fun () -> UpdateProcess.UpdateGroup(
                            dependenciesFileName,
                            GroupName groupName,
                            { UpdaterOptions.Default with
                                Common = InstallerOptions.CreateLegacyOptions(force, withBindingRedirects, cleanBindingRedirects, createNewBindingFiles, semVerUpdateMode, touchAffectedRefs, false, [], [], None)
                                NoInstall = installAfter |> not }))

    /// Update a filtered set of packages
    member this.UpdateFilteredPackages(groupName: string option, package: string, version: string option, force: bool, withBindingRedirects: bool, cleanBindingRedirects: bool, createNewBindingFiles:bool, installAfter: bool, semVerUpdateMode, touchAffectedRefs): unit =
        let groupName =
            match groupName with
            | None -> Constants.MainDependencyGroup
            | Some name -> GroupName name
        let withBindingRedirects = if withBindingRedirects then BindingRedirectsSettings.On else BindingRedirectsSettings.Off

        RunInLockedAccessMode(
            Path.Combine(this.RootPath,Constants.PaketFilesFolderName),
            fun () -> UpdateProcess.UpdateFilteredPackages(dependenciesFileName, groupName, package, version,
                                                  { UpdaterOptions.Default with
                                                      Common = InstallerOptions.CreateLegacyOptions(force, withBindingRedirects, cleanBindingRedirects, createNewBindingFiles, semVerUpdateMode, touchAffectedRefs, false, [], [], None)
                                                      NoInstall = installAfter |> not }))

    /// Updates the given package.
    member this.UpdatePackage(groupName, package: string, version: string option, force: bool, semVerUpdateMode, touchAffectedRefs): unit =
        this.UpdatePackage(groupName, package, version, force, false, false, false, true, semVerUpdateMode, touchAffectedRefs)

    /// Updates the given package.
    member this.UpdatePackage(groupName: string option, package: string, version: string option, force: bool, withBindingRedirects: bool, cleanBindingRedirects: bool, createNewBindingFiles:bool, installAfter: bool, semVerUpdateMode, touchAffectedRefs): unit =
        let groupName =
            match groupName with
            | None -> Constants.MainDependencyGroup
            | Some name -> GroupName name
        let withBindingRedirects = if withBindingRedirects then BindingRedirectsSettings.On else BindingRedirectsSettings.Off

        RunInLockedAccessMode(
            Path.Combine(this.RootPath,Constants.PaketFilesFolderName),
            fun () -> UpdateProcess.UpdatePackage(dependenciesFileName, groupName, PackageName package, version,
                                                  { UpdaterOptions.Default with
                                                      Common = InstallerOptions.CreateLegacyOptions(force, withBindingRedirects, cleanBindingRedirects, createNewBindingFiles, semVerUpdateMode, touchAffectedRefs, false, [], [], None)
                                                      NoInstall = installAfter |> not }))

    /// Restores all dependencies.
    member this.Restore(ignoreChecks): unit = this.Restore(false,None,[],false,ignoreChecks,false,None,None)

    /// Restores all dependencies.
    member this.Restore(): unit = this.Restore(false,None,[],false,false,false,None,None)

    /// Simple packages restore:
    /// - Doesn't restore projects
    /// - Doesn't write targets file
    member __.SimplePackagesRestore(): unit =
        RestoreProcess.Restore(dependenciesFileName,RestoreProcess.RestoreProjectOptions.NoProjects,false,None,true,false,None,None,true)

    /// Restores the given paket.references files.
    member this.Restore(group: string option, files: string list, ignoreChecks): unit = this.Restore(false, group, files, false, ignoreChecks,false,None,None)

    /// Restores the given paket.references files.
    member this.Restore(group: string option, files: string list): unit = this.Restore(false, group, files, false, false,false,None,None)

    /// Restores the given paket.references files.
    member this.Restore(force: bool, group: string option, files: string list, touchAffectedRefs: bool, ignoreChecks, failOnChecks, targetFramework, outputPath) : unit =
        if touchAffectedRefs then
            let packagesToTouch = RestoreProcess.FindPackagesNotExtractedYet(dependenciesFileName)
            this.Process (FindReferences.TouchReferencesOfPackages packagesToTouch)

        let restoreOpts =
            if List.isEmpty files then
                RestoreProcess.RestoreProjectOptions.AllProjects
            else
                RestoreProcess.RestoreProjectOptions.ReferenceFileList files

        RestoreProcess.Restore(dependenciesFileName,restoreOpts,force,Option.map GroupName group,ignoreChecks, failOnChecks, targetFramework, outputPath, false)

    /// Restores the given paket.references files.
    member this.Restore(force: bool, group: string option, project: string, touchAffectedRefs: bool, ignoreChecks, failOnChecks, targetFramework, outputPath) : unit =
        if touchAffectedRefs then
            let packagesToTouch = RestoreProcess.FindPackagesNotExtractedYet(dependenciesFileName)
            this.Process (FindReferences.TouchReferencesOfPackages packagesToTouch)
        RestoreProcess.Restore(dependenciesFileName,RestoreProcess.RestoreProjectOptions.SingleProject project,force,Option.map GroupName group,ignoreChecks, failOnChecks, targetFramework, outputPath, false)

    /// Restores packages for all available paket.references files
    /// (or all packages if onlyReferenced is false)
    member this.Restore(force: bool, group: string option, onlyReferenced: bool, touchAffectedRefs: bool, ignoreChecks, failOnFailedChecks, targetFramework, outputPath): unit =
        if not onlyReferenced then
            this.Restore(force,group,[],touchAffectedRefs,ignoreChecks,failOnFailedChecks,targetFramework, outputPath)
        else
            let referencesFiles =
                ProjectFile.FindAllProjects this.RootPath
                |> Array.choose (fun p -> p.FindReferencesFile())
            if Array.isEmpty referencesFiles then
                traceWarnfn "No paket.references files found for which packages could be installed."
            else
                this.Restore(force, group, Array.toList referencesFiles, touchAffectedRefs, ignoreChecks,  failOnFailedChecks, targetFramework, outputPath)

    /// Lists outdated packages.
    [<Obsolete("Use ShowOutdated with the force parameter set to true to get the old behavior")>]
    member this.ShowOutdated(strict: bool, includePrereleases: bool, groupName: string Option): unit =
        this.ShowOutdated(strict, true, includePrereleases, groupName)

    member this.ShowOutdated(strict: bool, force: bool, includePrereleases: bool, groupName: string Option): unit =
        FindOutdated.ShowOutdated strict force includePrereleases groupName |> this.Process

    /// Finds all outdated packages.
    [<Obsolete("Use FindOutdated with the force parameter set to true to get the old behavior")>]
    member this.FindOutdated(strict: bool, includePrereleases: bool, groupName: string Option): (string * string * SemVerInfo) list =
        this.FindOutdated(strict, true, includePrereleases, groupName)

    member this.FindOutdated(strict: bool, force: bool, includePrereleases: bool, groupName: string Option): (string * string * SemVerInfo) list =
        FindOutdated.FindOutdated strict force includePrereleases groupName
        |> this.Process
        |> List.map (fun (g, p,_,newVersion) -> g.ToString(),p.ToString(),newVersion)

    /// Downloads the latest paket.bootstrapper into the .paket folder and try to rename it to paket.exe in order to activate magic mode.
    member this.DownloadLatestBootstrapper() : unit =
        RunInLockedAccessMode(
            Path.Combine(this.RootPath,Constants.PaketFilesFolderName),
            fun () ->
                this.Process Releases.downloadLatestBootstrapperAndTargets
                let bootStrapperFileName = Path.Combine(this.RootPath,Constants.PaketFolderName, Constants.BootstrapperFileName)
                let paketFileName = FileInfo(Path.Combine(this.RootPath,Constants.PaketFolderName, Constants.PaketFileName))
                try
                    if paketFileName.Exists then
                        paketFileName.Delete()
                    File.Move(bootStrapperFileName,paketFileName.FullName)
                    false
                with
                | _ ->
                    false
        )

    /// Pulls new paket.targets and bootstrapper and puts them into .paket folder.
    member this.TurnOnAutoRestore(): unit =
        RunInLockedAccessMode(
            Path.Combine(this.RootPath,Constants.PaketFilesFolderName),
            fun () -> VSIntegration.TurnOnAutoRestore |> this.Process; false)

    /// Removes paket.targets file and Import section from project files.
    member this.TurnOffAutoRestore(): unit =
        RunInLockedAccessMode(
            Path.Combine(this.RootPath,Constants.PaketFilesFolderName),
            fun () -> VSIntegration.TurnOffAutoRestore |> this.Process; false)

    /// Returns the installed version of the given package.
    member this.GetInstalledVersion(packageName: string): string option =
        this.GetInstalledVersion(None,packageName)

    /// Returns the installed version of the given package.
    member this.GetInstalledVersion(groupName:string option,packageName: string): string option =
        let groupName =
            match groupName with
            | None -> Constants.MainDependencyGroup
            | Some name -> GroupName name

        match this.GetLockFile().Groups |> Map.tryFind groupName with
        | None -> None
        | Some group ->
            group.Resolution.TryFind(PackageName packageName)
            |> Option.map (fun package -> package.Version.ToString())

    /// Returns the installed versions of all installed packages.
    member this.GetInstalledPackages(): (string * string * string) list =
        this.GetLockFile().GetGroupedResolution()
        |> listPackages

    /// Returns all sources from the dependencies file.
    member __.GetSources() =
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
        |> List.distinct

    /// Returns the installed versions of all installed packages which are referenced in the references file.
    member this.GetInstalledPackages(referencesFile:ReferencesFile): (string * string * string) list =
        let lockFile = this.GetLockFile()
        let resolved = lockFile.GetGroupedResolution()
        referencesFile
        |> lockFile.GetPackageHull
        |> Seq.map (fun kv ->
            let groupName,packageName = kv.Key
            groupName.ToString(),
            packageName.ToString(),
            resolved.[kv.Key].Version.ToString())
        |> Seq.toList

    /// Returns an InstallModel for the given package.
    member this.GetInstalledPackageModel(groupName,packageName) =
        let packageName = PackageName packageName
        let groupName =
            match groupName with
            | None -> Constants.MainDependencyGroup
            | Some name -> GroupName name

        match this.GetLockFile().Groups |> Map.tryFind groupName with
        | None -> failwithf "Group %O can't be found in paket.lock." groupName
        | Some group ->
            match group.TryFind(packageName) with
            | None -> failwithf "Package %O is not installed in group %O." packageName groupName
            | Some resolvedPackage ->
                let folder = resolvedPackage.Folder this.RootPath groupName
                let kind =
                    match resolvedPackage.Kind with
                    | ResolvedPackageKind.Package -> InstallModelKind.Package
                    | ResolvedPackageKind.DotnetCliTool -> InstallModelKind.DotnetCliTool

                InstallModel.CreateFromContent(
                    resolvedPackage.Name,
                    resolvedPackage.Version,
                    kind,
                    Paket.Requirements.FrameworkRestriction.NoRestriction,
                    NuGet.GetContent(folder).Force())

    /// Returns all libraries for the given package and framework.
    member this.GetLibraries(groupName,packageName,frameworkIdentifier:TargetProfile) =
        this
          .GetInstalledPackageModel(groupName,packageName)
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

        this.GetLockFile().GetGroupedResolution()
        |> Seq.filter (fun kv -> normalizedDependendenciesFromRefFile |> Seq.exists ((=) kv.Key))
        |> Seq.filter (fun kv -> normalizedDependencies |> Seq.exists ((=) kv.Key))
        |> listPackages

    /// Returns the installed versions of all direct dependencies.
    member this.GetDirectDependencies(): (string * string * string) list =
        let dependenciesFile = DependenciesFile.ReadFromFile dependenciesFileName
        let normalizedDependencies =
            dependenciesFile.Groups
            |> Seq.collect (fun kv ->
                    dependenciesFile.GetDependenciesInGroup(kv.Value.Name)
                    |> Seq.map (fun kv' -> kv.Key, kv'.Key)
                    |> Seq.toList)
            |> Set.ofSeq

        this.GetLockFile().GetGroupedResolution()
        |> Seq.filter (fun kv -> normalizedDependencies.Contains kv.Key)
        |> listPackages

    /// Returns all groups.
    member __.GetGroups(): string list =
        let dependenciesFile = DependenciesFile.ReadFromFile dependenciesFileName
        dependenciesFile.Groups
        |> Seq.map (fun kv -> kv.Key.ToString())
        |> Seq.toList

    /// Returns the direct dependencies for the given package.
    member this.GetDirectDependenciesForPackage(groupName,packageName:string): (string * string * string) list =
        let resolvedPackages = this.GetLockFile().GetGroupedResolution()
        let package = resolvedPackages.[groupName, (PackageName packageName)]
        let normalizedDependencies =
            package.Dependencies
            |> Seq.map (fun (name,_,_) -> groupName, name)
            |> Set.ofSeq

        resolvedPackages
        |> Seq.filter (fun kv -> normalizedDependencies.Contains kv.Key)
        |> listPackages

    /// Removes the given package from the main dependency group of the dependencies file.
    member this.Remove(package: string): unit = this.Remove(None, package)

    /// Removes the given package from dependencies file.
    member this.Remove(groupName, package: string): unit = this.Remove(groupName, package, false, false, true)

    /// Removes the given package from dependencies file.
    member this.Remove(groupName, package: string, force: bool, interactive: bool,installAfter: bool): unit =
        RunInLockedAccessMode(
            Path.Combine(this.RootPath,Constants.PaketFilesFolderName),
            fun () -> RemoveProcess.Remove(dependenciesFileName, groupName, PackageName package, force, interactive, installAfter))

    /// Removes the given package from the specified project
    member this.RemoveFromProject(groupName,package: string,force: bool, projectName: string,installAfter: bool): unit =
        RunInLockedAccessMode(
            Path.Combine(this.RootPath,Constants.PaketFilesFolderName),
            fun () -> RemoveProcess.RemoveFromProject(dependenciesFileName, groupName, PackageName package, force, projectName, installAfter))

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
        |> Seq.map (fun source ->
            match source with
            | NuGetV2 s ->
                let res = NuGetV3.getSearchAPI(s.Authentication,s.Url) |> Async.AwaitTask |> Async.RunSynchronously
                match res with
                | Some _ ->
                    NuGetV3.FindPackages(s.Authentication, s.Url, searchTerm, maxResults)
                    |> Async.map (FSharp.Core.Result.mapError (fun err -> s.Url, err))
                | None ->
                    NuGetV2.FindPackages(s.Authentication, s.Url, searchTerm, maxResults)
                    |> Async.map (FSharp.Core.Result.mapError (fun err -> s.Url, err))
            | NuGetV3 s ->
                NuGetV3.FindPackages(s.Authentication, s.Url, searchTerm, maxResults)
                |> Async.map (FSharp.Core.Result.mapError (fun err -> s.Url, err))
            | LocalNuGet(s,_) ->
                async {
                    return
                        Fake.Globbing.search s (sprintf "**/*%s*" searchTerm)
                        |> List.distinctBy (fun s ->
                            let parts = FileInfo(s).Name.Split('.')
                            let nameParts = parts |> Seq.takeWhile (fun x -> x <> "nupkg" && System.Int32.TryParse x |> fst |> not)
                            String.Join(".",nameParts).ToLower())
                        |> List.map NuGetLocal.getPackageNameFromLocalFile
                        |> List.toArray
                        |> FSharp.Core.Result.Ok
                })
        // TODO: This is to keep current API surface, in future version we want to properly delegate error cases to higher levels?
        |> Seq.map (fun r ->
            async {
                let! result = r
                match result with
                | FSharp.Core.Result.Ok r -> return r
                | FSharp.Core.Result.Error (url, err) ->
                    if verbose then
                        tracefn "Ignoring error when requesting '%s': %O" url err.SourceException
                    else
                        tracefn "Ignoring error when requesting '%s': %s" url err.SourceException.Message
                    return [||]
            })

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
            this.GetSources() |> Seq.collect (fun kv -> kv.Value),
            searchTerm,
            cancellationToken,
            maxResults)

    static member FindPackageVersions(root,sources:PackageSource seq,name:string,?maxResults,?alternativeProjectRoot) =
        let maxResults = defaultArg maxResults 1000
        let sources =
            match sources |> Seq.toList |> List.distinct with
            | [] -> [PackageSources.DefaultNuGetSource]
            | sources -> sources
            |> List.distinct

        let versions =
            NuGet.GetVersions true alternativeProjectRoot root (GetPackageVersionsParameters.ofParams sources (GroupName "") (PackageName name))
            |> Async.RunSynchronously
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

    // Packs all paket.template files.
    member __.Pack(outputPath, ?buildConfig, ?buildPlatform, ?version, ?specificVersions, ?releaseNotes, ?templateFile, ?workingDir, ?excludedTemplates, ?lockDependencies, ?minimumFromLockFile, ?pinProjectReferences, ?interprojectReferencesConstraint, ?symbols, ?includeReferencedProjects, ?projectUrl) =
        let dependenciesFile = DependenciesFile.ReadFromFile dependenciesFileName
        let specificVersions = defaultArg specificVersions Seq.empty
        let workingDir = defaultArg workingDir (dependenciesFile.FileName |> Path.GetDirectoryName)
        let lockDependencies = defaultArg lockDependencies false
        let minimumFromLockFile = defaultArg minimumFromLockFile false
        let pinProjectReferences = defaultArg pinProjectReferences false
        let interprojectReferencesConstraint = defaultArg interprojectReferencesConstraint None
        let symbols = defaultArg symbols false
        let includeReferencedProjects = defaultArg includeReferencedProjects false
        let projectUrl = defaultArg (Some projectUrl) None
        PackageProcess.Pack(workingDir, dependenciesFile, outputPath, buildConfig, buildPlatform, version, specificVersions, releaseNotes, templateFile, excludedTemplates, lockDependencies, minimumFromLockFile, pinProjectReferences, interprojectReferencesConstraint, symbols, includeReferencedProjects, projectUrl)

    /// Pushes a nupkg file.
    static member Push(packageFileName, ?url, ?apiKey, ?endPoint: string, ?paketVersion, ?maxTrials, ?ignoreConflicts) =
        let urlWithEndpoint = RemoteUpload.GetUrlWithEndpoint url endPoint
        let envKey =
            match Environment.GetEnvironmentVariable("NUGET_KEY") |> Option.ofObj with
            | Some key ->
                Some key
            | None ->
                match Environment.GetEnvironmentVariable("nugetkey") |> Option.ofObj with
                | Some(key) ->
                    traceWarnfn "The environment variable nugetkey is deprecated. Please use the NUGET_KEY environment variable."
                    Some(key)
                | None -> None

        let configKey =
            let url = defaultArg url "https://nuget.org"
            AuthService.GetGlobalAuthenticationProvider url
            |> AuthProvider.retrieve false
            |> Option.bind (fun a -> match a with Token t -> Some t | _ -> None )

        let firstPresentKey =
            [apiKey; envKey; configKey]
            |> List.choose id
            |> List.where (String.IsNullOrEmpty >> not)
            |> List.tryHead

        match firstPresentKey with
        | None ->
            failwithf "Could not push package %s due to missing credentials for the url %s. Please specify a NuGet API key via the command line, the environment variable \"nugetkey\", or by using 'paket config add-token'." packageFileName urlWithEndpoint
        | Some apiKey ->
            let maxTrials = defaultArg maxTrials 5
            let ignoreConflicts = defaultArg ignoreConflicts false
            RemoteUpload.Push
                maxTrials
                urlWithEndpoint
                apiKey
                "4.1.0"  // see https://github.com/NuGet/NuGetGallery/issues/4315 - maybe us (defaultArg paketVersion "4.1.0")
                packageFileName
                ignoreConflicts

    /// Lists all paket.template files in the current solution.
    member this.ListTemplateFiles() : TemplateFile list =
        let lockFile = this.GetLockFile()
        ProjectFile.FindAllProjects(this.RootPath)
        |> Array.choose (fun proj -> proj.FindTemplatesFile())
        |> Array.choose (fun path ->
                         try
                             Some(TemplateFile.Load(path, lockFile, None, Map.empty))
                         with
                         | _ -> None)
        |> Array.toList


    /// Fix the transitive references in a list of generated .nuspec files
    [<Obsolete "Use a real references file instead. Note that this overload doesn't take a 'real' references file.">]
    static member FixNuspecs (referencesFile:string, nuspecFileList:string list) =

        for nuspecFile in nuspecFileList do
            if not (File.Exists nuspecFile) then
                failwithf "Specified file '%s' does not exist." nuspecFile

        for nuspecFile in nuspecFileList do
            let nuspecText = File.ReadAllText nuspecFile

            let doc =
                try let doc = Xml.XmlDocument() in doc.LoadXml nuspecText
                    doc
                with exn -> raise (Exception(sprintf "Could not parse nuspec file '%s'." nuspecFile, exn))

            if not (File.Exists referencesFile) then
                failwithf "Specified references-file '%s' does not exist." referencesFile

            let referencesText = File.ReadAllLines referencesFile
            let transitiveReferences =
                referencesText |> Array.map (fun l -> l.Split [|','|])
                |> Array.choose (fun x -> if x.[2] = "Transitive" then Some x.[0] else None)
                |> Set.ofArray

            let rec traverse (parent:XmlNode) =
                let nodesToRemove = ResizeArray()
                for node in parent.ChildNodes do
                    if node.Name = "dependency" then
                        let packageName =
                            match node.Attributes.["id"] with null -> "" | x -> x.InnerText

                        if transitiveReferences.Contains packageName then
                            nodesToRemove.Add node |> ignore

                if nodesToRemove.Count = 0 then
                    for node in parent.ChildNodes do traverse node
                else
                    for node in nodesToRemove do parent.RemoveChild node |> ignore
            traverse doc
            use fileStream = File.Open (nuspecFile, FileMode.Create)
            doc.Save fileStream

    static member FixNuspecs (projectFile: ProjectFile, referencesFile:ReferencesFile, nuspecFileList:string list) =
        let attr (name: string) (node: XmlNode) =
            match node.Attributes.[name] with
            | null -> None
            | attr -> Some attr.InnerText

        /// adjusts the given version requirement from a nuspec file using the 'locked' version of that same dependency
        let adjustRangeToLockedVersion (VersionRequirement(range, prerelease)) (lockedVersion: SemVerInfo) =
            let range' =
                match range with
                | VersionRange.Maximum max -> VersionRange.Between(VersionRangeBound.Including, lockedVersion, max, VersionRangeBound.Including)
                | VersionRange.GreaterThan _ -> VersionRange.GreaterThan lockedVersion
                | VersionRange.Minimum _ -> VersionRange.Minimum lockedVersion
                | VersionRange.Specific _ -> VersionRange.Specific lockedVersion
                | VersionRange.LessThan top -> VersionRange.Between(VersionRangeBound.Including, lockedVersion, top, VersionRangeBound.Excluding)
                | VersionRange.OverrideAll _ -> VersionRange.OverrideAll lockedVersion
                | VersionRange.Range(_, _, right, rightBound) -> VersionRange.Range(VersionRangeBound.Including, lockedVersion, right, rightBound)

            VersionRequirement(range', prerelease)
        let deps = Dependencies.Locate(Path.GetDirectoryName(referencesFile.FileName))
        let locked = deps.GetLockFile()
        let projectReferences =
            projectFile.GetAllReferencedProjects(onlyWithOutput = true, cache = PackProcessCache.empty)
            |> List.map (fun proj -> proj.NameWithoutExtension)
            |> Set.ofList
        let depsFile = deps.GetDependenciesFile()
        let allFrameworkRestrictions = 
            locked.GetPackageHull referencesFile 
            |> Seq.map(fun kvp -> snd kvp.Key, fst kvp.Key, kvp.Value.Settings.FrameworkRestrictions.GetExplicitRestriction()) 


        // NuGet has thrown away "group" association, so this is best effort.
        let implicitOrExplictlyReferencedPackages =
            locked.GetPackageHull referencesFile
            |> Seq.map (fun kv -> kv.Key |> snd)
            |> Set.ofSeq


        for nuspecFile in nuspecFileList do
            if not (File.Exists nuspecFile) then
                failwithf "Specified file '%s' does not exist." nuspecFile

        let projectReferencedDeps =
            referencesFile.Groups
            |> Seq.collect (fun (KeyValue(group, packages)) -> packages.NugetPackages |> Seq.map (fun p -> group, p))

        let groupsForProjectReferencedDeps =
            projectReferencedDeps
            |> Seq.map (fun (grp, pkg) -> pkg.Name, grp)
            |> Seq.groupBy fst
            |> Seq.map (fun (key, items) -> key, (items |> Seq.map snd |> List.ofSeq))
            |> Map.ofSeq

        let allDepsFilePackages =
            depsFile.Groups
            |> Seq.map (fun (KeyValue(g, p)) -> g, p.Packages)

        let allDepsFilePackageRequirements =
            allDepsFilePackages
            |> Seq.collect (fun (group, packages) ->
                let depsFileRanges = depsFile.Groups.[group].Packages |> List.map (fun p -> p.Name, p.VersionRequirement) |> Map.ofList
                packages
                |> Seq.choose (fun p ->
                    match depsFileRanges.TryFind p.Name with
                    | Some req -> Some ((group, p.Name), req)
                    | None -> None
                )
            )
            |> Map.ofSeq

        let allLockFileResolvedVersions =
            allDepsFilePackages
            |> Seq.collect (fun (group, packages) ->
                let lockGroup = locked.Groups.[group].Resolution
                packages
                |> Seq.choose (fun p ->
                    lockGroup.TryFind p.Name
                    |> Option.map (fun p -> (group, p.Name), p.Version)
                )
            )
            |> Map.ofSeq

        let lockedPackageVersionRequirements =
            projectReferencedDeps
            |> Seq.map (fun (grp, pkg) -> grp, pkg.Name)
            |> Seq.choose (fun groupAndPackage -> match allDepsFilePackageRequirements.TryFind groupAndPackage with | Some r -> Some(groupAndPackage, r) | None -> None)
            |> Seq.map (fun (groupAndPackage, versionRequirement) ->
                match allLockFileResolvedVersions |> Map.tryFind groupAndPackage with
                | Some lockedVersion -> groupAndPackage, adjustRangeToLockedVersion versionRequirement lockedVersion
                | None -> groupAndPackage, versionRequirement
            )
            |> Map.ofSeq

        let (|IndirectDependency|DirectDependency|UnknownDependency|) (p: PackageName) =
            if not (implicitOrExplictlyReferencedPackages.Contains p)
            then
                UnknownDependency
            else

                tracefn "Many groups detected %O" groupsForProjectReferencedDeps
                match groupsForProjectReferencedDeps.TryFind p with
                | None -> IndirectDependency
                | Some [] -> IndirectDependency
                // | Some [group] -> DirectDependency (group, p)
                | Some (groups) ->
                    DirectDependency (groups, p)

        let (|MatchesRange|NeedsRangeUpdate|LockRangeNotFound|) (groupAndPackage: (GroupName * PackageName), nuspecVersionRequirement: VersionRequirement) =
            match lockedPackageVersionRequirements.TryFind groupAndPackage with
            | Some lockedFileRange ->
                if lockedFileRange = nuspecVersionRequirement
                then MatchesRange
                else NeedsRangeUpdate lockedFileRange
            | None -> LockRangeNotFound

        for nuspecFile in nuspecFileList do
            let nuspecText = File.ReadAllText nuspecFile

            let doc =
                try let doc = Xml.XmlDocument() in doc.LoadXml nuspecText
                    doc
                with exn -> raise (Exception(sprintf "Could not parse nuspec file '%s'." nuspecFile, exn))

            let rec traverse (parent:XmlNode) =
                let nodesToRemove = ResizeArray()
                for node in parent.ChildNodes do
                    if node.Name = "dependency" then
                        let targetFramework = 
                            attr "targetFramework" node.ParentNode
                            |> Option.bind(fun tfm ->
                                PlatformMatching.forceExtractPlatforms tfm |> fun p -> p.ToTargetProfile true
                            )
                        let results =
                            targetFramework
                            |> Option.map(fun tfm ->
                                allFrameworkRestrictions
                                |> Seq.filter(fun (_, _, fr) -> fr.IsMatch tfm)
                                |> Seq.toList
                                
                            )
                            |> Option.defaultValue []
                        
                        
                        let packName = attr "id" node |> Option.map PackageName
                        let versionRange = attr "version" node |> Option.bind VersionRequirement.TryParse
                        tracefn "Checking dependency status for package %O, version %O" packName versionRange

                        // Ignore unknown packages, see https://github.com/fsprojects/Paket/issues/2694
                        // Assert that the version we remove it not newer than what we have in our resolution!
                        match packName with
                        | Some IndirectDependency ->
                            tracefn "Package '%O' was not explicitly referenced in %s and will be removed" packName.Value referencesFile.FileName
                            nodesToRemove.Add node |> ignore
                        | Some UnknownDependency ->
                            tracefn "Package '%O' is not part of the explicit dependency tree in %s and so will be skipped" packName.Value referencesFile.FileName
                            ()
                        | Some (DirectDependency (g, (PackageName.PackageName(pkg, _) as p))) ->
                            let groupWithinFramework = results |> Seq.tryFind(fun (p1,g1,_) -> p = p1 && g |> Seq.exists(fun x -> x = g1)) |> Option.map(fun (_,g,_) -> g)
                            match versionRange, groupWithinFramework with
                            | Some versionRange, Some grp ->
                                match (grp, p), versionRange with
                                | MatchesRange ->
                                    tracefn "Package '%s' is a direct dependency and requires no version patching" pkg
                                    ()
                                | LockRangeNotFound ->
                                    let grp = g |> List.map(fun (GroupName(name,_)) -> name) |> String.concat ", "
                                    tracefn "Couldn't find a version range for package '%s' in any group '%s', is this package in your paket.dependencies file?" pkg grp
                                    ()
                                | NeedsRangeUpdate newVersionRange ->
                                    let oldVersionRangeString = versionRange.FormatInNuGetSyntax()
                                    let nugetVersionRangeString = newVersionRange.FormatInNuGetSyntax()
                                    tracefn "Package '%s' is a direct dependency and requires version patching from %s to %s"pkg oldVersionRangeString nugetVersionRangeString
                                    node.Attributes.["version"].InnerText <- nugetVersionRangeString
                            | _, _ ->
                                tracefn "Package '%s' is a direct dependency but no desired range was found, so it will be skipped" pkg
                                ()
                        | None ->
                            raise (Exception(sprintf "Could not read dependency id for package node %O" node))

                if nodesToRemove.Count = 0 then
                    for node in parent.ChildNodes do traverse node
                else
                    for node in nodesToRemove do parent.RemoveChild node |> ignore
            traverse doc
            use fileStream = File.Open (nuspecFile, FileMode.Create)
            doc.Save fileStream

module PublicAPI =
    /// Takes a version string formatted for Semantic Versioning and parses it
    /// into the internal representation used by Paket.
    let ParseSemVer (version:string) = SemVer.Parse version

    let PreCalculateMaps () =
        async {
            KnownTargetProfiles.AllProfiles
            |> Seq.iter (fun profile ->
                SupportCalculation.getPlatformsSupporting profile |> ignore
                let fws =
                    profile.Frameworks
                    |> List.filter (function
                        | MonoTouch
                        | UAP _
                        | MonoAndroid _
                        | XamariniOS
                        | XamarinTV
                        | XamarinWatch
                        | XamarinMac
                        | DotNetCoreApp _
                        | DotNetStandard _
                        | Unsupported _
                        | XCode _
                        | Tizen _ -> false
                        | _ -> true)
                if fws.Length > 0 then SupportCalculation.findPortable false fws |> ignore)
            // calculated as part of the above...
            SupportCalculation.getSupportedPreCalculated PortableProfileType.Profile259 |> ignore
        }
        |> Async.StartAsTask
