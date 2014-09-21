module Paket.Simplifier

open System.IO
open Paket.LockFile
open Logging

let Analyze(lockFile : LockFile, depFile : DependenciesFile, refFiles : list<FileInfo * string[]>) = 
    
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

let Simplify () = 
    if not <| File.Exists("paket.dependencies") then
        failwith "paket.dependencies file not found."
    let lockFile = LockFile.findLockfile("paket.dependencies")
    if not <| File.Exists(lockFile.FullName) then 
        failwith "lock file not found. Create lock file by running paket install"

    let refFiles = 
        FindAllFiles(".", "paket.references") 
        |> Seq.map(fun f -> f, File.ReadAllLines f.FullName) 
        |> Seq.toList

    let simplifiedDepsFile, simplifiedRefFiles =
        Analyze(LockFile.Parse (File.ReadAllLines(lockFile.FullName)), 
                DependenciesFile.ReadFromFile("paket.dependencies"), 
                refFiles)

    for file,lines in simplifiedRefFiles do
        File.WriteAllLines(file.FullName, lines)
        tracefn "Simplified %s" file.FullName