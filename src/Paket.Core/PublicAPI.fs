namespace Paket

open Paket.Domain
open Paket.Logging
open Paket.PackageSources

open System
open System.Xml
open System.IO
open Chessie.ErrorHandling

/// Paket API which is optimized for F# Interactive use.
type Dependencies(dependenciesFileName: string) =
    let listPackages (packages: System.Collections.Generic.KeyValuePair<GroupName*PackageName, PackageResolver.ResolvedPackage> seq) =
        packages
        |> Seq.map (fun kv ->
                let groupName,packageName = kv.Key
                groupName.ToString(),packageName.ToString(),kv.Value.Version.ToString())
        |> Seq.toList
        


    /// Clears the NuGet cache
    static member ClearCache() =
        let emptyDir path =
            if verbose then
               verbosefn "Emptying '%s'" path
            emptyDir (DirectoryInfo path)
        
        emptyDir (Constants.UserNuGetPackagesFolder)
        emptyDir (Constants.NuGetCacheFolder)
        emptyDir (Constants.GitRepoCacheFolder)

    /// Tries to locate the paket.dependencies file in the current folder or a parent folder.
    static member Locate(): Dependencies = Dependencies.Locate(Directory.GetCurrentDirectory())

    /// Returns an instance of the paket.lock file.
    member this.GetLockFile() = 
        let lockFileName = DependenciesFile.FindLockfile dependenciesFileName
        LockFile.LoadFrom(lockFileName.FullName)

    /// Returns an instance of the paket.dependencies file.
    member this.GetDependenciesFile() = DependenciesFile.ReadFromFile dependenciesFileName

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
                        failwithf "Could not find '%s'. To use Paket with this solution, please run 'paket init' first.%sIf you have already run 'paket.init' then ensure that '%s' is located in the top level directory of your repository.%sLike this:%sMySourceDir%s  .paket%s  paket.dependencies" 
                          Constants.DependenciesFileName Environment.NewLine Constants.DependenciesFileName Environment.NewLine Environment.NewLine Environment.NewLine Environment.NewLine
                    else
                        Constants.DependenciesFileName
                | _ -> findInPath(parent, withError)

        let dependenciesFileName = findInPath(DirectoryInfo path,true)
        if verbose then
            verbosefn "found: %s" dependenciesFileName
        Dependencies(dependenciesFileName)

    /// Initialize paket.dependencies file in current directory
    static member Init() = Dependencies.Init(Directory.GetCurrentDirectory())

    /// Initialize paket.dependencies file in the given directory
    static member Init(directory) =  Dependencies.Init(directory,false)

    /// Initialize paket.dependencies file in the given directory
    static member Init(directory,fromBootstrapper) =
        let directory = DirectoryInfo(directory)

        RunInLockedAccessMode(
            directory.FullName,
            fun () ->
                PaketEnv.init directory
                |> returnOrFail
        )

        let deps = Dependencies.Locate()
        deps.DownloadLatestBootstrapper(fromBootstrapper)

    /// Converts the solution from NuGet to Paket.
    static member ConvertFromNuget(force: bool,installAfter: bool, initAutoRestore: bool,credsMigrationMode: string option, ?directory: DirectoryInfo) : unit =
        match directory with
        | Some d -> Dependencies.ConvertFromNuget(force, installAfter, initAutoRestore, credsMigrationMode, false, d)
        | None -> Dependencies.ConvertFromNuget(force, installAfter, initAutoRestore, credsMigrationMode, false)

    /// Converts the solution from NuGet to Paket.
    static member ConvertFromNuget(force: bool,installAfter: bool, initAutoRestore: bool,credsMigrationMode: string option, fromBootstrapper, ?directory: DirectoryInfo) : unit =
        let dir = defaultArg directory (DirectoryInfo(Directory.GetCurrentDirectory()))
        let rootDirectory = dir

        RunInLockedAccessMode(
            rootDirectory.FullName,
            fun () ->
                NuGetConvert.convertR rootDirectory force credsMigrationMode
                |> returnOrFail
                |> NuGetConvert.replaceNuGetWithPaket initAutoRestore installAfter fromBootstrapper
        )

    /// Converts the current package dependency graph to the simplest dependency graph.
    member this.Simplify(interactive : bool) =
        RunInLockedAccessMode(
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
        this.Add(groupName, package, version, force = false,  withBindingRedirects = false, cleanBindingRedirects = false, createNewBindingFiles = false, interactive = false, installAfter = true, semVerUpdateMode = SemVerUpdateMode.NoRestriction, touchAffectedRefs = false)

    /// Adds the given package with the given version to the dependencies file.
    member this.Add(groupName: string option, package: string,version: string,force: bool, withBindingRedirects: bool, cleanBindingRedirects: bool,  createNewBindingFiles:bool, interactive: bool, installAfter: bool, semVerUpdateMode, touchAffectedRefs): unit =
        RunInLockedAccessMode(
            this.RootPath,
            fun () -> AddProcess.Add(dependenciesFileName, groupName, PackageName(package.Trim()), version,
                                     InstallerOptions.CreateLegacyOptions(force, withBindingRedirects, cleanBindingRedirects, createNewBindingFiles, semVerUpdateMode, touchAffectedRefs, false, [], [], None),
                                     interactive, installAfter))

   /// Adds the given package with the given version to the dependencies file.
    member this.AddToProject(groupName, package: string,version: string,force: bool, withBindingRedirects: bool, cleanBindingRedirects: bool, createNewBindingFiles:bool, projectName: string, installAfter: bool, semVerUpdateMode, touchAffectedRefs): unit =
        RunInLockedAccessMode(
            this.RootPath,
            fun () -> AddProcess.AddToProject(dependenciesFileName, groupName, PackageName package, version,
                                              InstallerOptions.CreateLegacyOptions(force, withBindingRedirects, cleanBindingRedirects, createNewBindingFiles, semVerUpdateMode, touchAffectedRefs, false, [], [], None),
                                              projectName, installAfter))

    /// Adds credentials for a Nuget feed
    member this.AddCredentials(source: string, username: string, password : string) : unit =
        RunInLockedAccessMode(
            this.RootPath,
            fun () -> ConfigFile.askAndAddAuth source username password |> returnOrFail )
  
    /// Adds a token for a source
    member this.AddToken(source : string, token : string) : unit =
        RunInLockedAccessMode(this.RootPath, fun () -> ConfigFile.AddToken(source, token) |> returnOrFail)

    /// Installs all dependencies.
    member this.Install(force: bool) = this.Install(force, false, false, false, false, SemVerUpdateMode.NoRestriction, false, false, [], [], None)

    /// Installs all dependencies.
    member this.Install(force: bool, withBindingRedirects: bool, cleanBindingRedirects: bool, createNewBindingFiles:bool, onlyReferenced: bool, semVerUpdateMode, touchAffectedRefs, generateLoadScripts, providedFrameworks, providedScriptTypes, alternativeProjectRoot): unit =        
        this.Install({ InstallerOptions.CreateLegacyOptions(force, withBindingRedirects, cleanBindingRedirects, createNewBindingFiles, semVerUpdateMode, touchAffectedRefs, generateLoadScripts, providedFrameworks, providedScriptTypes, alternativeProjectRoot) with OnlyReferenced = onlyReferenced })

    /// Installs all dependencies.
    member private this.Install(options: InstallerOptions): unit =
        RunInLockedAccessMode(
            this.RootPath,
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
        this.GenerateLoadScriptData this.DependenciesFile groups frameworks scriptTypes 
        |> List.iter (fun sd -> 
            let rootDir = this.RootDirectory
            Directory.CreateDirectory <| Path.Combine (Constants.PaketFolderName,"load") |> ignore
            let scriptPath = Path.Combine (rootDir.FullName , sd.PartialPath)
            tracefn "scriptpath - %s" scriptPath
            let scriptDir = Path.GetDirectoryName scriptPath |> Path.GetFullPath |> DirectoryInfo
            scriptDir.Create()
            tracefn "created - '%s'" <| Path.Combine (rootDir.FullName , sd.PartialPath)
            sd.Save rootDir
        )

    /// Updates all dependencies.
    member this.Update(force: bool): unit = 
        this.Update(force, false, false, false)

    /// Updates all dependencies.
    member this.Update(force: bool, withBindingRedirects:bool, cleanBindingRedirects: bool, createNewBindingFiles:bool): unit =
        this.Update(force, withBindingRedirects, cleanBindingRedirects, createNewBindingFiles, true, SemVerUpdateMode.NoRestriction, false)

    /// Updates all dependencies.
    member this.Update(force: bool, withBindingRedirects: bool, cleanBindingRedirects: bool, createNewBindingFiles:bool, installAfter: bool, semVerUpdateMode, touchAffectedRefs): unit =
        RunInLockedAccessMode(
            this.RootPath,
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
        RunInLockedAccessMode(
            this.RootPath,
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

        RunInLockedAccessMode(
            this.RootPath,
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

        RunInLockedAccessMode(
            this.RootPath,
            fun () -> UpdateProcess.UpdatePackage(dependenciesFileName, groupName, PackageName package, version,
                                                  { UpdaterOptions.Default with
                                                      Common = InstallerOptions.CreateLegacyOptions(force, withBindingRedirects, cleanBindingRedirects, createNewBindingFiles, semVerUpdateMode, touchAffectedRefs, false, [], [], None)
                                                      NoInstall = installAfter |> not }))

    /// Restores all dependencies.
    member this.Restore(ignoreChecks): unit = this.Restore(false,None,[],false,ignoreChecks,false,None)

    /// Restores all dependencies.
    member this.Restore(): unit = this.Restore(false,None,[],false,false,false,None)

    /// Restores the given paket.references files.
    member this.Restore(group: string option, files: string list, ignoreChecks): unit = this.Restore(false, group, files, false, ignoreChecks,false,None)

    /// Restores the given paket.references files.
    member this.Restore(group: string option, files: string list): unit = this.Restore(false, group, files, false, false,false,None)

    /// Restores the given paket.references files.
    member this.Restore(force: bool, group: string option, files: string list, touchAffectedRefs: bool, ignoreChecks, failOnChecks, targetFramework) : unit =
        RunInLockedAccessMode(
            this.RootPath,
            fun () ->
                if touchAffectedRefs then
                    let packagesToTouch = RestoreProcess.FindPackagesNotExtractedYet(dependenciesFileName)
                    this.Process (FindReferences.TouchReferencesOfPackages packagesToTouch)
                RestoreProcess.Restore(dependenciesFileName,None,force,Option.map GroupName group,files,ignoreChecks, failOnChecks, targetFramework))

    /// Restores the given paket.references files.
    member this.Restore(force: bool, group: string option, project: string, touchAffectedRefs: bool, ignoreChecks, failOnChecks, targetFramework) : unit =
        RunInLockedAccessMode(
            this.RootPath,
            fun () ->
                if touchAffectedRefs then
                    let packagesToTouch = RestoreProcess.FindPackagesNotExtractedYet(dependenciesFileName)
                    this.Process (FindReferences.TouchReferencesOfPackages packagesToTouch)
                RestoreProcess.Restore(dependenciesFileName,Some project,force,Option.map GroupName group,[],ignoreChecks, failOnChecks, targetFramework))

    /// Restores packages for all available paket.references files
    /// (or all packages if onlyReferenced is false)
    member this.Restore(force: bool, group: string option, onlyReferenced: bool, touchAffectedRefs: bool, ignoreChecks, failOnFailedChecks, targetFramework): unit =
        if not onlyReferenced then 
            this.Restore(force,group,[],touchAffectedRefs,ignoreChecks,failOnFailedChecks,targetFramework) 
        else
            let referencesFiles =
                this.RootPath
                |> ProjectFile.FindAllProjects
                |> Array.choose (fun (p:ProjectFile) -> p.FindReferencesFile())
            if Array.isEmpty referencesFiles then
                traceWarnfn "No paket.references files found for which packages could be installed."
            else 
                this.Restore(force, group, Array.toList referencesFiles, touchAffectedRefs, ignoreChecks,  failOnFailedChecks, targetFramework)

    /// Lists outdated packages.
    member this.ShowOutdated(strict: bool,includePrereleases: bool, groupName: string Option): unit =
        FindOutdated.ShowOutdated strict includePrereleases groupName |> this.Process

    /// Finds all outdated packages.
    member this.FindOutdated(strict: bool,includePrereleases: bool, groupName: string Option): (string * string * SemVerInfo) list =
        FindOutdated.FindOutdated strict includePrereleases groupName
        |> this.Process
        |> List.map (fun (g, p,_,newVersion) -> g.ToString(),p.ToString(),newVersion)

    /// Downloads the latest paket.bootstrapper into the .paket folder.
    member this.DownloadLatestBootstrapper() : unit =
        this.DownloadLatestBootstrapper(false)

    /// Downloads the latest paket.bootstrapper into the .paket folder andtry to rename it to paket.exe in order to activate magic mode.
    member this.DownloadLatestBootstrapper(fromBootstrapper) : unit =
        RunInLockedAccessMode(
            this.RootPath,
            fun () -> 
                Releases.downloadLatestBootstrapperAndTargets fromBootstrapper |> this.Process
                let bootStrapperFileName = Path.Combine(this.RootPath,Constants.PaketFolderName, Constants.BootstrapperFileName)
                let paketFileName = FileInfo(Path.Combine(this.RootPath,Constants.PaketFolderName, Constants.PaketFileName))
                let configFileName = FileInfo(Path.Combine(this.RootPath,Constants.PaketFolderName, Constants.PaketFileName + ".config"))
                try
                    if paketFileName.Exists then
                        paketFileName.Delete()
                    File.Move(bootStrapperFileName,paketFileName.FullName)

                    let config = """<?xml version="1.0" encoding="utf-8" ?>
<configuration>
  <appSettings>
    <add key="Prerelease" value="True"/>
  </appSettings>
</configuration>"""
                    File.WriteAllText(configFileName.FullName, config)
                with
                | _ ->()
                )

    /// Pulls new paket.targets and bootstrapper and puts them into .paket folder.
    member this.TurnOnAutoRestore(fromBootstrapper: bool): unit =
        RunInLockedAccessMode(
            this.RootPath,
            fun () -> VSIntegration.TurnOnAutoRestore fromBootstrapper |> this.Process)

    /// Pulls new paket.targets and bootstrapper and puts them into .paket folder.
    member this.TurnOnAutoRestore(): unit =
        this.TurnOnAutoRestore(false)

    /// Removes paket.targets file and Import section from project files.
    member this.TurnOffAutoRestore(): unit =
        RunInLockedAccessMode(
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
        let lockFile = this.GetLockFile()
        let resolved = lockFile.GetGroupedResolution()
        referencesFile
        |> lockFile.GetPackageHull
        |> Seq.map (fun kv ->
                        let groupName,packageName = kv.Key
                        groupName.ToString(),packageName.ToString(),resolved.[kv.Key].Version.ToString())
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
            match group.Resolution.TryFind(packageName) with
            | None -> failwithf "Package %O is not installed in group %O." packageName groupName
            | Some resolvedPackage ->
                let packageName = resolvedPackage.Name
                let groupFolder = if groupName = Constants.MainDependencyGroup then "" else "/" + groupName.CompareString
                let folder = DirectoryInfo(sprintf "%s/packages%s/%O" this.RootPath groupFolder packageName)
                let nuspec = FileInfo(sprintf "%s/packages%s/%O/%O.nuspec" this.RootPath groupFolder packageName packageName)
                let nuspec = Nuspec.Load nuspec.FullName
                let files = NuGetV2.GetLibFiles(folder.FullName)
                InstallModel.CreateFromLibs(packageName, resolvedPackage.Version, Paket.Requirements.FrameworkRestriction.NoRestriction, files, [], [], nuspec)

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
            |> Seq.map (fun kv -> dependenciesFile.GetDependenciesInGroup(kv.Value.Name) |> Seq.map (fun kv' -> kv.Key, kv'.Key)  |> Seq.toList)
            |> List.concat

        this.GetLockFile().GetGroupedResolution()
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
        let resolvedPackages = this.GetLockFile().GetGroupedResolution()
        let package = resolvedPackages.[groupName, (PackageName packageName)]
        let normalizedDependencies = package.Dependencies |> Seq.map (fun (name,_,_) -> groupName, name) |> Seq.toList

        resolvedPackages
        |> Seq.filter (fun kv -> normalizedDependencies |> Seq.exists ((=) kv.Key))
        |> listPackages

    /// Removes the given package from the main dependency group of the dependencies file.
    member this.Remove(package: string): unit = this.Remove(None, package)

    /// Removes the given package from dependencies file.
    member this.Remove(groupName, package: string): unit = this.Remove(groupName, package, false, false, true)

    /// Removes the given package from dependencies file.
    member this.Remove(groupName, package: string, force: bool, interactive: bool,installAfter: bool): unit =
        RunInLockedAccessMode(
            this.RootPath,
            fun () -> RemoveProcess.Remove(dependenciesFileName, groupName, PackageName package, force, interactive, installAfter))

    /// Removes the given package from the specified project
    member this.RemoveFromProject(groupName,package: string,force: bool, projectName: string,installAfter: bool): unit =
        RunInLockedAccessMode(
            this.RootPath,
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
        |> Seq.choose (fun source -> 
            match source with 
            | NuGetV2 s ->
                let res = NuGetV3.getSearchAPI(s.Authentication,s.Url) |> Async.AwaitTask |> Async.RunSynchronously
                match res with
                | Some _ -> Some(NuGetV3.FindPackages(s.Authentication, s.Url, searchTerm, maxResults))
                | None ->  Some(NuGetV2.FindPackages(s.Authentication, s.Url, searchTerm, maxResults))
            | NuGetV3 s -> Some(NuGetV3.FindPackages(s.Authentication, s.Url, searchTerm, maxResults))
            | LocalNuGet(s,_) -> 
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

    static member FindPackageVersions(root,sources:PackageSource seq,name:string,?maxResults,?alternativeProjectRoot) =
        let maxResults = defaultArg maxResults 1000
        let sources = 
            match sources |> Seq.toList |> List.distinct with
            | [] -> [PackageSources.DefaultNuGetSource]
            | sources -> sources
            |> List.distinct
        
        let versions = 
            NuGetV2.GetVersions true alternativeProjectRoot root (sources, PackageName name)
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
    member this.Pack(outputPath, ?buildConfig, ?buildPlatform, ?version, ?specificVersions, ?releaseNotes, ?templateFile, ?workingDir, ?excludedTemplates, ?lockDependencies, ?minimumFromLockFile, ?pinProjectReferences, ?symbols, ?includeReferencedProjects, ?projectUrl) =
        let dependenciesFile = DependenciesFile.ReadFromFile dependenciesFileName
        let specificVersions = defaultArg specificVersions Seq.empty
        let workingDir = defaultArg workingDir (dependenciesFile.FileName |> Path.GetDirectoryName)
        let lockDependencies = defaultArg lockDependencies false
        let minimumFromLockFile = defaultArg minimumFromLockFile false
        let pinProjectReferences = defaultArg pinProjectReferences false
        let symbols = defaultArg symbols false
        let includeReferencedProjects = defaultArg includeReferencedProjects false
        let projectUrl = defaultArg (Some(projectUrl)) None
        PackageProcess.Pack(workingDir, dependenciesFile, outputPath, buildConfig, buildPlatform, version, specificVersions, releaseNotes, templateFile, excludedTemplates, lockDependencies, minimumFromLockFile, pinProjectReferences, symbols, includeReferencedProjects, projectUrl)

    /// Pushes a nupkg file.
    static member Push(packageFileName, ?url, ?apiKey, (?endPoint: string), ?maxTrials) =
        let urlWithEndpoint = RemoteUpload.GetUrlWithEndpoint url endPoint
        let envKey = Environment.GetEnvironmentVariable("nugetkey") |> Option.ofObj 
        let configKey = url |> Option.bind ConfigFile.GetAuthentication |> Option.bind (fun a -> match a with Token t -> Some t | _ -> None )
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
            RemoteUpload.Push maxTrials urlWithEndpoint apiKey packageFileName

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
    static member FixNuspecs (referencesFile:string, nuspecFileList:string list) =

        for nuspecFile in nuspecFileList do
            if not (File.Exists nuspecFile) then
                failwithf "Specified file '%s' does not exist." nuspecFile

        for nuspecFile in nuspecFileList do
            let nuspecText = File.ReadAllText nuspecFile

            let doc =
                try let doc = Xml.XmlDocument() in doc.LoadXml nuspecText
                    doc
                with exn -> failwithf "Could not parse nuspec file '%s'.%sMessage: %s" nuspecFile Environment.NewLine exn.Message

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


module PublicAPI =
    /// Takes a version string formatted for Semantic Versioning and parses it
    /// into the internal representation used by Paket.
    let ParseSemVer (version:string) = SemVer.Parse version