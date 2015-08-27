/// Contains methods for addition of new packages
module Paket.AddProcess

open Paket
open System
open System.IO
open Paket.Domain
open Paket.Logging

let private notInstalled (project : ProjectFile) groupName package = project.HasPackageInstalled(groupName,NormalizedPackageName package) |> not

let private addToProject (project : ProjectFile) package =
    ProjectFile.FindOrCreateReferencesFile(FileInfo(project.FileName))
        .AddNuGetReference(package)
        .Save()

let private add installToProjects addToProjectsF dependenciesFileName package version options installAfter =
    let existingDependenciesFile = DependenciesFile.ReadFromFile(dependenciesFileName)
    let (PackageName name) = package
    if (not installToProjects) && existingDependenciesFile.HasPackage package && String.IsNullOrWhiteSpace version then
        traceWarnfn "%s contains package %s already." dependenciesFileName name
    else
        let dependenciesFile =
            existingDependenciesFile
                .Add(package,version)

        let lockFile = UpdateProcess.SelectiveUpdate(dependenciesFile, false, None, options.Force)
        let projects = seq { for p in ProjectFile.FindAllProjects(Path.GetDirectoryName lockFile.FileName) -> p } // lazy sequence in case no project install required

        dependenciesFile.Save()

        package |> addToProjectsF projects

        if installAfter then
            let sources = dependenciesFile.GetAllPackageSources()
            InstallProcess.Install(sources, options, lockFile)

// Add a package with the option to add it to a specified project.
let AddToProject(dependenciesFileName, package, version, options : InstallerOptions, projectName, installAfter) =
    let addToSpecifiedProject (projects : ProjectFile seq) package =
        match ProjectFile.TryFindProject(projects,projectName) with
        | Some p ->
            if package |> notInstalled p Constants.MainDependencyGroup then
                package |> addToProject p
            else traceWarnfn "Package %s already installed in project %s" package.Id p.Name
        | None ->
            traceErrorfn "Could not install package in specified project %s. Project not found" projectName

    add true addToSpecifiedProject dependenciesFileName package version options installAfter

// Add a package with the option to interactively add it to multiple projects.
let Add(dependenciesFileName, package, version, options : InstallerOptions, interactive, installAfter) =
    let addToProjects (projects : ProjectFile seq) package =
        if interactive then
            for project in projects do
                if package |> notInstalled project Constants.MainDependencyGroup && Utils.askYesNo(sprintf "  Install to %s?" project.Name) then
                    package |> addToProject project

    add interactive addToProjects dependenciesFileName package version options installAfter
