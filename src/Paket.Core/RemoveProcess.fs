/// Contains methods to remove of installed packages
module Paket.RemoveProcess

open Paket
open System.IO
open Paket.Domain
open Paket.Logging

let private removePackageFromProject (project : ProjectFile) package = 
    ProjectFile.FindOrCreateReferencesFile(FileInfo(project.FileName))
        .RemoveNuGetReference(package)
        .Save()

let private remove removeFromProjects dependenciesFileName (package: PackageName) force hard installAfter = 
    let (PackageName name) = package
    let root = Path.GetDirectoryName dependenciesFileName
    let allProjects = ProjectFile.FindAllProjects root
    
    removeFromProjects allProjects
            
    // check we have it removed from all paket.references files
    let stillInstalled =
        allProjects
        |> Seq.exists (fun project -> 
            let proj = FileInfo(project.FileName)
            match ProjectFile.FindReferencesFile proj with
            | None -> false 
            | Some fileName -> 
                let lines = File.ReadAllLines(fileName)
                lines |> Seq.exists (fun l -> l.ToLower() = name.ToLower()))

    let oldLockFile =    
        let lockFileName = DependenciesFile.FindLockfile dependenciesFileName
        LockFile.LoadFrom(lockFileName.FullName)

    let lockFile =
        if stillInstalled then oldLockFile else
        let exisitingDependenciesFile = DependenciesFile.ReadFromFile dependenciesFileName
        let dependenciesFile =
            exisitingDependenciesFile
                .Remove(package)

        dependenciesFile.Save()
        
        UpdateProcess.SelectiveUpdate(dependenciesFile,None,force)
    
    if installAfter then
        let sources = DependenciesFile.ReadFromFile(dependenciesFileName).GetAllPackageSources()
        InstallProcess.Install(sources, force, hard, false, lockFile)

// remove a package with the option to remove it from a specified project
let RemoveFromProject(dependenciesFileName, package:PackageName, force, hard, projectName, installAfter) =
    
    let removeFromSpecifiedProject (projects : ProjectFile seq) =    
        let project = 
            projects |> Seq.tryFind (fun p -> p.NameWithoutExtension = projectName || p.Name = projectName)

        match project with
        | Some p ->
            if p.HasPackageInstalled(NormalizedPackageName package) then
                package |> removePackageFromProject p
            else traceWarnfn "Package %s was not installed in project %s" package.Id p.Name
        | None ->
            traceErrorfn "Could not install package in specified project %s. Project not found" projectName

    remove removeFromSpecifiedProject dependenciesFileName package force hard installAfter

// remove a package with the option to interactively remove it from multiple projects
let Remove(dependenciesFileName, package:PackageName, force, hard, interactive, installAfter) =
    
    let removeFromProjects (projects: ProjectFile seq) =
        for project in projects do        
            if project.HasPackageInstalled(NormalizedPackageName package) then
                if (not interactive) || Utils.askYesNo(sprintf "  Remove from %s?" project.Name) then
                    package |> removePackageFromProject project

    remove removeFromProjects dependenciesFileName package force hard installAfter
