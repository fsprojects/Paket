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
            UpdateProcess.updateWithModifiedDependenciesFile(dependenciesFile,package,force)
        else
            let lockFileName = DependenciesFile.FindLockfile dependenciesFileName
            LockFile.LoadFrom(lockFileName.FullName)
    
    if interactive then
        let (PackageName package) = package
        for project in ProjectFile.FindAllProjects(Path.GetDirectoryName lockFile.FileName) do
            if Utils.askYesNo(sprintf "  Install to %s?" project.Name) then
                let proj = FileInfo(project.FileName)
                match ProjectFile.FindReferencesFile proj with
                | None ->
                    let newFileName =
                        let fi = FileInfo(Path.Combine(proj.Directory.FullName,Constants.ReferencesFile))
                        if fi.Exists then
                            Path.Combine(proj.Directory.FullName,proj.Name + "." + Constants.ReferencesFile)
                        else
                            fi.FullName

                    File.WriteAllLines(newFileName,[package])
                | Some fileName -> File.AppendAllLines(fileName,["";package])

    if installAfter then
        let sources = dependenciesFile.GetAllPackageSources()
        InstallProcess.Install(sources, force, hard, lockFile)

    if changed then
        dependenciesFile.Save()