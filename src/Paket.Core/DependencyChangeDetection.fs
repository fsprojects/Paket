module Paket.DependencyChangeDetection

open Paket.Domain
open Paket.Requirements
open Paket.PackageResolver

let findChangesInDependenciesFile(dependenciesFile:DependenciesFile,lockFile:LockFile) =   
    let directMap =
        dependenciesFile.Packages
        |> Seq.map (fun d -> NormalizedPackageName d.Name,d)
        |> Map.ofSeq

    let inline hasChanged (newRequirement:PackageRequirement) (originalPackage:ResolvedPackage) =
      if newRequirement.VersionRequirement.IsInRange originalPackage.Version |> not then true
      elif newRequirement.Settings <> originalPackage.Settings then true
      else false

    let added =
        dependenciesFile.Packages
        |> Seq.map (fun d -> NormalizedPackageName d.Name,d)
        |> Seq.filter (fun (name,pr) ->
            match lockFile.GetCompleteResolution().TryFind name with
            | Some p -> hasChanged pr p
            | _ -> true)
        |> Seq.map fst
        |> Set.ofSeq
    
    let modified =
        [for g in lockFile.Groups do
             for t in lockFile.GetTopLevelDependencies(g.Key) do 
                let name = t.Key
                match directMap.TryFind name with
                | Some pr ->
                    if hasChanged pr t.Value then
                        yield name // Modified
                | _ -> yield name // Removed
        ]
        |> List.map lockFile.GetAllNormalizedDependenciesOf
        |> Seq.concat
        |> Set.ofSeq           

    added 
    |> Set.union modified

let PinUnchangedDependencies (dependenciesFile:DependenciesFile) (oldLockFile:LockFile) (changedDependencies:Set<NormalizedPackageName>) =
    oldLockFile.GetCompleteResolution()
    |> Seq.map (fun kv -> kv.Value)
    |> Seq.filter (fun p -> not <| changedDependencies.Contains(NormalizedPackageName p.Name))
    |> Seq.fold 
            (fun (dependenciesFile : DependenciesFile) resolvedPackage ->                 
                    dependenciesFile.AddFixedPackage(
                        resolvedPackage.Name,
                        "= " + resolvedPackage.Version.ToString(),
                        resolvedPackage.Settings))
            dependenciesFile