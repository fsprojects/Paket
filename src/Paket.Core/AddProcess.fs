/// Contains methods for addition of new packages
module Paket.AddProcess

open Paket
open System.IO
open Paket.Domain
open Paket.Logging

let private notInstalled (project : ProjectFile) package = project.HasPackageInstalled(NormalizedPackageName package) |> not

let private addToProject (project : ProjectFile) package =
    ProjectFile.FindOrCreateReferencesFile(FileInfo(project.FileName))
        .AddNuGetReference(package)
        .Save()

let private add addToProjects dependenciesFileName package version force hard installAfter =
    let existingDependenciesFile = DependenciesFile.ReadFromFile(dependenciesFileName)
    let dependenciesFile =
        existingDependenciesFile
            .Add(package,version)

    dependenciesFile.Save()

    let lockFile = UpdateProcess.SelectiveUpdate(dependenciesFile,Some(NormalizedPackageName package),force)
    let projects = seq { for p in ProjectFile.FindAllProjects(Path.GetDirectoryName lockFile.FileName) -> p } // lazy sequence in case no project install required
    
    package |> addToProjects projects

    if installAfter then
        let sources = dependenciesFile.GetAllPackageSources()
        InstallProcess.Install(sources, force, hard, false, lockFile)

// add a package with the option to add it to a specified project
let AddToProject(dependenciesFileName, package, version, force, hard, projectName, installAfter) =
    
    let addToSpecifiedProject (projects : ProjectFile seq) package =    
        let project = 
            projects |> Seq.tryFind (fun p -> p.NameWithoutExtension = projectName || p.Name = projectName)

        match project with
        | Some p ->
            if package |> notInstalled p then
                package |> addToProject p
            else traceWarnfn "Package %s already installed in project %s" package.Id p.Name
        | None ->
            traceErrorfn "Could not install package in specified project %s. Project not found" projectName

    add addToSpecifiedProject dependenciesFileName package version force hard installAfter
    
// add a package with the option to interactively add it to multiple projects
let Add(dependenciesFileName, package, version, force, hard, interactive, installAfter) =
   
    let addToProjects (projects : ProjectFile seq) package = 
        if interactive then
            for project in projects do
                if package |> notInstalled project && Utils.askYesNo(sprintf "  Install to %s?" project.Name) then
                    package |> addToProject project
    
    add addToProjects dependenciesFileName package version force hard installAfter