/// Contains methods to remove of installed packages
module Paket.RemoveProcess

open Paket
open System.IO
open Paket.Domain

let Remove(dependenciesFileName, package:PackageName, force, hard, interactive, installAfter) =
    let (PackageName name) = package
    let root = Path.GetDirectoryName dependenciesFileName
    let allProjects = ProjectFile.FindAllProjects root
    for project in allProjects do
        let proj = FileInfo(project.FileName)
        match ProjectFile.FindReferencesFile proj with
        | None -> ()
        | Some fileName -> 
            let lines = File.ReadAllLines(fileName)
            let installed = lines |> Seq.exists (fun l -> l.ToLower() = name.ToLower())
            if installed then
                if (not interactive) || Utils.askYesNo(sprintf "  Remove from %s?" project.Name) then
                    let newLines = lines |> Seq.filter (fun l -> l.ToLower() <> name.ToLower())
                    File.WriteAllLines(fileName,newLines)

    // check we have it removed from paket.references files
    for project in allProjects do
        let proj = FileInfo(project.FileName)
        match ProjectFile.FindReferencesFile proj with
        | None -> ()
        | Some fileName -> 
            let lines = File.ReadAllLines(fileName)
            let installed = lines |> Seq.exists (fun l -> l.ToLower() = name.ToLower())
            if installed then
                failwithf "%s is still installed in %s" name project.Name

    let exisitingDependenciesFile = DependenciesFile.ReadFromFile dependenciesFileName
    let dependenciesFile =
        exisitingDependenciesFile
          .Remove(package)

    let changed = exisitingDependenciesFile <> dependenciesFile
    let lockFile = 
        if changed then
            UpdateProcess.updateWithModifiedDependenciesFile(true,dependenciesFile,package,force)
        else
            let lockFileName = DependenciesFile.FindLockfile dependenciesFileName
            LockFile.LoadFrom(lockFileName.FullName)
    
    if installAfter then
        let sources = DependenciesFile.ReadFromFile(dependenciesFileName).GetAllPackageSources()
        InstallProcess.Install(sources, force, hard, lockFile)

    if changed then
        dependenciesFile.Save()