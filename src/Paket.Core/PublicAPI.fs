namespace Paket

open System.IO
open Paket.Logging

/// Paket API which is optimized for F# Interactive use.
type Dependencies private () =
    /// Tries to locate the paket.dependencies file in one the given folder or a parent folder.
    static member Locate(path) = 
        Settings.DependenciesFile <- Settings.findDependenciesFileInPath true (DirectoryInfo path)
        tracefn "found: %s" Settings.DependenciesFile

    /// Adds the given package without version requirements to the dependencies file.
    static member Add(package) = Dependencies.Add(package,"")

    /// Adds the given package with the given version to the dependencies file.
    static member Add(package,version) = AddProcess.Add(package, version, false, false, false, true)
    
    /// Returns the installed version of the given package.
    static member GetInstalledVersion(packageName) = 
        let dependenciesFile = DependenciesFile.ReadFromFile(Settings.DependenciesFile)
        let lockFileName = DependenciesFile.FindLockfile Settings.DependenciesFile
        let lockFile = LockFile.LoadFrom(lockFileName.FullName)
        lockFile.ResolvedPackages.TryFind packageName 
        |> Option.map (fun package -> package.Version.ToString())

    /// Returns the installed versions of all installed packages.
    static member GetInstalledPackages() = 
        let dependenciesFile = DependenciesFile.ReadFromFile(Settings.DependenciesFile)
        let lockFileName = DependenciesFile.FindLockfile Settings.DependenciesFile
        let lockFile = LockFile.LoadFrom(lockFileName.FullName)
        lockFile.ResolvedPackages
        |> Seq.map (fun kv -> kv.Value.Name,kv.Value.Version.ToString())
        |> Seq.toList

    /// Returns the installed versions of all direct dependencies.
    static member GetDirectDependencies() = 
        let dependenciesFile = DependenciesFile.ReadFromFile(Settings.DependenciesFile)
        let lockFileName = DependenciesFile.FindLockfile Settings.DependenciesFile
        let lockFile = LockFile.LoadFrom(lockFileName.FullName)
        lockFile.ResolvedPackages
        |> Seq.filter (fun kv -> dependenciesFile.DirectDependencies.ContainsKey kv.Key)
        |> Seq.map (fun kv -> kv.Value.Name,kv.Value.Version.ToString())        
        |> Seq.toList

    /// Removes the given package from dependencies file.
    static member Remove(package) = RemoveProcess.Remove(package, false, false, false, true)
    