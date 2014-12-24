module Paket.Simplifier

open System.IO
open Logging
open System
open Paket.Domain
open Paket.PackageResolver
open Rop

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
        try succeed (DependenciesFile.ReadFromFile path)
        with _ -> failure DependenciesFileParseError
    else failure DependenciesFileMissing

let ensureNoStrictMode (depFile : DependenciesFile) =
    if not depFile.Options.Strict then succeed depFile
    else failure StrictModeDetected

let getRefFiles (depFile : DependenciesFile) =
    ProjectFile.FindAllProjects(Path.GetDirectoryName depFile.FileName) 
    |> Array.choose (fun p -> ProjectFile.FindReferencesFile <| FileInfo(p.FileName))
    |> Array.map (fun file -> try succeed <| ReferencesFile.FromFile(file)
                              with _ -> failure <| ReferencesFileParseError file)
    |> Rop.collect
    |> bind (fun refFiles -> succeed (depFile,refFiles))

let getLockFile (depFile : DependenciesFile, refFiles : ReferencesFile list) = 
    let path = depFile.FindLockfile()
    if File.Exists(path.FullName) then 
        try succeed (depFile, refFiles, LockFile.LoadFrom(path.FullName))
        with _ -> failure LockFileParseError
    else failure LockFileMissing



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
            | Some deps -> (NormalizedPackageName p.Name, deps) |> succeed
            | None -> DependencyNotLocked(p.Name) |> failure)

    let refs =
        refFiles 
        |> List.collect (fun r -> r.NugetPackages |> List.map (fun p -> r,p))
        |> List.map (fun (r,p) ->
            match lookupDeps(p) with
            | Some deps -> (NormalizedPackageName p, deps) |> succeed
            | None -> ReferenceNotLocked(r, p) |> failure)
    
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
    succeed dependenciesFileName
    |> bind getDependenciesFile
    |> bind ensureNoStrictMode
    |> bind getRefFiles
    |> bind getLockFile
    |> bind getDependencyLookup
    |> lift (fun (d,r,l) -> analyze(d,r,l,interactive))