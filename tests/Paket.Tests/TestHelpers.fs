module Paket.TestHelpers

open Paket

let resolve graph (dependencies: (string * VersionRange) seq) =
    let packages = dependencies |> Seq.map (fun (n,v) -> { Name = n; VersionRange = v; Source = ""})
    Resolver.Resolve(Discovery.DictionaryDiscovery graph, packages)

let getVersion resolved =
    match resolved with
    | ResolvedVersion.Resolved x ->
        match x.ReferencedVersion with
        | Exactly v -> v

let getDefiningPackage resolved =
    match resolved with
    | ResolvedVersion.Resolved x -> x.DefiningPackage

let getDefiningVersion resolved =
    match resolved with
    | ResolvedVersion.Resolved x -> x.DefiningVersion