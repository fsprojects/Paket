/// Contains methods for addition of new packages
module Paket.AddProcess

open Paket
open System
open System.IO
open Paket.Domain
open Paket.Logging
 
let private matchGroupName groupName =
    match groupName with
        | None -> Constants.MainDependencyGroup
        | Some name -> GroupName name

let private notInstalled (project : ProjectFile) groupName package = project.HasPackageInstalled(groupName,package) |> not

let private addToProject (project : ProjectFile) groupName package =
    project.FindOrCreateReferencesFile()
        .AddNuGetReference(groupName,package)
        .Save()

let private add installToProjects addToProjectsF dependenciesFileName groupName package version options installAfter runResolver packageKind =
    let existingDependenciesFile = DependenciesFile.ReadFromFile(dependenciesFileName)
    if (not installToProjects) && existingDependenciesFile.HasPackage(groupName,package) && String.IsNullOrWhiteSpace version then
        traceWarnfn "%s contains package %O in group %O already." dependenciesFileName package groupName
    else
        let lockFileName = DependenciesFile.FindLockfile dependenciesFileName
        let mutable lockFile = None
        let lockFileHasPackage =
            if not lockFileName.Exists then false else
            let lf = LockFile.LoadFrom lockFileName.FullName
            lockFile <- Some lf
            let lockFileGroup = lf.GetGroup(groupName)
            let vr = DependenciesFileParser.parseVersionString version

            match Map.tryFind package lockFileGroup.Resolution with
            | Some p when vr.VersionRequirement.IsInRange(p.Version) -> true
            | _ -> false
                
        let dependenciesFile =
            if lockFileHasPackage then
                existingDependenciesFile
            else
                existingDependenciesFile
                    .Add(groupName,package,version, Requirements.InstallSettings.Default, packageKind)

        let projects = 
            seq { for p in ProjectFile.FindAllProjects(Path.GetDirectoryName dependenciesFile.FileName) -> p } // lazy sequence in case no project install required
            |> Seq.cache

        if not runResolver then 
            dependenciesFile.Save()
            
            addToProjectsF projects groupName package
        elif lockFileHasPackage then
            dependenciesFile.Save()
            
            addToProjectsF projects groupName package

            if installAfter then
                match lockFile with
                | None -> ()
                | Some lockFile ->
                    let newVersion =
                        match lockFile.Groups |> Map.tryFind groupName with
                        | None -> 
                            None
                        | Some group ->
                            match group.Resolution |> Map.tryFind package with
                            | Some package -> Some package.Version
                            | None -> None

                    let touchedGroups = Map.empty.Add(groupName,"")
                    InstallProcess.Install(options, false, dependenciesFile, lockFile, touchedGroups, Some [groupName, package, None, newVersion])
                    GarbageCollection.CleanUp(dependenciesFile, lockFile)
        else
            let updateMode = PackageResolver.UpdateMode.InstallGroup groupName
            let alternativeProjectRoot = None
            let lockFile,hasChanged,updatedGroups,updatedPackages = UpdateProcess.SelectiveUpdate(dependenciesFile, alternativeProjectRoot, updateMode, options.SemVerUpdateMode, options.Force)
            
            dependenciesFile.Save()
            addToProjectsF projects groupName package

            if installAfter then
                let forceTouch = hasChanged && options.TouchAffectedRefs
                InstallProcess.Install(options, forceTouch, dependenciesFile, lockFile, updatedGroups, Some updatedPackages)
                GarbageCollection.CleanUp(dependenciesFile, lockFile)

        lockFileName.Refresh()
        if lockFileName.Exists then
            let resolutionForGroup = LockFile.LoadFrom(lockFileName.FullName).GetGroup(groupName).Resolution
            match resolutionForGroup.TryFind package with
            | Some resolved ->
                tracefn "Resolved package '%s' to version %s" package.Name resolved.Version.AsString
            | None ->
                traceWarnfn "Could not find package %s in group %s" package.Name groupName.Name
    installAfter

// Add a package with the option to add it to a specified project.
let AddToProject(dependenciesFileName, groupName, package, version, options : InstallerOptions, projectName, installAfter, runResolver, packageKind) =
    let groupName = matchGroupName(groupName)

    let addToSpecifiedProject (projects : ProjectFile seq) groupName packageName =
        
        match ProjectFile.TryFindProject(projects,projectName) with
        | Some p ->
            if packageName |> notInstalled p groupName then
                addToProject p groupName packageName
            else traceWarnfn "Package %O already installed in project %s in group %O" packageName p.FileName groupName
        | None ->
            traceErrorfn "Could not install package in specified project %s. Project not found" projectName

    add true addToSpecifiedProject dependenciesFileName groupName package version options installAfter runResolver packageKind

// Add a package with the option to interactively add it to multiple projects.
let Add(dependenciesFileName, groupName, package, version, options : InstallerOptions, interactive, installAfter, runResolver, packageKind) =
    let groupName = matchGroupName groupName

    let addToProjects (projects : ProjectFile seq) groupName package =
        if interactive then
            for project in projects do
                if package |> notInstalled project groupName && Utils.askYesNo(sprintf "  Install to %s into group %O?" project.FileName groupName) then
                    addToProject project groupName package

    add interactive addToProjects dependenciesFileName groupName package version options installAfter runResolver packageKind
    
let AddGithub(dependenciesFileName, groupName, repository, file, version, options) =
    let group = matchGroupName groupName
    
    let existingDependenciesFile = DependenciesFile.ReadFromFile(dependenciesFileName)
    
    let dependenciesFile = 
        existingDependenciesFile.AddGithub(group, repository, file, version)

    dependenciesFile.Save()
    
    let updateMode = PackageResolver.UpdateMode.InstallGroup group
    let alternativeProjectRoot = None
    let lockFile,_,_,_ = UpdateProcess.SelectiveUpdate(dependenciesFile, alternativeProjectRoot, updateMode, options.SemVerUpdateMode, options.Force)
    
    InstallProcess.Install(options, false, dependenciesFile, lockFile, Map.empty, None)
    GarbageCollection.CleanUp(dependenciesFile, lockFile)

let AddGit(dependenciesFileName, groupName, repository, version, options) =
    let group = matchGroupName(groupName)
    
    let existingDependenciesFile = DependenciesFile.ReadFromFile(dependenciesFileName)
    
    let dependenciesFile = 
        existingDependenciesFile.AddGit(group, repository, version)

    dependenciesFile.Save()
    
    let updateMode = PackageResolver.UpdateMode.InstallGroup group
    let alternativeProjectRoot = None
    let lockFile,_,_,_ = UpdateProcess.SelectiveUpdate(dependenciesFile, alternativeProjectRoot, updateMode, options.SemVerUpdateMode, options.Force)
    
    InstallProcess.Install(options, false, dependenciesFile, lockFile, Map.empty, None)
    GarbageCollection.CleanUp(dependenciesFile, lockFile)