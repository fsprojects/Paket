module Paket.DependencyChangeDetection

open Paket.Domain

let findChangesInDependenciesFile(dependenciesFile:DependenciesFile,lockFile:LockFile) =   
    let direct =
        dependenciesFile.DirectDependencies
        |> Seq.map (fun d -> NormalizedPackageName d.Key)
        |> Set.ofSeq

    let added =
        direct
        |> Set.filter (lockFile.ResolvedPackages.ContainsKey >> not)
        
    let removed =
        lockFile.GetTopLevelDependencies()
        |> Seq.filter (direct.Contains >> not)
        |> Seq.map lockFile.GetAllNormalizedDependenciesOf
        |> Seq.concat
        |> Set.ofSeq

    Set.union added removed

let FixUnchangedDependencies (dependenciesFile:DependenciesFile) (oldLockFile:LockFile) =
    let changedDependencies = findChangesInDependenciesFile(dependenciesFile,oldLockFile)
            
    oldLockFile.ResolvedPackages
    |> Seq.map (fun kv -> kv.Value)
    |> Seq.filter (fun p -> not <| changedDependencies.Contains(NormalizedPackageName p.Name))
    |> Seq.fold 
            (fun (dependenciesFile : DependenciesFile) resolvedPackage ->                 
                    dependenciesFile.AddFixedPackage(resolvedPackage.Name, "= " + resolvedPackage.Version.ToString()))
            dependenciesFile