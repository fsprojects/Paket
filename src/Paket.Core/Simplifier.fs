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
    else fail StrictModeDetected2

let simplifyR interactive environment =
    let flatLookup = getFlatLookup environment.LockFile
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