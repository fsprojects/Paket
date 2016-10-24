module Paket.Why

open System

open Paket.Domain
open Paket.Logging

open Chessie.ErrorHandling

type AdjGraph<'a> = list<'a * list<'a>>

module AdjGraph =
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

type DependencyChain = List<PackageName>

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

type InferError =
| NuGetNotInLockFile
| NuGetNotInGroup of groupsHavingNuGet : GroupName list

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Reason =
    let format = function
    | TopLevel -> 
        sprintf "direct (%s) and top-level dependency." 
                Constants.DependenciesFileName
    | Direct chains -> 
        sprintf "direct (%s) dependency."
                Constants.DependenciesFileName
    | Transient chains -> 
        sprintf "transient dependency."

    let infer (packageName : PackageName, 
               groupName : GroupName,
               directDeps : Set<PackageName>, 
               lockFile : LockFile) :
               Result<Reason, InferError> =

        let inferError () =
            let otherGroups =
                lockFile.Groups 
                |> Seq.filter (fun pair -> pair.Value.Resolution.ContainsKey packageName) 
                |> Seq.map (fun pair -> pair.Key)
                |> Seq.toList
            if List.isEmpty otherGroups then
                NuGetNotInLockFile
            else
                NuGetNotInGroup otherGroups

        let group = lockFile.GetGroup groupName
        if not <| group.Resolution.ContainsKey packageName then
            inferError () 
            |> List.singleton 
            |> Result.Bad
        else
            let graph = depGraph group.Resolution
            let topLevelDeps = 
                lockFile.GetTopLevelDependencies groupName
                |> Seq.map (fun pair -> pair.Key)
                |> Set.ofSeq
            let chains = 
                topLevelDeps
                |> Set.toList
                |> List.collect (fun p -> AdjGraph.paths p packageName graph)
            match Set.contains packageName directDeps, Set.contains packageName topLevelDeps with
            | true, true ->
                Result.Succeed TopLevel
            | true, false ->
                Result.Succeed (Direct chains)
            | false, false ->
                Result.Succeed (Transient chains)
            | false, true ->
                failwith "impossible"

let ohWhy (packageName, 
           directDeps : Set<PackageName>, 
           lockFile : LockFile, 
           groupName, 
           usage, 
           options) =

    match Reason.infer(packageName, groupName, directDeps, lockFile) with
    | Result.Bad [NuGetNotInLockFile] ->
        traceErrorfn "NuGet %O was not found in %s" packageName Constants.LockFileName
    | Result.Bad [NuGetNotInGroup otherGroups] ->
        traceWarnfn 
            "NuGet %O was not found in %s group. However it was found in following groups: %A. Specify correct group." 
            packageName
            (groupName.ToString())
            (otherGroups |> List.map (fun pair -> pair.ToString()))

        usage |> traceWarn
    | Result.Ok (reason, []) ->
        reason
        |> Reason.format
        |> sprintf "NuGet %O is a %s" packageName
        |> tracen

        match reason with
        | TopLevel -> ()
        | Direct chains
        | Transient chains ->
            tracefn "It's a part of following dependency chains:"
            tracen ""
            for (top, chains) in chains |> List.groupBy (Seq.item 0) do
                match chains |> List.sortBy Seq.length, options.AllPaths with
                | shortest :: [], false ->
                    DependencyChain.format shortest |> tracen
                | shortest :: rest, false ->
                    DependencyChain.format shortest |> tracen
                    tracen ""
                    tracefn 
                        "... and %d path%s more starting at %O. To display all paths use --allpaths flag" 
                        rest.Length 
                        (if rest.Length > 1 then "s" else "") 
                        top
                | all, true ->
                    DependencyChain.formatMany all |> tracen
                | _ ->
                    failwith "impossible"
                tracen ""
    | _ ->
        failwith "impossible"