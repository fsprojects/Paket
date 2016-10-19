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

type WhyPath = list<PackageName * bool>

type DependencyChain = Set<PackageName>

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DependencyChain =
    let format (chain : DependencyChain) =
        chain
        |> Seq.mapi (fun i name -> sprintf "%s-> %O" (String.replicate i "  ") name)
        |> String.concat Environment.NewLine

    let formatMany chains =
        chains
        |> Seq.map format
        |> String.concat (String.replicate 2 Environment.NewLine)

// In context of FAKE project dependencies
type Reason =
// e.g. Argu - specified in paket.dependencies, is not a dependency of any other package
| TopLevel
// e.g. Microsoft.AspNet.Razor - specified in paket.dependencies, but also a dependency of other package(s)
| Direct of DependencyChain list
// e.g. Microsoft.AspNet.Mvc - not specified in paket.dependencies, a dependency of other package(s)
| Transient of DependencyChain list

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Reason =
    let format = function
    | TopLevel -> 
        sprintf "direct (%s) and top-level dependency" 
                Constants.DependenciesFileName
    | Direct chains -> 
        sprintf "direct (%s) dependency. It's a part of following build chains: %s"
                Constants.DependenciesFileName
                (DependencyChain.formatMany chains)
    | Transient chains -> 
        sprintf "transient dependency. It's a part of following dependency chains: %s"
                (DependencyChain.formatMany chains)

    let infer (name : PackageName, 
               groupName : GroupName,
               directDeps : Set<PackageName>, 
               lockFile : LockFile) =
        if Set.contains name directDeps then 
            TopLevel
        else
            TopLevel

let prettyFormatPath (path: WhyPath) = 
    path 
    |> List.mapi
        (fun i (name, isDirect) -> 
            sprintf "%s-> %O%s"
                    (String.replicate i "  ")
                    name
                    (if isDirect then sprintf " (%s)" Constants.DependenciesFileName else ""))
    |> String.concat Environment.NewLine

let prettyPrintPath (path: WhyPath) =
    path
    |> prettyFormatPath
    |> tracen
    tracen ""

let ohWhy (packageName, 
           directDeps : Set<PackageName>, 
           lockFile : LockFile, 
           groupName, 
           usage, 
           options) =

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
            |> List.map (List.map (fun name -> name, Set.contains name directDeps))
            |> List.groupBy (List.item 0)

        tracefn "Dependency paths for %O in group %s:" packageName (groupName.ToString())
        tracen ""

        for ((top,_), paths) in topLevelPaths do
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