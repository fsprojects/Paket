module Paket.Simplifier

open System.IO
open Logging
open System
open Paket.Domain
open Paket.PackageResolver

type SimplifyResult = {
    DependenciesFileSimplifyResult : DependenciesFile * DependenciesFile
    ReferencesFilesSimplifyResult : (ReferencesFile * ReferencesFile) list
}

type SimplifyMessage = 
    | DependenciesFileMissing
    | DependenciesFileParseError
    | StrictModeDetected
    | LockFileMissing
    | LockFileParseError
    | DependencyNotLocked of PackageName
    | ReferencesFileParseError of string
    | ReferenceNotLocked of ReferencesFile * PackageName

let getDependenciesFile path =
    if File.Exists path then 
        try Rop.succeed (DependenciesFile.ReadFromFile path)
        with _ -> Rop.fail DependenciesFileParseError
    else Rop.fail DependenciesFileMissing

let ensureNoStrictMode (depFile : DependenciesFile) =
    if not depFile.Options.Strict then Rop.succeed depFile
    else Rop.fail StrictModeDetected

let getRefFiles (depFile : DependenciesFile) =
    ProjectFile.FindAllProjects(Path.GetDirectoryName depFile.FileName) 
    |> Array.choose (fun p -> ProjectFile.FindReferencesFile <| FileInfo(p.FileName))
    |> Array.map (fun file -> try Rop.succeed <| ReferencesFile.FromFile(file)
                              with _ -> Rop.fail <| ReferencesFileParseError file)
    |> Rop.collect
    |> Rop.bind (fun refFiles -> Rop.succeed (depFile,refFiles))

let getLockFile (depFile : DependenciesFile, refFiles : ReferencesFile list) = 
    let path = depFile.FindLockfile()
    if File.Exists(path.FullName) then 
        try Rop.succeed (depFile, refFiles, LockFile.LoadFrom(path.FullName))
        with _ -> Rop.fail LockFileParseError
    else Rop.fail LockFileMissing



let getDependencyLookup(depFile : DependenciesFile,  refFiles : ReferencesFile list, lockFile : LockFile) =
    let lookupDeps packageName =
        try
            lockFile.GetAllDependenciesOf packageName 
            |> Set.ofSeq
            |> Set.remove packageName
            |> Some
        with _ -> 
            None
    
    let deps =  
        depFile.Packages
        |> List.map (fun p -> 
            match lookupDeps(p.Name) with
            | Some deps -> (NormalizedPackageName p.Name, deps) |> Rop.succeed
            | None -> DependencyNotLocked(p.Name) |> Rop.fail)

    let refs =
        refFiles 
        |> List.collect (fun r -> r.NugetPackages |> List.map (fun p -> r,p))
        |> List.map (fun (r,p) ->
            match lookupDeps(p) with
            | Some deps -> (NormalizedPackageName p, deps) |> Rop.succeed
            | None -> ReferenceNotLocked(r, p) |> Rop.fail)
    
    deps @ refs
    |> Rop.collect
    |> Rop.lift (fun lookup -> depFile, refFiles, Map.ofList lookup)

let private interactiveConfirm fileName (PackageName package) = 
        Utils.askYesNo(sprintf "Do you want to remove indirect dependency %s from file %s ?" package fileName)

let analyze(depFile : DependenciesFile, refFiles : ReferencesFile list, lookup : Map<_,_>, interactive) = 
    let getSimplifiedDeps (depNameFun : 'a -> PackageName) fileName declaredDeps =
        let indirectDeps = 
            declaredDeps 
            |> List.map depNameFun 
            |> List.fold (fun set directDep -> Set.union set (lookup.[NormalizedPackageName directDep])) Set.empty
        let depsToRemove =
            if interactive then indirectDeps |> Set.filter (interactiveConfirm fileName) else indirectDeps
            |> Set.map NormalizedPackageName
        
        declaredDeps 
        |> List.filter (fun dep -> 
            depsToRemove 
            |> Set.contains (NormalizedPackageName (depNameFun dep)) 
            |> not)

    let simplifiedPackages = 
        depFile.Packages 
        |> getSimplifiedDeps (fun p -> p.Name) depFile.FileName 
        |> Seq.toList
    
    let simplifiedReferencesFiles = 
        refFiles |> 
        List.map (fun refFile -> 
            {refFile with NugetPackages = refFile.NugetPackages |> getSimplifiedDeps id refFile.FileName})
    
    { DependenciesFileSimplifyResult = 
        depFile,  
        DependenciesFile(
            depFile.FileName, 
            depFile.Options, 
            depFile.Sources, 
            simplifiedPackages, 
            depFile.RemoteFiles)
      ReferencesFilesSimplifyResult = List.zip refFiles simplifiedReferencesFiles }
             
let simplify (dependenciesFileName,interactive) = 
    Rop.succeed dependenciesFileName
    |> Rop.bind getDependenciesFile
    |> Rop.bind ensureNoStrictMode
    |> Rop.bind getRefFiles
    |> Rop.bind getLockFile
    |> Rop.bind getDependencyLookup
    |> Rop.lift (fun (d,r,l) -> analyze(d,r,l,interactive))

open Paket.Environment
open Rop

let getFlatLookup (lockFile : LockFile) = 
    lockFile.ResolvedPackages
    |> Map.map (fun name package -> 
                    lockFile.GetAllDependenciesOf package.Name
                    |> Set.ofSeq
                    |> Set.remove package.Name)

let findFlatDependencies (packageName, flatLookup, failure) = 
    flatLookup 
    |> Map.tryFind (NormalizedPackageName packageName)
    |> failIfNone failure

let ensureNotInStrictMode environment =
    if not environment.DependenciesFile.Options.Strict then succeed environment
    else fail StrictModeDetected2

let leavePackage(packageName, indirectPackages, fileName, interactive) =
    if indirectPackages |> Seq.exists (fun p -> NormalizedPackageName p = NormalizedPackageName packageName) then
        if interactive then
            interactiveConfirm fileName packageName
        else 
            false
    else
        true

let simplifyDependenciesFile (dependenciesFile : DependenciesFile) flatLookup interactive =
    let create (d : DependenciesFile) indirect =
        let newPackages = 
            dependenciesFile.Packages
            |> List.filter (fun package -> leavePackage(package.Name, indirect, dependenciesFile.FileName, interactive))
             
        DependenciesFile(d.FileName, d.Options, d.Sources, newPackages, d.RemoteFiles)

    let indirect = 
        dependenciesFile.Packages 
        |> List.map (fun p -> findFlatDependencies(p.Name, flatLookup, DependencyNotFoundInLockFile(p.Name)))
        |> Rop.collect
        |> lift Seq.concat
        
    create dependenciesFile
    <!> indirect

let simplifyProject project refFile flatLookup interactive =
    let create project refFile indirect =
        project,
        {refFile with NugetPackages = 
                        refFile.NugetPackages 
                        |> List.filter (fun p -> leavePackage(p, indirect, refFile.FileName, interactive))}

    let indirect = 
                refFile.NugetPackages
                |> List.map (fun p -> findFlatDependencies(p, flatLookup, ReferenceNotFoundInLockFile(refFile, p)))
                |> Rop.collect
                |> lift Seq.concat
                 
    create project refFile
    <!> indirect

let replace environment dependenciesFile projects =
        { environment with DependenciesFile = dependenciesFile
                           Projects = projects }

let s interactive environment =
    let flatLookup = getFlatLookup environment.LockFile
    let dependenciesFile = simplifyDependenciesFile environment.DependenciesFile flatLookup interactive
    let simplifiedProjects =
        environment.Projects 
        |> List.map (fun (project,refFile) -> simplifyProject project refFile flatLookup interactive)
        |> Rop.collect

    replace environment
    <!> dependenciesFile
    <*> simplifiedProjects