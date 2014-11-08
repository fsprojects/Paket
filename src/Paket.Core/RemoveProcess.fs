/// Contains methods to remove of installed packages
module Paket.RemoveProcess

open Paket
open System.IO

let Remove(package:string, force, hard, interactive, installAfter) =
    let allProjects = ProjectFile.FindAllProjects(".")
    for project in allProjects do
        let proj = FileInfo(project.FileName)
        match ProjectFile.FindReferencesFile proj with
        | None -> ()
        | Some fileName -> 
            let lines = File.ReadAllLines(fileName)
            let installed = lines |> Seq.exists (fun l -> l.ToLower() = package.ToLower())
            if installed then
                if (not interactive) || Utils.askYesNo(sprintf "  Remove from %s?" project.Name) then
                    let newLines = lines |> Seq.filter (fun l -> l.ToLower() <> package.ToLower())
                    File.WriteAllLines(fileName,newLines)

    // check we have it removed from paket.references files
    for project in allProjects do
        let proj = FileInfo(project.FileName)
        match ProjectFile.FindReferencesFile proj with
        | None -> ()
        | Some fileName -> 
            let lines = File.ReadAllLines(fileName)
            let installed = lines |> Seq.exists (fun l -> l.ToLower() = package.ToLower())
            if installed then
                failwithf "%s is still installed in %s" package project.Name

    let exisitingDependenciesFile = DependenciesFile.ReadFromFile(Settings.DependenciesFile)
    let dependenciesFile =
        exisitingDependenciesFile
          .Remove(package)

    let lockFile = 
        if exisitingDependenciesFile = dependenciesFile then
            let lockFileName = DependenciesFile.FindLockfile Settings.DependenciesFile
            LockFile.LoadFrom(lockFileName.FullName)
        else
            UpdateProcess.updateWithModifiedDependenciesFile(dependenciesFile,package,force)
    
    if installAfter then
        let sources =
            Settings.DependenciesFile
            |> File.ReadAllLines
            |> PackageSourceParser.getSources 

        InstallProcess.Install(sources, force, hard, lockFile)

    dependenciesFile.Save()