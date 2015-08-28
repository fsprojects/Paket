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
        let dependenciesFile = exisitingDependenciesFile.Remove(package)
        dependenciesFile.Save()
        
        UpdateProcess.SelectiveUpdate(dependenciesFile,false,None,force)
    
    if installAfter then
        let sources = DependenciesFile.ReadFromFile(dependenciesFileName).GetAllPackageSources()
        InstallProcess.Install(sources, InstallerOptions.createLegacyOptions(force, hard, false), lockFile )

// remove a package with the option to remove it from a specified project
let RemoveFromProject(dependenciesFileName, packageName:PackageName, force, hard, projectName, installAfter) =    
    let removeFromSpecifiedProject (projects : ProjectFile seq) =        
        match ProjectFile.TryFindProject(projects,projectName) with
        | Some p ->
            if p.HasPackageInstalled(Constants.MainDependencyGroup,packageName) then
                packageName |> removePackageFromProject p
            else traceWarnfn "Package %O was not installed in project %s" packageName p.Name
        | None ->
            traceErrorfn "Could not install package in specified project %s. Project not found" projectName

    remove removeFromSpecifiedProject dependenciesFileName packageName force hard installAfter

// remove a package with the option to interactively remove it from multiple projects
let Remove(dependenciesFileName, packageName:PackageName, force, hard, interactive, installAfter) =
    
    let removeFromProjects (projects: ProjectFile seq) =
        for project in projects do        
            if project.HasPackageInstalled(Constants.MainDependencyGroup,packageName) then
                if (not interactive) || Utils.askYesNo(sprintf "  Remove from %s?" project.Name) then
                    packageName |> removePackageFromProject project

    remove removeFromProjects dependenciesFileName packageName force hard installAfter
