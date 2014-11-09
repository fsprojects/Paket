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
    member this.Add(package,version) = AddProcess.Add(dependenciesFileName, package, version, false, false, false, true)
        
    /// Install all dependencies
    member this.Install() = UpdateProcess.Update(dependenciesFileName, false, false, false) 

    /// Update all dependencies
    member this.Update() = UpdateProcess.Update(dependenciesFileName, true, false, false)

    /// Update given package
    member this.UpdatePackage(package,version) = UpdateProcess.UpdatePackage(dependenciesFileName,package,version,false,false) 

    /// Restore given files
    member this.Restore(files) = RestoreProcess.Restore(dependenciesFileName,false,files) 

    /// Returns the lock file.
    member this.GetLockFile() = 
        let dependenciesFile = DependenciesFile.ReadFromFile dependenciesFileName
        let lockFileName = DependenciesFile.FindLockfile dependenciesFileName
        LockFile.LoadFrom(lockFileName.FullName)

    /// Identity outdated packages    
    member this.ListOutdated() = FindOutdated.ListOutdated(dependenciesFileName,false,false)

    /// Pull new paket.targets and bootstrapper
    member this.InitAutoRestore() = VSIntegration.InitAutoRestore(dependenciesFileName)

    /// Converts all projects from NuGet to Paket
    member this.ConvertFromNuGet() = NuGetConvert.ConvertFromNuget(dependenciesFileName,false,false |> not,false |> not)

    /// Convert the current package dependency graph to the simplest dependency graph
    member this.Simplify() = Simplifier.Simplify(dependenciesFileName,false)

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
    member this.Remove(package) = RemoveProcess.Remove(dependenciesFileName, package, false, false, false, true)

    /// Show references for given packages    
    member this.FindReferencesFor(packages:string list) = FindReferences.ShowReferencesFor(dependenciesFileName,packages)

    /// Find all references for a given package.
    member this.FindReferencesFor(package) = FindReferences.FindReferencesForPackage(dependenciesFileName, package)
    