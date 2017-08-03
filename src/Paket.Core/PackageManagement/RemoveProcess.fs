/// Contains methods to remove installed packages
module Paket.RemoveProcess

open Paket
open System.IO
open Pri.LongPath
open Paket.Domain
open Paket.Logging
open InstallProcess

let private removePackageFromProject (project : ProjectFile) groupName package = 
    project.FindOrCreateReferencesFile()
        .RemoveNuGetReference(groupName,package)
        .Save()

let private remove removeFromProjects dependenciesFileName alternativeProjectRoot groupName (package: PackageName) force installAfter = 
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
                let refFile = ReferencesFile.FromFile fileName
                match refFile.Groups |> Map.tryFind groupName with
                | None -> false
                | Some group -> group.NugetPackages |> Seq.exists (fun p -> p.Name = package))

    let oldLockFile =
        let lockFileName = DependenciesFile.FindLockfile dependenciesFileName
        LockFile.LoadFrom(lockFileName.FullName)

    let dependenciesFile,lockFile,_ =
        let exisitingDependenciesFile = DependenciesFile.ReadFromFile dependenciesFileName
        if stillInstalled then exisitingDependenciesFile,oldLockFile,false else
        let dependenciesFile = exisitingDependenciesFile.Remove(groupName,package)
        dependenciesFile.Save()
        
        let lockFile,hasChanged,_ = UpdateProcess.SelectiveUpdate(dependenciesFile, alternativeProjectRoot, PackageResolver.UpdateMode.Install,SemVerUpdateMode.NoRestriction,force)
        dependenciesFile,lockFile,hasChanged
    
    if installAfter then
        let updatedGroups = Map.add groupName 0 Map.empty
        InstallProcess.Install(InstallerOptions.CreateLegacyOptions(force, false, false, false, SemVerUpdateMode.NoRestriction, false, false, [], [], None), false, dependenciesFile, lockFile, updatedGroups)
        GarbageCollection.CleanUp(root, dependenciesFile, lockFile)

/// Removes a package with the option to remove it from a specified project.
let RemoveFromProject(dependenciesFileName, groupName, packageName:PackageName, force, projectName, installAfter) =
    let groupName = 
        match groupName with
        | None -> Constants.MainDependencyGroup
        | Some name -> GroupName name

    let removeFromSpecifiedProject (projects : ProjectFile seq) =
        match ProjectFile.TryFindProject(projects,projectName) with
        | Some p ->
            if p.HasPackageInstalled(groupName,packageName) then
                removePackageFromProject p groupName packageName
            else traceWarnfn "Package %O was not installed in project %s in group %O" packageName p.FileName groupName
        | None ->
            traceErrorfn "Could not remove package %O from specified project %s. Project not found" packageName projectName
    let alternativeProjectRoot = None
    remove removeFromSpecifiedProject dependenciesFileName alternativeProjectRoot groupName packageName force installAfter

/// Remove a package with the option to interactively remove it from multiple projects.
let Remove(dependenciesFileName, groupName, packageName:PackageName, force, interactive, installAfter) =
    let groupName = 
        match groupName with
        | None -> Constants.MainDependencyGroup
        | Some name -> GroupName name

    let removeFromProjects (projects: ProjectFile seq) =
        for project in projects do
            if project.HasPackageInstalled(groupName,packageName) then
                if (not interactive) || Utils.askYesNo(sprintf "  Remove from %s (group %O)?" project.FileName groupName) then
                    removePackageFromProject project groupName packageName

    let alternativeProjectRoot = None
    remove removeFromProjects dependenciesFileName alternativeProjectRoot groupName packageName force installAfter
