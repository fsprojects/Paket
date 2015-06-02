module Paket.DependencyChangeDetection

open Paket.Domain
open Paket.Requirements

let findChangesInDependenciesFile(dependenciesFile:DependenciesFile,lockFile:LockFile) =   
    let directMap =
        dependenciesFile.Packages
        |> Seq.map (fun d -> NormalizedPackageName d.Name,d)
        |> Map.ofSeq

    let inline isChanged (newRequirement:PackageRequirement) originalVersion =
      if newRequirement.VersionRequirement.IsInRange originalVersion |> not then
        true
      else false

    let added =
        dependenciesFile.Packages
        |> Seq.map (fun d -> NormalizedPackageName d.Name,d)
        |> Seq.filter (fun (name,pr) ->
            match lockFile.ResolvedPackages.TryFind name with
            | Some p -> isChanged pr p.Version
            | _ -> true)
        |> Seq.map fst
        |> Set.ofSeq
    
    let modified =
        [for t in lockFile.GetTopLevelDependencies() do 
            let name = t.Key
            match directMap.TryFind name with
            | Some pr ->
                if isChanged pr t.Value.Version then
                    yield name // Modified
            | _ -> yield name // Removed
        ]
        |> List.map lockFile.GetAllNormalizedDependenciesOf
        |> Seq.concat
        |> Set.ofSeq           

    added 
    |> Set.union modified

let PinUnchangedDependencies (dependenciesFile:DependenciesFile) (oldLockFile:LockFile) (changedDependencies:Set<NormalizedPackageName>) =
    oldLockFile.ResolvedPackages
    |> Seq.map (fun kv -> kv.Value)
    |> Seq.filter (fun p -> not <| changedDependencies.Contains(NormalizedPackageName p.Name))
    |> Seq.fold 
            (fun (dependenciesFile : DependenciesFile) resolvedPackage ->                 
                    dependenciesFile.AddFixedPackage(
                        resolvedPackage.Name,
                        "= " + resolvedPackage.Version.ToString(),
                        resolvedPackage.Settings))
            dependenciesFile