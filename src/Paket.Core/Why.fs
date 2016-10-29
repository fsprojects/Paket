module Paket.Why

open System

open Paket.Domain
open Paket.Logging

open Chessie.ErrorHandling

type AdjLblGraph<'a, 'b> = list<'a * list<('a * 'b)>>

type LblPath<'a, 'b> = 'a * LblPathNode<'a, 'b>

and LblPathNode<'a, 'b> =
| LblPathNode of LblPath<'a, 'b>
| LblPathLeaf of 'a * 'b

module AdjLblGraph =
    let adj n (g: AdjLblGraph<_, _>) =
        g
        |> List.find (fst >> (=) n)
        |> snd

    let rec paths start stop g : list<LblPath<_, _>> =
        [ for (n, lbl) in adj start g do
            if n = stop then yield (start, LblPathLeaf (stop, lbl))
            for path in paths n stop g do 
                yield (start, LblPathNode path)]

let depGraph (res : PackageResolver.PackageResolution) : AdjLblGraph<Domain.PackageName, VersionRequirement> =
    res
    |> Seq.toList
    |> List.map (fun pair -> pair.Key, (pair.Value.Dependencies 
                                       |> Set.map (fun (p,v,_) -> p,v) 
                                       |> Set.toList))

type WhyOptions = 
    { AllPaths : bool
      VersionConstraints : bool }

type DependencyChain = LblPath<PackageName, VersionRequirement>

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module DependencyChain =
    let first ((name, _) : DependencyChain) = name

    let rec length = function
    | (_, LblPathNode n) -> length n + 1
    | (_, LblPathLeaf _) -> 2

    let format showVersions (c : DependencyChain) =
        let formatVerReq (vr : VersionRequirement) =
            match vr.ToString() with
            | "" -> ""
            | nonempty -> sprintf " (%s)" nonempty
        let formatName name i = sprintf "%s-> %O" (String.replicate i "  ") name
        let rec format' i (name,chain) =
            let rest = 
                match chain, showVersions with
                | LblPathNode chain,_ -> format' (i+1) chain
                | LblPathLeaf (name,req), true -> sprintf "%s%s" (formatName name (i+1)) (formatVerReq req)
                | LblPathLeaf (name,req), false -> formatName name (i+1)
            sprintf "%s%s%s" (formatName name i) Environment.NewLine rest
        
        format' 0 c

    let formatMany showVersions chains =
        chains
        |> Seq.map (format showVersions)
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
               Result<_,_> =

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
                |> List.collect (fun p -> AdjLblGraph.paths p packageName graph)
            let version =
                group.Resolution
                |> Map.find packageName
                |> fun x -> x.Version
            match Set.contains packageName directDeps, Set.contains packageName topLevelDeps with
            | true, true ->
                Result.Ok ((TopLevel, version), [])
            | true, false ->
                Result.Ok ((Direct chains, version), [])
            | false, false ->
                Result.Ok ((Transient chains, version), [])
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
    | Result.Ok ((reason, version), []) ->
        reason
        |> Reason.format
        |> sprintf "NuGet %O%s is a %s" packageName (if options.VersionConstraints then " " + version.ToString() else "")
        |> tracen

        match reason with
        | TopLevel -> ()
        | Direct chains
        | Transient chains ->
            tracefn "It's a part of following dependency chains:"
            tracen ""
            for (top, chains) in chains |> List.groupBy (DependencyChain.first) do
                match chains |> List.sortBy DependencyChain.length, options.AllPaths with
                | shortest :: [], false ->
                    DependencyChain.format options.VersionConstraints shortest |> tracen
                | shortest :: rest, false ->
                    DependencyChain.format options.VersionConstraints shortest |> tracen
                    tracen ""
                    tracefn 
                        "... and %d path%s more starting at %O. To display all paths use --allpaths flag" 
                        rest.Length 
                        (if rest.Length > 1 then "s" else "") 
                        top
                | all, true ->
                    DependencyChain.formatMany options.VersionConstraints all |> tracen
                | _ ->
                    failwith "impossible"
                tracen ""
    | _ ->
        failwith "impossible"