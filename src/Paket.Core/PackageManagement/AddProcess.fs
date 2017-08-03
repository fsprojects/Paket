/// Contains methods for addition of new packages
module Paket.AddProcess

open Paket
open System
open System.IO
open Pri.LongPath
open Paket.Domain
open Paket.Logging
open InstallProcess

let private notInstalled (project : ProjectFile) groupName package = project.HasPackageInstalled(groupName,package) |> not

let private addToProject (project : ProjectFile) groupName package =
    project.FindOrCreateReferencesFile()
        .AddNuGetReference(groupName,package)
        .Save()

let private add installToProjects addToProjectsF dependenciesFileName groupName package version options installAfter =
    let existingDependenciesFile = DependenciesFile.ReadFromFile(dependenciesFileName)
    if (not installToProjects) && existingDependenciesFile.HasPackage(groupName,package) && String.IsNullOrWhiteSpace version then
        traceWarnfn "%s contains package %O in group %O already." dependenciesFileName package groupName
    else
        let dependenciesFile =
            existingDependenciesFile
                .Add(groupName,package,version)

        let updateMode = PackageResolver.UpdateMode.Install
        let alternativeProjectRoot = None
        let lockFile,hasChanged,updatedGroups = UpdateProcess.SelectiveUpdate(dependenciesFile, alternativeProjectRoot, updateMode, options.SemVerUpdateMode, options.Force)
        let projects = seq { for p in ProjectFile.FindAllProjects(Path.GetDirectoryName lockFile.FileName) -> p } // lazy sequence in case no project install required

        dependenciesFile.Save()

        addToProjectsF projects groupName package

        if installAfter then
            let forceTouch = hasChanged && options.TouchAffectedRefs
            InstallProcess.Install(options, forceTouch, dependenciesFile, lockFile, updatedGroups)
            GarbageCollection.CleanUp(Path.GetDirectoryName dependenciesFileName, dependenciesFile, lockFile)

// Add a package with the option to add it to a specified project.
let AddToProject(dependenciesFileName, groupName, package, version, options : InstallerOptions, projectName, installAfter) =
    let groupName = 
        match groupName with
        | None -> Constants.MainDependencyGroup
        | Some name -> GroupName name

    let addToSpecifiedProject (projects : ProjectFile seq) groupName packageName =
        
        match ProjectFile.TryFindProject(projects,projectName) with
        | Some p ->
            if packageName |> notInstalled p groupName then
                addToProject p groupName packageName
            else traceWarnfn "Package %O already installed in project %s in group %O" packageName p.FileName groupName
        | None ->
            traceErrorfn "Could not install package in specified project %s. Project not found" projectName

    add true addToSpecifiedProject dependenciesFileName groupName package version options installAfter

// Add a package with the option to interactively add it to multiple projects.
let Add(dependenciesFileName, groupName, package, version, options : InstallerOptions, interactive, installAfter) =
    let groupName = 
        match groupName with
        | None -> Constants.MainDependencyGroup
        | Some name -> GroupName name

    let addToProjects (projects : ProjectFile seq) groupName package =
        if interactive then
            for project in projects do
                if package |> notInstalled project groupName && Utils.askYesNo(sprintf "  Install to %s into group %O?" project.FileName groupName) then
                    addToProject project groupName package

    add interactive addToProjects dependenciesFileName groupName package version options installAfter
