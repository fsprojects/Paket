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

let createsError ((g,deps):ResolverPuzzle) (package:ResolvedPackage) =
    deps
    |> List.exists (fun (d,vr:VersionRequirement) -> d = package.Name && vr.IsInRange(package.Version) |> not)

type Status =
| Open of Dependency list
| Valid
| Error

let isValid ((g,deps):ResolverPuzzle) resolution =
    deps
    |> List.fold (fun s (d,vr:VersionRequirement) ->
        match s with
        | Open deps ->
            match resolution |> Map.tryFind d with
            | Some r -> if not <| vr.IsInRange(r.Version) then Error else s
            | None -> Open ((d,vr)::deps)
        | _ -> s) (Open [])
    |> fun s -> 
        match s with
        | Open [] -> Valid
        | Error -> Error
        | Valid -> Valid
        | Open stillOpen ->
            let satisfiable =
                stillOpen
                |> List.forall (fun (d,vr:VersionRequirement) ->
                    g
                    |> List.exists (fun (p,v,_) -> p = d && vr.IsInRange v))
            if satisfiable then s else Error


let bruteForce ((g,deps):ResolverPuzzle) =
    let rec check ((g,deps):ResolverPuzzle) resolution =
        match isValid (g,deps) resolution with
        | Error -> None
        | Valid -> Some resolution
        | Open _ ->
            match g with
            | (p,v,packageDeps) :: g' ->
                match check (g',deps) resolution with
                | None -> 
                    match Map.tryFind p resolution with
                    | Some(_) -> None
                    | _ ->
                        let resolved =
                            { Name = p
                              Version = v
                              Dependencies = Set.empty
                              Unlisted = false
                              Settings = InstallSettings.Default
                              Source = PackageSources.DefaultNuGetSource }
                                        
                        let deps' = packageDeps @ deps
                        if createsError (g,deps') resolved then None else
                        let resolution' = Map.add p resolved resolution
                        check (g',deps') resolution'                
                | r -> r
            | _ -> None

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

            if (resolution |> Seq.length) > 7 then
                failwithf "Found big %A" resolution

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