namespace Paket

open System.IO
open Paket.Logging
open System
open Paket.Domain

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

    /// Tries to locate the paket.dependencies file in the current folder, and if fails then creates one.
    static member LocateOrCreate(): Dependencies =
        try
            Dependencies.Locate()
        with _ ->
            Dependencies.Create(Environment.CurrentDirectory)

    /// Tries to create a paket.dependencies file in the given folder.
    static member Create(): Dependencies = Dependencies.Create(Environment.CurrentDirectory)

    /// Tries to create a paket.dependencies file in the given folder.
    static member Create(path: string): Dependencies =
        let dependenciesFileName = Path.Combine(path,Constants.DependenciesFileName)
        Dependencies(dependenciesFileName)
        
    /// Adds the given package without version requirements to the dependencies file.
    member this.Add(package: string): unit = this.Add(package,"")

    /// Adds the given package with the given version to the dependencies file.
    member this.Add(package: string,version: string): unit = this.Add(package, version, false, false, false, true)

    /// Adds the given package with the given version to the dependencies file.
    member this.Add(package: string,version: string,force: bool,hard: bool,interactive: bool,installAfter: bool): unit =
        AddProcess.Add(dependenciesFileName, PackageName package, version, force, hard, interactive, installAfter)
        
    /// Installs all dependencies.
    member this.Install(force: bool,hard: bool): unit = UpdateProcess.Update(dependenciesFileName,false,force,hard)

    /// Updates all dependencies.
    member this.Update(force: bool,hard: bool): unit = UpdateProcess.Update(dependenciesFileName,true,force,hard)

    /// Updates the given package.
    member this.UpdatePackage(package: string,version: string option,force: bool,hard: bool): unit =
        UpdateProcess.UpdatePackage(dependenciesFileName,PackageName package,version,force,hard)

    /// Restores the given paket.references files.
    member this.Restore(files: string list): unit = this.Restore(false,files)

    /// Restores the given paket.references files.
    member this.Restore(force,files: string list): unit = RestoreProcess.Restore(dependenciesFileName,force,files)

    /// Lists outdated packages.
    member this.ShowOutdated(strict: bool,includePrereleases: bool): unit =
        FindOutdated.ShowOutdated(dependenciesFileName,strict,includePrereleases)
    
    /// Pulls new paket.targets and bootstrapper and puts them into .paket folder.
    member this.InitAutoRestore(): unit = VSIntegration.InitAutoRestore(dependenciesFileName)

    /// Converts the current package dependency graph to the simplest dependency graph.
    member this.Simplify(): unit = this.Simplify(false)

    /// Converts the current package dependency graph to the simplest dependency graph.
    member this.Simplify(interactive: bool): unit = Simplifier.Simplify(dependenciesFileName,interactive)

     /// Converts the solution from NuGet to Paket.
    member this.ConvertFromNuget(force: bool,installAfter: bool,initAutoRestore: bool,credsMigrationMode: string option): unit =
        let credsMigrationMode = credsMigrationMode |> Option.map NuGetConvert.CredsMigrationMode.Parse
        NuGetConvert.ConvertFromNuget(dependenciesFileName, force, installAfter, initAutoRestore, credsMigrationMode)

    /// Returns the installed version of the given package.
    member this.GetInstalledVersion(packageName: string): string option =
        getLockFile().ResolvedPackages.TryFind (NormalizedPackageName (PackageName packageName))
        |> Option.map (fun package -> package.Version.ToString())

    /// Returns the installed versions of all installed packages.
    member this.GetInstalledPackages(): (string * string) list =
        getLockFile().ResolvedPackages
        |> listPackages

    /// Returns the installed versions of all direct dependencies.
    member this.GetDirectDependencies(): (string * string) list =
        let dependenciesFile = DependenciesFile.ReadFromFile dependenciesFileName
        let normalizedDependecies = dependenciesFile.DirectDependencies |> Seq.map (fun kv -> kv.Key) |> Seq.map NormalizedPackageName |> Seq.toList
        getLockFile().ResolvedPackages
        |> Seq.filter (fun kv -> normalizedDependecies |> Seq.exists ((=) kv.Key))
        |> listPackages

    /// Removes the given package from dependencies file.
    member this.Remove(package: string): unit = this.Remove(package, false, false, false, true)
    
    /// Removes the given package from dependencies file.
    member this.Remove(package: string,force: bool,hard: bool,interactive: bool,installAfter: bool): unit =
        RemoveProcess.Remove(dependenciesFileName, PackageName package, force, hard, interactive, installAfter)

    /// Shows all references for the given packages.
    member this.ShowReferencesFor(packages: string list): unit =
        FindReferences.ShowReferencesFor(dependenciesFileName,packages |> List.map PackageName)
    /// Finds all references for a given package.
    member this.FindReferencesFor(package: string): string list =
        FindReferences.FindReferencesForPackage(dependenciesFileName, PackageName package)
    
