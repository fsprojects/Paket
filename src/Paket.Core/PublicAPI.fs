namespace Paket

open System.IO
open Paket.Logging

/// Paket API which is optimized for F# Interactive use.
type Dependencies(dependenciesFileName) =
    let rootPath = Path.GetDirectoryName dependenciesFileName
    
    /// Tries to locate the paket.dependencies file in one the given folder or a parent folder.
    static member Locate(path) = 
        let dependenciesFileName = Settings.FindDependenciesFileInPath true (DirectoryInfo path)
        tracefn "found: %s" dependenciesFileName
        Dependencies(dependenciesFileName)
        
    /// Adds the given package without version requirements to the dependencies file.
    member this.Add(package) = this.Add(package,"")

    /// Adds the given package with the given version to the dependencies file.
    member this.Add(package,version) = this.Add(package, version, false, false, false, true)

    /// Adds the given package with the given version to the dependencies file.
    member this.Add(package,version,force,hard,interactive,installAfter) = AddProcess.Add(dependenciesFileName, package, version, force, hard, interactive, installAfter)
        
    /// Install all dependencies
    member this.Install(force,hard) = UpdateProcess.Update(dependenciesFileName,false,force,hard)

    /// Update all dependencies
    member this.Update(force,hard) = UpdateProcess.Update(dependenciesFileName,true,force,hard)

    /// Updates the given package
    member this.UpdatePackage(package,version,force,hard) = UpdateProcess.UpdatePackage(dependenciesFileName,package,version,force,hard) 

    /// Restore given files
    member this.Restore(files) = this.Restore(false,files) 

    /// Restore given files
    member this.Restore(force,files) = RestoreProcess.Restore(dependenciesFileName,force,files) 

    /// Returns the lock file.
    member this.GetLockFile() = 
        let dependenciesFile = DependenciesFile.ReadFromFile dependenciesFileName
        let lockFileName = DependenciesFile.FindLockfile dependenciesFileName
        LockFile.LoadFrom(lockFileName.FullName)

    /// Identify outdated packages    
    member this.ListOutdated(strict,includePrereleases) = FindOutdated.ListOutdated(dependenciesFileName,strict,includePrereleases)

    /// Pull new paket.targets and bootstrapper
    member this.InitAutoRestore() = VSIntegration.InitAutoRestore(dependenciesFileName)

    /// Convert the current package dependency graph to the simplest dependency graph
    member this.Simplify() = this.Simplify(false)

    /// Convert the current package dependency graph to the simplest dependency graph
    member this.Simplify(interactive) = Simplifier.Simplify(dependenciesFileName,interactive)

     /// Convert from nuget usage to paket
    member this.ConvertFromNuget(force,installAfter,initAutoRestore,credsMigrationMode) =
        NuGetConvert.ConvertFromNuget(dependenciesFileName, force, installAfter, initAutoRestore, credsMigrationMode)

    /// Returns the installed version of the given package.
    member this.GetInstalledVersion(packageName) = 
        this.GetLockFile().ResolvedPackages.TryFind packageName 
        |> Option.map (fun package -> package.Version.ToString())

    /// Returns the installed versions of all installed packages.
    member this.GetInstalledPackages() = 
        this.GetLockFile().ResolvedPackages
        |> Seq.map (fun kv -> kv.Value.Name,kv.Value.Version.ToString())
        |> Seq.toList

    /// Returns the installed versions of all direct dependencies.
    member this.GetDirectDependencies() = 
        let dependenciesFile = DependenciesFile.ReadFromFile dependenciesFileName
        this.GetLockFile().ResolvedPackages
        |> Seq.filter (fun kv -> dependenciesFile.DirectDependencies.ContainsKey kv.Key)
        |> Seq.map (fun kv -> kv.Value.Name,kv.Value.Version.ToString())        
        |> Seq.toList

    /// Removes the given package from dependencies file.
    member this.Remove(package) = this.Remove(package, false, false, false, true)
    
    /// Removes the given package from dependencies file.
    member this.Remove(package,force,hard,interactive,installAfter) = RemoveProcess.Remove(dependenciesFileName, package, force, hard, interactive, installAfter)

    /// Show references for the given packages.
    member this.ShowReferencesFor(packages:string list) = FindReferences.ShowReferencesFor(dependenciesFileName,packages)

    /// Find all references for a given package.
    member this.FindReferencesFor(package) = FindReferences.FindReferencesForPackage(dependenciesFileName, package)
    