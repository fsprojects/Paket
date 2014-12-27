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
        with _ -> Rop.failure DependenciesFileParseError
    else Rop.failure DependenciesFileMissing

let ensureNoStrictMode (depFile : DependenciesFile) =
    if not depFile.Options.Strict then Rop.succeed depFile
    else Rop.failure StrictModeDetected

let getRefFiles (depFile : DependenciesFile) =
    ProjectFile.FindAllProjects(Path.GetDirectoryName depFile.FileName) 
    |> Array.choose (fun p -> ProjectFile.FindReferencesFile <| FileInfo(p.FileName))
    |> Array.map (fun file -> try Rop.succeed <| ReferencesFile.FromFile(file)
                              with _ -> Rop.failure <| ReferencesFileParseError file)
    |> Rop.collect
    |> Rop.bind (fun refFiles -> Rop.succeed (depFile,refFiles))

let getLockFile (depFile : DependenciesFile, refFiles : ReferencesFile list) = 
    let path = depFile.FindLockfile()
    if File.Exists(path.FullName) then 
        try Rop.succeed (depFile, refFiles, LockFile.LoadFrom(path.FullName))
        with _ -> Rop.failure LockFileParseError
    else Rop.failure LockFileMissing



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
            | None -> DependencyNotLocked(p.Name) |> Rop.failure)

    let refs =
        refFiles 
        |> List.collect (fun r -> r.NugetPackages |> List.map (fun p -> r,p))
        |> List.map (fun (r,p) ->
            match lookupDeps(p) with
            | Some deps -> (NormalizedPackageName p, deps) |> Rop.succeed
            | None -> ReferenceNotLocked(r, p) |> Rop.failure)
    
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