module Resovler.PropertyTests

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers
open Paket.Domain
open Paket.PackageResolver
open FsCheck
open PropertyTestGenerators
open System.Collections.Generic
open Paket.Requirements

let hasDuplicates (resolution:PackageResolution) =
    let hashSet = HashSet<_>()
    resolution
    |> Map.exists (fun p _ -> hashSet.Add p |> not)

let hasError ((g,deps):ResolverPuzzle) (resolution:PackageResolution) =
    deps
    |> List.exists (fun (d,vr:VersionRequirement) -> 
        match resolution |> Map.tryFind d with
        | Some r -> vr.IsInRange(r.Version) |> not
        | None -> false)

let isValid ((g,deps):ResolverPuzzle) resolution =
    deps
    |> List.forall (fun (d,vr:VersionRequirement) -> 
        match resolution |> Map.tryFind d with
        | Some r -> vr.IsInRange(r.Version) 
        | None -> false)

let bruteForce ((g,deps):ResolverPuzzle) =
    let rec check ((g,deps):ResolverPuzzle) resolution =
        match g with
        | (p,v,packageDeps) :: g' ->
            let select() =
                let resolved =
                    { Name = p
                      Version = v
                      Dependencies = Set.empty
                      Unlisted = false
                      Settings = InstallSettings.Default
                      Source = PackageSources.DefaultNuGetSource }
                match Map.tryFind p resolution with
                | Some(_) -> None
                | _ ->
                    let resolution' = Map.add p resolved resolution
                    let deps' = packageDeps @ deps
                    if hasError (g',deps') resolution' then None else
                    check (g',deps') resolution'

            match check (g',deps) resolution with
            | None -> select()
            | r -> r

        | _ -> if isValid (g,deps) resolution then Some resolution else None

    check (g,deps) Map.empty

[<Test>]
let ``can resolve empty requirements`` () =
    check (fun (g:PackageGraph) ->
        match resolve g [] with
        | Resolution.Ok resolution -> resolution |> Map.isEmpty            
        | _ -> false)

[<Test>]
[<Ignore>]
let ``if it resolves then, it should satisfy all deps. if not we have a real conflict`` () =
    check (fun ((g,deps):ResolverPuzzle) ->
        match resolve g deps with
        | Resolution.Ok resolution ->
            if hasDuplicates resolution then
                failwithf "duplicates found in resolution: %A" resolution

            deps
            |> List.forall (fun (d,vr) -> 
                match resolution |> Map.tryFind d with
                | Some r -> vr.IsInRange(r.Version) 
                | None -> false)
            
        | conflict ->
            match bruteForce (g,deps) with
            | None -> ()
            | Some resolution ->
                failwithf "brute force found %A" resolution

            let conflicts = conflict.GetConflicts()
            conflicts |> List.isEmpty |> not)