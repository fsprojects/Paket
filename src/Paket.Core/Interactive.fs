namespace Paket

open System.IO

/// Paket API which is optimized for REPL use.
type Dependencies private () =
    /// Tries to locate the paket.dependencies file in one the given folder or a parent folder.
    static member Locate(path) = 
        Settings.DependenciesFile <- Settings.findDependenciesFileInPath true (DirectoryInfo path)

    /// Adds the given package without version requirements to the dependencies file.
    static member Add(package) = Dependencies.Add(package,"")

    /// Adds the given package with the given version to the dependencies file.
    static member Add(package,version) = AddProcess.Add(package, version, false, false, false, false)