module Paket.Simplifier

open System.IO
open Logging
open System

let private formatDiff (before : string) (after : string) =
    let nl = Environment.NewLine
    nl + "Before:" + nl + nl + before + nl + nl + nl + "After:" + nl + nl + after + nl + nl

let private simplify file before after =
    if before <> after then
        File.WriteAllText(file, after)
        tracefn "Simplified %s" file
        traceVerbose (formatDiff before after)
    else
        tracefn "%s is already simplified" file

let Analyze(allPackages : list<ResolvedPackage>, depFile : DependenciesFile, refFiles : list<ReferencesFile>) = 
    
    let depsLookup =
        allPackages
        |> Seq.map (fun package -> package.Name.ToLower(), 
                                               package.Dependencies 
                                               |> List.map (fun (name,_) -> name.ToLower()) 
                                               |> Set.ofList)
        |> Map.ofSeq

    let rec getAllDeps (package : string) =
        Set.union depsLookup.[package.ToLower()]
                  (Set.unionMany (depsLookup.[package.ToLower()] |> Set.map getAllDeps))

    let flattenedLookup = depsLookup |> Map.map (fun key _ -> getAllDeps key)

    let getSimplifiedDeps (depNameFun : 'a -> string) allDeps =
        let indirectDeps = 
            allDeps 
            |> List.map depNameFun 
            |> List.fold (fun set directDep -> Set.union set (flattenedLookup.[ directDep.ToLower() ])) Set.empty
        allDeps |> List.filter (fun dep -> not <| Set.contains ((depNameFun dep).ToLower()) indirectDeps)

    let simplifiedDeps = depFile.Packages |> getSimplifiedDeps (fun p -> p.Name) |> Seq.toList
    let refFiles' = if depFile.Strict 
                    then refFiles 
                    else refFiles |> List.map (fun refFile -> {refFile with NugetPackages = 
                                                                            refFile.NugetPackages |> getSimplifiedDeps id})

    DependenciesFile(depFile.FileName, depFile.Strict, simplifiedDeps, depFile.RemoteFiles), refFiles'

let Simplify (dependenciesFileName) = 
    if not <| File.Exists(dependenciesFileName) then
        failwithf "%s file not found." dependenciesFileName
    let depFile = DependenciesFile.ReadFromFile(dependenciesFileName)
    let lockFilePath = depFile.FindLockfile()
    if not <| File.Exists(lockFilePath.FullName) then 
        failwith "lock file not found. Create lock file by running paket install."

    let lockFile = LockFile.LoadFrom lockFilePath.FullName
    let packages = lockFile.ResolvedPackages |> Seq.map (fun kv -> kv.Value) |> List.ofSeq
    let refFiles = 
        ProjectFile.FindAllProjects(".") 
        |> List.choose ProjectFile.FindReferencesFile 
        |> List.map ReferencesFile.FromFile
    let refFilesBefore = refFiles |> List.map (fun refFile -> refFile.FileName, refFile) |> Map.ofList

    let simplifiedDepFile, simplifiedRefFiles = Analyze(packages, depFile, refFiles)
    
    simplify depFile.FileName <| depFile.ToString() <| simplifiedDepFile.ToString()

    if depFile.Strict then
        traceWarn ("Strict mode detected. Will not attempt to simplify " + Constants.ReferencesFile + " files.")
    else
        for refFile in simplifiedRefFiles do
            simplify refFile.FileName <| refFilesBefore.[refFile.FileName].ToString() <| refFile.ToString()