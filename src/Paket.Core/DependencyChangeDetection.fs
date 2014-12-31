module Paket.DependencyChangeDetection

open Paket.Domain

let findChangesInDependenciesFile(dependenciesFile:DependenciesFile,lockFile:LockFile) =   
    let directMap =
        dependenciesFile.DirectDependencies
        |> Seq.map (fun d -> NormalizedPackageName d.Key,d.Value)
        |> Map.ofSeq

    let added =
        dependenciesFile.DirectDependencies
        |> Seq.map (fun d -> NormalizedPackageName d.Key)
        |> Seq.filter (lockFile.ResolvedPackages.ContainsKey >> not)
        |> Set.ofSeq
    
    let modified =
        [for t in lockFile.GetTopLevelDependencies() do 
            let name = t.Key
            match directMap.TryFind name with
            | Some range ->
                if range.IsInRange t.Value.Version |> not then
                    yield name // Modified
            | _ -> yield name // Removed
        ]
        |> List.map lockFile.GetAllNormalizedDependenciesOf
        |> Seq.concat
        |> Set.ofSeq           

    added 
    |> Set.union modified

let FixUnchangedDependencies (dependenciesFile:DependenciesFile) (oldLockFile:LockFile) =
    let changedDependencies = findChangesInDependenciesFile(dependenciesFile,oldLockFile)
            
    oldLockFile.ResolvedPackages
    |> Seq.map (fun kv -> kv.Value)
    |> Seq.filter (fun p -> not <| changedDependencies.Contains(NormalizedPackageName p.Name))
    |> Seq.fold 
            (fun (dependenciesFile : DependenciesFile) resolvedPackage ->                 
                    dependenciesFile.AddFixedPackage(
                        resolvedPackage.Name,
                        "= " + resolvedPackage.Version.ToString(),
                        resolvedPackage.FrameworkRestrictions))
            dependenciesFile