module Paket.Simplifier

open System
open System.IO
open Pri.LongPath

open Paket.Domain
open Paket.Logging
open Paket.PackageResolver
open Chessie.ErrorHandling

let private findTransitive (groupName,packages, flatLookup, failureF) = 
    packages
    |> List.map (fun packageName -> 
        flatLookup 
        |> Map.tryFind (groupName, packageName)
        |> failIfNone (failureF packageName))
    |> collect
    |> lift Seq.concat

let private removePackage(packageName, transitivePackages, fileName, interactive) =
    if transitivePackages |> Seq.exists (fun p -> p = packageName) then
        if interactive then
            let message = sprintf "Do you want to remove transitive dependency %O from file %s?" packageName fileName 
            Utils.askYesNo(message)
        else 
            true
    else
        false

let simplifyDependenciesFile (dependenciesFile : DependenciesFile, groupName, flatLookup, interactive) = trial {
    let packages = dependenciesFile.Groups.[groupName].Packages |> List.map (fun p -> p.Name)
    let! transitive = findTransitive(groupName, packages, flatLookup, DependencyNotFoundInLockFile)

    return
        dependenciesFile.Groups.[groupName].Packages
        |> List.fold  (fun (d:DependenciesFile) package ->
                if removePackage(package.Name, transitive, dependenciesFile.FileName, interactive) then
                    d.Remove(groupName,package.Name)
                else d) dependenciesFile
}

let simplifyReferencesFile (refFile:ReferencesFile, groupName, flatLookup, interactive) = trial {
    match refFile.Groups |> Map.tryFind groupName with
    | None -> return refFile
    | Some g -> 
        let! transitive = findTransitive(groupName, g.NugetPackages |> List.map (fun p -> p.Name), 
                                flatLookup, 
                                (fun p -> ReferenceNotFoundInLockFile(refFile.FileName, groupName.ToString(),p)))

        let newPackages = 
            g.NugetPackages 
            |> List.filter (fun p -> not <| removePackage(p.Name, transitive, refFile.FileName, interactive))

        let newGroups = refFile.Groups |> Map.add groupName {g with NugetPackages = newPackages }

        return { refFile with Groups = newGroups }
}

let beforeAndAfter environment dependenciesFile projects =
    environment,
    { environment with 
        DependenciesFile = dependenciesFile
        Projects = projects }

let simplify interactive environment = trial {
    let! lockFile = environment |> PaketEnv.ensureLockFileExists

    let flatLookup = lockFile.GetDependencyLookupTable()
    let dependenciesFileRef = ref (environment.DependenciesFile.SimplifyFrameworkRestrictions())
    let projectsRef = ref None

    for kv in lockFile.Groups do
        let groupName = kv.Key
        let! dependenciesFile = simplifyDependenciesFile(!dependenciesFileRef, groupName, flatLookup, interactive)
        dependenciesFileRef := dependenciesFile
        let projectFiles, referencesFiles = List.unzip environment.Projects

        let! referencesFiles' =
            referencesFiles
            |> List.map (fun refFile -> simplifyReferencesFile(refFile, groupName, flatLookup, interactive))
            |> collect

        let projects = List.zip projectFiles referencesFiles'
        projectsRef := Some projects

    return beforeAndAfter environment (!dependenciesFileRef) (!projectsRef).Value
}

let updateEnvironment (before,after) =
    if before.DependenciesFile.ToString() = after.DependenciesFile.ToString() then
        if verbose then
            verbosefn "%s is already simplified" before.DependenciesFile.FileName
    else
        tracefn "Simplifying %s" after.DependenciesFile.FileName
        after.DependenciesFile.Save()

    for (_,refFileBefore),(_,refFileAfter) in List.zip before.Projects after.Projects do
        if refFileBefore = refFileAfter then
            if verbose then
                verbosefn "%s is already simplified" refFileBefore.FileName
        else
            tracefn "Simplifying %s" refFileAfter.FileName
            refFileAfter.Save()