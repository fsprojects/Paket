module Paket.Simplifier

open System
open System.IO

open Paket.Domain
open Paket.Logging
open Paket.PackageResolver
open Paket.Rop

let getFlatLookup (lockFile : LockFile) = 
    lockFile.ResolvedPackages
    |> Map.map (fun name package -> 
                    lockFile.GetAllDependenciesOf package.Name
                    |> Set.ofSeq
                    |> Set.remove package.Name)

let findIndirect (packages, flatLookup, failureF) = 
    packages
    |> List.map (fun packageName -> 
        flatLookup 
        |> Map.tryFind (NormalizedPackageName packageName)
        |> failIfNone (failureF packageName))
    |> Rop.collect
    |> lift Seq.concat

let removePackage(packageName, indirectPackages, fileName, interactive) =
    if indirectPackages |> Seq.exists (fun p -> NormalizedPackageName p = NormalizedPackageName packageName) then
        if interactive then
            let message = sprintf "Do you want to remove indirect dependency %s from file %s ?" packageName.Id fileName 
            Utils.askYesNo(message)
        else 
            true
    else
        false

let simplifyDependenciesFile (dependenciesFile : DependenciesFile, flatLookup, interactive) =
    let create (d : DependenciesFile) indirect =
        let newPackages = 
            dependenciesFile.Packages
            |> List.filter (fun package -> not <| removePackage(package.Name, indirect, dependenciesFile.FileName, interactive))
        DependenciesFile(d.FileName, d.Options, d.Sources, newPackages, d.RemoteFiles)

    let packages = dependenciesFile.Packages |> List.map (fun p -> p.Name)
    let indirect = findIndirect(packages, flatLookup, DependencyNotFoundInLockFile)
        
    create dependenciesFile
    <!> indirect

let simplifyReferencesFile (refFile, flatLookup, interactive) =
    let create refFile indirect =
        let newPackages = 
            refFile.NugetPackages 
            |> List.filter (fun p -> not <| removePackage(p, indirect, refFile.FileName, interactive))
        { refFile with NugetPackages = newPackages }

    let indirect = findIndirect(refFile.NugetPackages, flatLookup, (fun p -> ReferenceNotFoundInLockFile(refFile,p)))
                 
    create refFile
    <!> indirect

let beforeAndAfter environment dependenciesFile projects =
        environment,
        { environment with DependenciesFile = dependenciesFile
                           Projects = projects }

let ensureNotInStrictMode environment =
    if not environment.DependenciesFile.Options.Strict then succeed environment
    else fail StrictModeDetected

let simplify interactive environment =
    match environment.LockFile with
    | Some lockFile ->
        let flatLookup = getFlatLookup lockFile
        let dependenciesFile = simplifyDependenciesFile(environment.DependenciesFile, flatLookup, interactive)
        let projectFiles, referencesFiles = List.unzip environment.Projects

        let referencesFiles' =
            referencesFiles
            |> List.map (fun refFile -> simplifyReferencesFile(refFile, flatLookup, interactive))
            |> Rop.collect

        let projects = List.zip projectFiles <!> referencesFiles'

        beforeAndAfter environment
        <!> dependenciesFile
        <*> projects
    | None -> fail (LockFileNotFound environment.RootDirectory)

let updateEnvironment ((before,after), _ ) =
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