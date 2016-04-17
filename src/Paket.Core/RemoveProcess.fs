/// Contains methods to remove installed packages
module Paket.RemoveProcess

open Paket
open System.IO
open Paket.Domain
open Paket.Logging
open InstallProcess

let private removePackageFromProject (project : ProjectType) groupName package = 
    project.FindOrCreateReferencesFile()
        .RemoveNuGetReference(groupName,package)
        .Save()

let private remove removeFromProjects dependenciesFileName groupName (package: PackageName) force installAfter = 
    let root = Path.GetDirectoryName dependenciesFileName
    let allProjects = ProjectType.FindAllProjects root
    
    removeFromProjects allProjects
            
    // check we have it removed from all paket.references files
    let stillInstalled =
        allProjects 
        |> Seq.exists (fun project -> 
            let proj = FileInfo(project.FileName)
            match ProjectType.FindReferencesFile proj with
            | None -> false 
            | Some fileName -> 
                let refFile = ReferencesFile.FromFile fileName
                match refFile.Groups |> Map.tryFind groupName with
                | None -> false
                | Some group -> group.NugetPackages |> Seq.exists (fun p -> p.Name = package))

    let oldLockFile =
        let lockFileName = DependenciesFile.FindLockfile dependenciesFileName
        LockFile.LoadFrom(lockFileName.FullName)

    let dependenciesFile,lockFile,hasChanged =
        let exisitingDependenciesFile = DependenciesFile.ReadFromFile dependenciesFileName
        if stillInstalled then exisitingDependenciesFile,oldLockFile,false else
        let dependenciesFile = exisitingDependenciesFile.Remove(groupName,package)
        dependenciesFile.Save()
        
        let lockFile,hasChanged,_ = UpdateProcess.SelectiveUpdate(dependenciesFile,PackageResolver.UpdateMode.UpdateGroup groupName,SemVerUpdateMode.NoRestriction,force)
        dependenciesFile,lockFile,hasChanged
    
    if installAfter then
        let updatedGroups = Map.add groupName 0 Map.empty
        InstallProcess.Install(InstallerOptions.CreateLegacyOptions(force, false, false, SemVerUpdateMode.NoRestriction, false), false, dependenciesFile, lockFile, updatedGroups)
        GarbageCollection.CleanUp(root, dependenciesFile, lockFile)

/// Removes a package with the option to remove it from a specified project.
let RemoveFromProject(dependenciesFileName, groupName, packageName:PackageName, force, projectName, installAfter) =
    let groupName = 
        match groupName with
        | None -> Constants.MainDependencyGroup
        | Some name -> GroupName name

    let removeFromSpecifiedProject (projects : ProjectType seq) =
        match ProjectType.TryFindProject(projects,projectName) with
        | Some p ->
            if p.HasPackageInstalled(groupName,packageName) then
                removePackageFromProject p groupName packageName
            else traceWarnfn "Package %O was not installed in project %s in group %O" packageName p.FileName groupName
        | None ->
            traceErrorfn "Could not remove package %O from specified project %s. Project not found" packageName projectName

    remove removeFromSpecifiedProject dependenciesFileName groupName packageName force installAfter

/// Remove a package with the option to interactively remove it from multiple projects.
let Remove(dependenciesFileName, groupName, packageName:PackageName, force, interactive, installAfter) =
    let groupName = 
        match groupName with
        | None -> Constants.MainDependencyGroup
        | Some name -> GroupName name

    let removeFromProjects (projects: ProjectType seq) =
        for project in projects do
            if project.HasPackageInstalled(groupName,packageName) then
                if (not interactive) || Utils.askYesNo(sprintf "  Remove from %s (group %O)?" project.FileName groupName) then
                    removePackageFromProject project groupName packageName

    remove removeFromProjects dependenciesFileName groupName packageName force installAfter
