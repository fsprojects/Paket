module Paket.Why

open System

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

type WhyOptions = 
    { AllPaths : bool }

let prettyFormatPath path = 
    path 
    |> List.mapi 
        (fun i v -> 
            sprintf "%s-> %s%s"
                    (String.replicate i "  ")
                    v
                    (if i = 0 then sprintf " (%s)" Constants.DependenciesFileName else ""))
    |> String.concat Environment.NewLine

let prettyPrintPath (path: PackageName list) =
    path
    |> List.map (sprintf "%O")
    |> prettyFormatPath
    |> tracen
    tracen ""

let ohWhy (packageName, lockFile : LockFile, groupName, usage, options) =
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
            |> List.groupBy (List.item 0)

        tracefn "Dependency paths for %O in group %s:" packageName (groupName.ToString())
        tracen ""

        for (top, paths) in topLevelPaths do
            match paths |> List.sortBy List.length with
            | shortest :: rest ->
                prettyPrintPath shortest

                match rest, options.AllPaths with
                | _ :: _, false ->
                    tracefn 
                        "... and %d path%s more starting at %O. To display all paths use --allpaths flag" 
                        rest.Length 
                        (if rest.Length > 1 then "s" else "") 
                        top
                    tracen ""
                | _ :: _, true ->
                    List.iter prettyPrintPath rest
                | [], _ ->
                    ()

            | [] ->
                failwith "impossible"