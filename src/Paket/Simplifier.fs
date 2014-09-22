module Paket.Simplifier

open System.IO
open Logging
open System

let Analyze(allPackages : list<ResolvedPackage>, depFile : DependenciesFile, refFiles : list<FileInfo * string[]>) = 
    
    let depsLookup =
        allPackages
        |> Seq.map (fun package -> package.Name.ToLower(), 
                                               package.DirectDependencies 
                                               |> List.map (fun (name,_) -> name.ToLower()) 
                                               |> Set.ofList)
        |> Map.ofSeq

    let runWithLower f = (fun (s: string) -> f(s.ToLower())) 

    let rec getAllDeps =
        memoize (fun (package : string) -> 
                     Set.union depsLookup.[package]
                               (Set.unionMany (depsLookup.[package] |> Set.map getAllDeps))) |> runWithLower

    let getSimplifiedDeps depNameFun allDeps =
        let indirectDeps = 
            allDeps 
            |> Seq.map depNameFun 
            |> Seq.fold (fun set directDep -> Set.union set (getAllDeps directDep)) Set.empty
        allDeps |> Seq.filter (fun dep -> not <| Set.contains ((depNameFun dep).ToLower()) indirectDeps)

    let simplifiedDeps = depFile.Packages |> getSimplifiedDeps (fun p -> p.Name) |> Seq.toList
    let refFiles' = refFiles |> List.map (fun (fi, refs) -> fi, refs |> getSimplifiedDeps id |> Seq.toArray)

    DependenciesFile(depFile.FileName, false, simplifiedDeps, depFile.RemoteFiles), refFiles'

let private formatDiff (before : string []) (after : string []) =
    "   Before:" + Environment.NewLine +
    "       " + String.Join("       " + Environment.NewLine, before) +
    Environment.NewLine + Environment.NewLine +
    "   After:" + Environment.NewLine +
    "       " + String.Join("       " + Environment.NewLine, after)

let Simplify () = 
    if not <| File.Exists("paket.dependencies") then
        failwith "paket.dependencies file not found."
    let depFile = DependenciesFile.ReadFromFile("paket.dependencies")
    let lockFilePath = depFile.FindLockfile()
    if not <| File.Exists(lockFilePath.FullName) then 
        failwith "lock file not found. Create lock file by running paket install"

    let lockFile = LockFile.LoadFrom lockFilePath.FullName
    let packages = 
        lockFile.ResolvedPackages 
                 |> Seq.map (fun r -> 
                             match r.Value with 
                             | Resolved package -> package
                             | Conflict _ -> failwith "lock file has conflicts. Resolve them before running simplify")
                 |> List.ofSeq

    let refFiles = 
        FindAllFiles(".", "paket.references") 
        |> Seq.map(fun f -> f, File.ReadAllLines f.FullName) 
        |> Seq.toList

    let refFilesLookup = refFiles |> List.map (fun (fi,content) -> fi.FullName, content) |> Map.ofList

    let simplifiedDepsFile, simplifiedRefFiles = Analyze(packages, depFile, refFiles)

    for file,lines in simplifiedRefFiles do
        File.WriteAllLines(file.FullName, lines)
        tracefn "Simplified %s" file.FullName
        traceVerbose (formatDiff refFilesLookup.[file.FullName] lines)