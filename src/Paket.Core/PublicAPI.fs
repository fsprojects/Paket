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

    /// Returns the lock file.
    member this.GetLockFile() = 
        let dependenciesFile = DependenciesFile.ReadFromFile dependenciesFileName
        let lockFileName = DependenciesFile.FindLockfile dependenciesFileName
        LockFile.LoadFrom(lockFileName.FullName)
    
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
    