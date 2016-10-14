module Paket.Why

open Paket.Domain
open Paket.Logging

type AdjGraph<'a> = list<'a * list<'a>>

let adj n (g: AdjGraph<_>) =
    g
    |> List.find (fst >> (=) n)
    |> snd

let rec paths start stop (g : AdjGraph<'a>) =
    if start = stop then [[start]]
    else
        [ for n in adj start g do
            for path in paths n stop g do
                yield start :: path ]

let depGraph (res : PackageResolver.PackageResolution) : AdjGraph<Domain.PackageName> =
    res
    |> Seq.toList
    |> List.map (fun pair -> pair.Key, (pair.Value.Dependencies 
                                       |> Set.map (fun (p,_,_) -> p) 
                                       |> Set.toList))

let prettyPrintPath path = 
    path 
    |> List.mapi 
        (fun i v -> 
            sprintf "%s%s%s%s"
                    (String.replicate i "  ")
                    (if i > 0 then "-> " else "")
                    v
                    (if i = 0 then " (top-level dependency)" else ""))
    |> List.iter tracen

let ohWhy (packageName, lockFile : LockFile, groupName, usage) =
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
        let g = depGraph group.Resolution
        let topLevel = lockFile.GetTopLevelDependencies groupName
        let topLevelPaths = 
            topLevel
            |> Seq.map (fun pair -> pair.Key)
            |> Seq.toList
            |> List.collect (fun p -> paths p packageName g)
        tracefn "Dependency graphs for %O" packageName
        tracefn ""
        for path in topLevelPaths do
            path 
            |> List.map (sprintf "%O")
            |> prettyPrintPath
            tracefn ""
        //let isTopLevel =
        //    lockFile.GetTopLevelDependencies groupName
        //    |> Map.exists (fun key _ -> key = packageName)
        //if isTopLevel then
        //    tracefn "NuGet %O is in %s group because it's defined as a top-level dependency" packageName (groupName.ToString()) 
        //else
        //    let xs =
        //        group.Resolution
        //        |> Seq.filter (fun pair -> pair.Value.Dependencies
        //                                |> Seq.exists (fun (name,_,_) -> name = packageName))
        //        |> Seq.map (fun pair -> pair.Key.ToString())
        //        |> Seq.toList
        //    
        //    tracefn "NuGet %O is in %s group because it's a dependency of those packages: %A"
        //            packageName
        //            (groupName.ToString())
        //            xs
