/// Contains methods for addition of new packages
module Paket.AddProcess

open Paket
open System.IO
open Paket.Domain

let Add(dependenciesFileName, package, version, force, hard, interactive, installAfter) =
    let exisitingDependenciesFile = DependenciesFile.ReadFromFile(dependenciesFileName)
    let dependenciesFile =
        exisitingDependenciesFile
          .Add(package,version)

    let changed = exisitingDependenciesFile <> dependenciesFile
    let lockFile = 
        if changed then
            UpdateProcess.updateWithModifiedDependenciesFile(false,dependenciesFile,package,force)
        else
            let lockFileName = DependenciesFile.FindLockfile dependenciesFileName
            LockFile.LoadFrom(lockFileName.FullName)
    
    if interactive then
        let (PackageName packageName) = package
        for project in ProjectFile.FindAllProjects(Path.GetDirectoryName lockFile.FileName) do
            if Utils.askYesNo(sprintf "  Install to %s?" project.Name) then
                ProjectFile.FindOrCreateReferencesFile(FileInfo(project.FileName))
                    .AddNuGetReference(package)
                    .Save()

    if installAfter then
        let sources = dependenciesFile.GetAllPackageSources()
        InstallProcess.Install(sources, force, hard, false, lockFile)

    if changed then
        dependenciesFile.Save()