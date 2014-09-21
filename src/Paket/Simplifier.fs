module Paket.Simplifier

open System.IO
open Paket.LockFile

let Simplify(lockFile : LockFile, depFile : DependenciesFile, refFiles : list<FileInfo * string[]>) = 
    
    let depsLookup =
        lockFile.ResolvedPackages
        |> List.map (fun package -> package.Name.ToLower(), 
                                    package.DirectDependencies 
                                            |> List.map (fun (name,_) -> name.ToLower()) 
                                            |> Set.ofList)
        |> Map.ofList

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

    DependenciesFile(false, simplifiedDeps, depFile.RemoteFiles), refFiles'