module Paket.Simplifier

open System
open System.IO

open Paket.Domain
open Paket.Logging
open Paket.PackageResolver
open Chessie.ErrorHandling

let private findTransitive (packages, flatLookup, failureF) = 
    packages
    |> List.map (fun packageName -> 
        flatLookup 
        |> Map.tryFind (NormalizedPackageName packageName)
        |> failIfNone (failureF packageName))
    |> collect
    |> lift Seq.concat

let private removePackage(packageName, transitivePackages, fileName, interactive) =
    if transitivePackages |> Seq.exists (fun p -> NormalizedPackageName p = NormalizedPackageName packageName) then
        if interactive then
            let message = sprintf "Do you want to remove transitive dependency %s from file %s?" packageName.Id fileName 
            Utils.askYesNo(message)
        else 
            true
    else
        false

let simplifyDependenciesFile (dependenciesFile : DependenciesFile, flatLookup, interactive) = trial {
    let packages = dependenciesFile.Packages |> List.map (fun p -> p.Name)
    let! transitive = findTransitive(packages, flatLookup, DependencyNotFoundInLockFile)

    let newPackages = 
        dependenciesFile.Packages
        |> List.filter (fun package -> not <| removePackage(package.Name, transitive, dependenciesFile.FileName, interactive))
    let d = dependenciesFile
    return DependenciesFile(d.FileName, d.Options, d.Sources, newPackages, d.RemoteFiles, d.Comments)
}

let simplifyReferencesFile (refFile, flatLookup, interactive) = trial {
    let! transitive = findTransitive(refFile.NugetPackages |> List.map (fun p -> p.Name), 
                            flatLookup, 
                            (fun p -> ReferenceNotFoundInLockFile(refFile.FileName,p)))

    let newPackages = 
        refFile.NugetPackages 
        |> List.filter (fun p -> not <| removePackage(p.Name, transitive, refFile.FileName, interactive))

    return { refFile with NugetPackages = newPackages }
}

let beforeAndAfter environment dependenciesFile projects =
    environment,
    { environment with 
        DependenciesFile = dependenciesFile
        Projects = projects }

let simplify interactive environment = trial {
    let! lockFile = environment |> PaketEnv.ensureLockFileExists

    let flatLookup = lockFile.GetDependencyLookupTable()
    let! dependenciesFile = simplifyDependenciesFile(environment.DependenciesFile, flatLookup, interactive)
    let projectFiles, referencesFiles = List.unzip environment.Projects

    let! referencesFiles' =
        referencesFiles
        |> List.map (fun refFile -> simplifyReferencesFile(refFile, flatLookup, interactive))
        |> collect

    let projects = List.zip projectFiles referencesFiles'

    return beforeAndAfter environment dependenciesFile projects
}

let updateEnvironment (before,after) =
    if before.DependenciesFile.ToString() = after.DependenciesFile.ToString() then
        tracefn "%s is already simplified" before.DependenciesFile.FileName
    else
        tracefn "Simplifying %s" after.DependenciesFile.FileName
        after.DependenciesFile.Save()

    for (_,refFileBefore),(_,refFileAfter) in List.zip before.Projects after.Projects do
        if refFileBefore = refFileAfter then
            tracefn "%s is already simplified" refFileBefore.FileName
        else
            tracefn "Simplifying %s" refFileAfter.FileName
            refFileAfter.Save()