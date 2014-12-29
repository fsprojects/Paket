module Paket.DependencyChangeDetection

open Paket.Domain

let findChangesInDependenciesFile(dependenciesFile:DependenciesFile,lockFile:LockFile) =   
    let directMap =
        dependenciesFile.DirectDependencies
        |> Seq.map (fun d -> NormalizedPackageName d.Key,d.Value)
        |> Map.ofSeq

    let direct =
        dependenciesFile.DirectDependencies
        |> Seq.map (fun d -> NormalizedPackageName d.Key)
        |> Set.ofSeq

    let topLevel = lockFile.GetTopLevelDependencies()

    let added =
        direct
        |> Set.filter (lockFile.ResolvedPackages.ContainsKey >> not)
        
    let removed =
        topLevel
        |> Seq.map (fun d -> d.Key)
        |> Seq.filter (direct.Contains >> not)
        |> Seq.map lockFile.GetAllNormalizedDependenciesOf
        |> Seq.concat
        |> Set.ofSeq

    let modified =
        [for t in topLevel do 
            let name = t.Key
            match directMap.TryFind name with
            | Some p ->
                if p.IsInRange (t.Value.Version) |> not then
                    yield name
            | _ -> ()]
        |> List.map lockFile.GetAllNormalizedDependenciesOf
        |> Seq.concat
        |> Set.ofSeq           

    added
    |> Set.union removed
    |> Set.union modified

let FixUnchangedDependencies (dependenciesFile:DependenciesFile) (oldLockFile:LockFile) =
    let changedDependencies = findChangesInDependenciesFile(dependenciesFile,oldLockFile)
            
    oldLockFile.ResolvedPackages
    |> Seq.map (fun kv -> kv.Value)
    |> Seq.filter (fun p -> not <| changedDependencies.Contains(NormalizedPackageName p.Name))
    |> Seq.fold 
            (fun (dependenciesFile : DependenciesFile) resolvedPackage ->                 
                    dependenciesFile.AddFixedPackage(resolvedPackage.Name, "= " + resolvedPackage.Version.ToString()))
            dependenciesFile