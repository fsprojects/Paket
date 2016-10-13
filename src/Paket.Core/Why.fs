module Paket.Why

open Paket.Logging

let ohWhy (packageName, lockFile: LockFile, groupName, usage) =
    let group = lockFile.GetGroup(groupName)
    if not <| group.Resolution.ContainsKey packageName then
        match lockFile.Groups |> Seq.filter (fun g -> g.Value.Resolution.ContainsKey packageName) |> Seq.toList with
        | _ :: _ as otherGroups ->
            traceWarnfn 
                "NuGet %O was not found in %s group. However it was found in following groups: %A. Specify correct group." 
                packageName
                (groupName.ToString())
                (otherGroups |> List.map (fun pair -> pair.Key.ToString()))

            usage |> traceWarn
        | [] ->
            traceErrorfn "NuGet %O was not found in %s" packageName Constants.LockFileName
    else
        let isTopLevel =
            lockFile.GetTopLevelDependencies groupName
            |> Map.exists (fun key _ -> key = packageName)
        if isTopLevel then
            tracefn "NuGet %O is in %s group because it's defined as a top-level dependency" packageName (groupName.ToString()) 
        else
            let xs =
                group.Resolution
                |> Seq.filter (fun pair -> pair.Value.Dependencies
                                        |> Seq.exists (fun (name,_,_) -> name = packageName))
                |> Seq.map (fun pair -> pair.Key.ToString())
                |> Seq.toList
            
            tracefn "NuGet %O is in %s group because it's a dependency of those packages: %A"
                    packageName
                    (groupName.ToString())
                    xs
