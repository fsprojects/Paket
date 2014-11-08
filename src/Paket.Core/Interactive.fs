namespace Paket

open System.IO
open Paket.Logging

/// Paket API which is optimized for REPL use.
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
    static member GetInstalledVersion(package) = 
        let dependenciesFile = DependenciesFile.ReadFromFile(Settings.DependenciesFile)
        let lockFileName = DependenciesFile.FindLockfile Settings.DependenciesFile
        let lockFile = LockFile.LoadFrom(lockFileName.FullName)
        lockFile.ResolvedPackages.[package].Version.ToString()

    