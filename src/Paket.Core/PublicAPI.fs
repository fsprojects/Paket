namespace Paket

open System.IO
open Paket.Logging
open System
open Paket.Domain
open Paket.Rop

/// Paket API which is optimized for F# Interactive use.
type Dependencies(dependenciesFileName: string) =
    let getLockFile() =
        let lockFileName = DependenciesFile.FindLockfile dependenciesFileName
        LockFile.LoadFrom(lockFileName.FullName)

    let listPackages (packages: System.Collections.Generic.KeyValuePair<_, PackageResolver.ResolvedPackage> seq) =
        packages
        |> Seq.map (fun kv -> kv.Value)
        |> Seq.map (fun p ->
                            let (PackageName name) = p.Name
                            name, p.Version.ToString())
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
                        failwithf "Could not find %s" Constants.DependenciesFileName
                    else 
                        Constants.DependenciesFileName
                else
                   findInPath(parent, withError)

        let dependenciesFileName = findInPath(DirectoryInfo path,true)
        tracefn "found: %s" dependenciesFileName
        Dependencies(dependenciesFileName)
        
    /// Tries to create a paket.dependencies file in the given folder.
    static member Create(): Dependencies = Dependencies.Create(Environment.CurrentDirectory)

    /// Tries to create a paket.dependencies file in the given folder.
    static member Create(path: string): Dependencies =
        let dependenciesFileName = Path.Combine(path,Constants.DependenciesFileName)
        if File.Exists dependenciesFileName then
            Logging.tracefn "%s already exists" dependenciesFileName
        else
            DependenciesFile(dependenciesFileName, InstallOptions.Default, [], [], []).Save()
        Dependencies(dependenciesFileName)
     
    /// Converts the solution from NuGet to Paket.
    static member ConvertFromNuget(force: bool,installAfter: bool,initAutoRestore: bool,credsMigrationMode: string option) : unit =
        let dependenciesFileName = 
            try 
                Dependencies.Locate().DependenciesFile
            with _ -> Path.Combine(Environment.CurrentDirectory, Constants.DependenciesFileName)
        
        let result = 
            Utils.RunInLockedAccessMode(
                Path.GetDirectoryName(dependenciesFileName),
                fun () ->  NuGetConvert.convert(dependenciesFileName, force, credsMigrationMode))
        
        let stringErr = function
        | NuGetConvert.ConvertMessage.DependenciesFileAlreadyExists fi -> 
            sprintf "%s already exists, use --force to overwrite" fi.FullName
        | NuGetConvert.ConvertMessage.UnknownCredentialsMigrationMode mode -> 
            sprintf "Unknown credentials migration mode: %s" mode
        | NuGetConvert.ConvertMessage.NugetPackagesConfigParseError fi -> 
            sprintf "Unable to parse %s" fi.FullName
        | NuGetConvert.ConvertMessage.ReferencesFileAlreadyExists fi -> 
            sprintf "%s already exists, use --force to overwrite" fi.FullName
        | NuGetConvert.ConvertMessage.NugetConfigFileParseError fi -> 
            sprintf "Unable to parse %s" fi.FullName
        | NuGetConvert.ConvertMessage.DependenciesFileParseError fi ->
            sprintf "Unable to parse %s" fi
        | NuGetConvert.ConvertMessage.PackageSourceParseError source -> 
            sprintf "Unable to parse packages source %s" source

        let remove (fi : FileInfo) = 
            tracefn "Removing %s" fi.FullName
            fi.Delete()

        match result with
        | Rop.Success(result, _) ->
            result.NugetConfigFiles |> List.iter remove
            result.NugetPackagesFiles |> List.map (fun n -> n.File) |> List.iter remove
            result.NugetTargets |> Option.iter remove
            result.NugetExe 
            |> Option.iter 
                   (fun nugetExe -> 
                   remove nugetExe
                   traceWarnfn "Removed %s and added %s as dependency instead. Please check all paths." 
                       nugetExe.FullName "Nuget.CommandLine")
            match result.NugetTargets |> orElse result.NugetExe with
            | Some fi when fi.Directory.EnumerateFileSystemInfos() |> Seq.isEmpty ->
                fi.Directory.Delete()
            | _ -> ()

            result.DependenciesFile.Save()
            result.ReferencesFiles |> List.iter (fun r -> r.Save())
            result.ProjectFiles |> List.iter (fun p -> p.Save())
            result.SolutionFiles |> List.iter (fun s -> s.Save())

            let autoVSPackageRestore = 
                result.NugetConfig.PackageRestoreAutomatic &&
                result.NugetConfig.PackageRestoreEnabled
            if initAutoRestore && (autoVSPackageRestore || result.NugetTargets.IsSome) then 
                VSIntegration.InitAutoRestore dependenciesFileName

            if installAfter then
                UpdateProcess.Update(dependenciesFileName,true,true,true)
            
        | Rop.Failure(msgs) ->
            msgs |> List.map stringErr |> List.iter Logging.traceError
    

    static member SimplifyR(interactive : bool) =
        
        match Paket.Environment.locatePaketRootDirectory(DirectoryInfo(Environment.CurrentDirectory)) with
        | Some rootDir ->
            Utils.RunInLockedAccessMode(
                rootDir.FullName,
                fun () -> 
                    Paket.Environment.fromRootDirectory rootDir
                    >>= Simplifier.ensureNotInStrictMode
                    >>= Simplifier.simplifyR interactive
                    |> either (Simplifier.updateEnvironment) (List.iter (string >> Logging.traceError))
            )
        | None ->
            Logging.traceErrorfn "Unable to find %s in current directory and parent directories" Constants.DependenciesFileName

    /// Get path to dependencies file
    member this.DependenciesFile with get() = dependenciesFileName

    /// Get the root path
    member this.RootPath with get() = Path.GetDirectoryName(dependenciesFileName)

    /// Adds the given package without version requirements to the dependencies file.
    member this.Add(package: string): unit = this.Add(package,"")

    /// Adds the given package with the given version to the dependencies file.
    member this.Add(package: string,version: string): unit = this.Add(package, version, false, false, false, true)

    /// Adds the given package with the given version to the dependencies file.
    member this.Add(package: string,version: string,force: bool,hard: bool,interactive: bool,installAfter: bool): unit =
        Utils.RunInLockedAccessMode(
            this.RootPath,
            fun () -> AddProcess.Add(dependenciesFileName, PackageName package, version, force, hard, interactive, installAfter))
        
    /// Installs all dependencies.
    member this.Install(force: bool,hard: bool,withBindingRedirects:bool): unit = 
        Utils.RunInLockedAccessMode(
            this.RootPath,
            fun () -> UpdateProcess.SmartInstall(dependenciesFileName,force,hard,withBindingRedirects))

    /// Installs all dependencies.
    member this.Install(force: bool,hard: bool): unit = this.Install(force,hard,false)

    /// Updates all dependencies.
    member this.Update(force: bool,hard: bool,withBindingRedirects:bool): unit = 
        Utils.RunInLockedAccessMode(
            this.RootPath,
            fun () -> UpdateProcess.Update(dependenciesFileName,force,hard,withBindingRedirects))

    /// Updates all dependencies.
    member this.Update(force: bool,hard: bool): unit = this.Update(force,hard,false)

    /// Updates the given package.
    member this.UpdatePackage(package: string,version: string option,force: bool,hard: bool): unit =
        Utils.RunInLockedAccessMode(
            this.RootPath,
            fun () -> UpdateProcess.UpdatePackage(dependenciesFileName,PackageName package,version,force,hard,false))

    /// Restores the given paket.references files.
    member this.Restore(files: string list): unit = this.Restore(false,files)

    /// Restores the given paket.references files.
    member this.Restore(force,files: string list): unit =
        Utils.RunInLockedAccessMode(
            this.RootPath,
            fun () -> RestoreProcess.Restore(dependenciesFileName,force,files))

    /// Lists outdated packages.
    member this.ShowOutdated(strict: bool,includePrereleases: bool): unit =
        FindOutdated.ShowOutdated(dependenciesFileName,strict,includePrereleases)

    /// Finds all outdated packages.
    member this.FindOutdated(strict: bool,includePrereleases: bool): (string * SemVerInfo) list =
        FindOutdated.FindOutdated(dependenciesFileName,strict,includePrereleases)
        |> List.map (fun (PackageName p,_,newVersion) -> p,newVersion)
    
    /// Pulls new paket.targets and bootstrapper and puts them into .paket folder.
    member this.InitAutoRestore(): unit = 
        Utils.RunInLockedAccessMode(
            this.RootPath,
            fun () -> VSIntegration.InitAutoRestore(dependenciesFileName))

    /// Converts the current package dependency graph to the simplest dependency graph.
    member this.Simplify(): unit = this.Simplify(false)

    /// Converts the current package dependency graph to the simplest dependency graph.
    member this.Simplify(interactive: bool): unit = 
        let errString = function
        | Simplifier.DependenciesFileMissing -> sprintf "%s file not found." dependenciesFileName
        | Simplifier.StrictModeDetected -> "Strict mode detected. Will not attempt to simplify dependencies."
        | Simplifier.LockFileMissing -> "Lock file not found. Create lock file by running paket install."
        | Simplifier.DependenciesFileParseError -> sprintf "Unable to parse %s." dependenciesFileName
        | Simplifier.LockFileParseError -> "Unable to parse lock file."
        | Simplifier.ReferencesFileParseError(name) -> sprintf "Unable to parse %s" name
        | Simplifier.DependencyNotLocked(PackageName name) -> sprintf "Dependency %s from %s not found in lock file." name dependenciesFileName
        | Simplifier.ReferenceNotLocked(referencesFile, PackageName name) -> sprintf "Reference %s from %s not found in lock file." name referencesFile.FileName
        
        let formatDiff (before : string) (after : string) =
            let nl = Environment.NewLine
            nl + "Before:" + nl + nl + before + nl + nl + nl + "After:" + nl + nl + after + nl + nl

        let simplify(file,before,after) =
            if before <> after then
                File.WriteAllText(file, after)
                tracefn "Simplified %s" file
                traceVerbose (formatDiff before after)
            else
                tracefn "%s is already simplified" file

        let result =
            Utils.RunInLockedAccessMode(
                this.RootPath,
                fun () -> Simplifier.simplify(dependenciesFileName,interactive))

        match result with 
        | Success (result, _) -> 
            let depFileBefore, depFileAfter = result.DependenciesFileSimplifyResult
            simplify(depFileBefore.FileName,depFileBefore.ToString(),depFileAfter.ToString())
            result.ReferencesFilesSimplifyResult
            |> List.map (fun (before,after) -> before.FileName, before.ToString(), after.ToString())
            |> List.iter simplify
        | Failure msgs -> msgs |> List.map errString |> List.iter Logging.traceError

    /// Returns the installed version of the given package.
    member this.GetInstalledVersion(packageName: string): string option =
        getLockFile().ResolvedPackages.TryFind (NormalizedPackageName (PackageName packageName))
        |> Option.map (fun package -> package.Version.ToString())

    /// Returns the installed versions of all installed packages.
    member this.GetInstalledPackages(): (string * string) list =
        getLockFile().ResolvedPackages
        |> listPackages

    /// Returns the installed versions of all direct dependencies which are referneced in the references file
    member this.GetDirectDependencies(referencesFile:ReferencesFile): (string * string) list =
        let normalizedDependecies = referencesFile.NugetPackages |> List.map NormalizedPackageName
        getLockFile().ResolvedPackages
        |> Seq.filter (fun kv -> normalizedDependecies |> Seq.exists ((=) kv.Key))
        |> listPackages

    /// Returns the installed versions of all direct dependencies.
    member this.GetDirectDependencies(): (string * string) list =
        let dependenciesFile = DependenciesFile.ReadFromFile dependenciesFileName
        let normalizedDependencies = dependenciesFile.DirectDependencies |> Seq.map (fun kv -> kv.Key) |> Seq.map NormalizedPackageName |> Seq.toList
        getLockFile().ResolvedPackages
        |> Seq.filter (fun kv -> normalizedDependencies |> Seq.exists ((=) kv.Key))
        |> listPackages

    /// Returns the direct dependencies for the given package.
    member this.GetDirectDependenciesForPackage(packageName:string): (string * string) list =
        let resolvedPackages = getLockFile().ResolvedPackages
        let package = resolvedPackages.[NormalizedPackageName (PackageName packageName)]
        let normalizedDependencies = package.Dependencies |> Seq.map (fun (name,_,_) -> name) |> Seq.map NormalizedPackageName |> Seq.toList
        resolvedPackages
        |> Seq.filter (fun kv -> normalizedDependencies |> Seq.exists ((=) kv.Key))
        |> listPackages

    /// Removes the given package from dependencies file.
    member this.Remove(package: string): unit = this.Remove(package, false, false, false, true)
    
    /// Removes the given package from dependencies file.
    member this.Remove(package: string,force: bool,hard: bool,interactive: bool,installAfter: bool): unit =
        Utils.RunInLockedAccessMode(
            this.RootPath,
            fun () -> RemoveProcess.Remove(dependenciesFileName, PackageName package, force, hard, interactive, installAfter))

    /// Shows all references for the given packages.
    member this.ShowReferencesFor(packages: string list): unit =
        FindReferences.ShowReferencesFor(dependenciesFileName,packages |> List.map PackageName)

    /// Finds all references for a given package.
    member this.FindReferencesFor(package: string): string list =
        FindReferences.FindReferencesForPackage(dependenciesFileName, PackageName package)
