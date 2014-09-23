module Paket.Simplifier

open System.IO
open Logging
open System

let private formatDiff (before : string []) (after : string []) =
    "   Before:" + Environment.NewLine +
    "       " + String.Join(Environment.NewLine + "       ", before) +
    Environment.NewLine + Environment.NewLine +
    "   After:" + Environment.NewLine +
    "       " + String.Join(Environment.NewLine + "       ", after)

let private simplify file before after =
    if before <> after then
        File.WriteAllLines(file, after)
        tracefn "Simplified %s" file
        traceVerbose (formatDiff before after)
    else
        tracefn "%s is already simplified" file

let Analyze(allPackages : list<ResolvedPackage>, depFile : DependenciesFile, refFiles : list<FileInfo * string[]>) = 
    
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
            |> Seq.map depNameFun 
            |> Seq.fold (fun set directDep -> Set.union set (flattenedLookup.[ directDep.ToLower() ])) Set.empty
        allDeps |> Seq.filter (fun dep -> not <| Set.contains ((depNameFun dep).ToLower()) indirectDeps)

    let simplifiedDeps = depFile.Packages |> getSimplifiedDeps (fun p -> p.Name) |> Seq.toList
    let refFiles' = if depFile.Strict 
                    then refFiles 
                    else refFiles |> List.map (fun (fi, refs) -> fi, refs |> getSimplifiedDeps id |> Seq.toArray)

    DependenciesFile(depFile.FileName, depFile.Strict, simplifiedDeps, depFile.RemoteFiles), refFiles'

let Simplify () = 
    if not <| File.Exists(Constants.DependenciesFile) then
        failwithf "%s file not found." Constants.DependenciesFile
    let depFile = DependenciesFile.ReadFromFile(Constants.DependenciesFile)
    let lockFilePath = depFile.FindLockfile()
    if not <| File.Exists(lockFilePath.FullName) then 
        failwith "lock file not found. Create lock file by running paket install"
    let lockFile = LockFile.LoadFrom lockFilePath.FullName
    let packages = lockFile.ResolvedPackages |> Seq.map (fun kv -> kv.Value) |> List.ofSeq
    let refFiles = 
        FindAllFiles(".", Constants.ReferencesFile) 
        |> Seq.map(fun f -> f, File.ReadAllLines f.FullName) 
        |> Seq.toList
    let refFilesBefore = refFiles |> List.map (fun (fi,content) -> fi.FullName, content) |> Map.ofList
    
    let simplifiedDepFile, simplifiedRefFiles = Analyze(packages, depFile, refFiles)
    
    let removedDeps = Set.difference (Set depFile.Packages) (Set simplifiedDepFile.Packages) |> Set.map (fun p -> p.Name.ToLower())
    let before = File.ReadAllLines(Constants.DependenciesFile)
    let after = before |> Array.filter(fun line -> 
                                           if not <| line.StartsWith("nuget", StringComparison.InvariantCultureIgnoreCase) then true
                                           else 
                                                let dep = line.Split([|' '|]).[1].Trim().ToLower()
                                                removedDeps |> Set.forall (fun removedDep -> removedDep <> dep))

    simplify Constants.DependenciesFile before after

    if depFile.Strict then
        traceWarn ("Strict mode detected. Will not attempt to simplify " + Constants.ReferencesFile + " files.")
    else
        for file,after in simplifiedRefFiles do
            let before = refFilesBefore.[file.FullName]
            simplify file.FullName before after