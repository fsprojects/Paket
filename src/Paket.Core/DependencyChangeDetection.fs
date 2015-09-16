module Paket.DependencyChangeDetection

open Paket.Domain
open Paket.Requirements
open Paket.PackageResolver

let findChangesInDependenciesFile(dependenciesFile:DependenciesFile,lockFile:LockFile) =   

    let inline hasChanged (newRequirement:PackageRequirement) (originalPackage:ResolvedPackage) =
      if newRequirement.VersionRequirement.IsInRange originalPackage.Version |> not then true
      elif newRequirement.Settings <> originalPackage.Settings then true
      else false

    let added groupName =
        dependenciesFile.Groups.[groupName].Packages
        |> Seq.map (fun d -> d.Name,d)
        |> Seq.filter (fun (name,pr) ->
            match lockFile.Groups |> Map.tryFind groupName with
            | None -> true
            | Some group ->
                match group.Resolution.TryFind name with
                | Some p -> hasChanged pr p
                | _ -> true)
        |> Seq.map (fun (p,_) -> groupName,p)
        |> Set.ofSeq
    
    let modified groupName = 
        let directMap =
            match dependenciesFile.Groups |> Map.tryFind groupName with
            | None -> Map.empty
            | Some group ->
                group.Packages
                |> Seq.map (fun d -> d.Name,d)
                |> Map.ofSeq

        [for t in lockFile.GetTopLevelDependencies(groupName) do
            let name = t.Key
            match directMap.TryFind name with
            | Some pr -> if hasChanged pr t.Value then yield groupName, name // Modified
            | _ -> yield groupName, name // Removed
        ]
        |> List.map lockFile.GetAllNormalizedDependenciesOf
        |> Seq.concat
        |> Seq.map (fun p -> groupName,p)
        |> Set.ofSeq

    let groupNames =
        dependenciesFile.Groups
        |> Seq.map (fun kv -> kv.Key)
        |> Seq.append (lockFile.Groups |> Seq.map (fun kv -> kv.Key))

    groupNames
    |> Seq.map (fun groupName -> 
            let added = added groupName 
            let modified = modified groupName
            Set.union added modified)
    |> Seq.concat
    |> Set.ofSeq

let PinUnchangedDependencies (dependenciesFile:DependenciesFile) (oldLockFile:LockFile) (changedDependencies:Set<GroupName*PackageName>) =
    oldLockFile.GetGroupedResolution()
    |> Seq.filter (fun kv -> not <| changedDependencies.Contains(kv.Key))
    |> Seq.fold 
            (fun (dependenciesFile : DependenciesFile) kv ->
                    let resolvedPackage = kv.Value
                    dependenciesFile.AddFixedPackage(
                        fst kv.Key,
                        resolvedPackage.Name,
                        "= " + resolvedPackage.Version.ToString(),
                        resolvedPackage.Settings))
            dependenciesFile