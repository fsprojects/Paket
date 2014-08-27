module Paket.TestHelpers

open Paket

let DictionaryDiscovery(graph : seq<string * string * (string * VersionRange) list>) = 
    { new IDiscovery with
          member __.GetDirectDependencies(sourceType, source, package, version) = 
              graph
              |> Seq.filter (fun (p,v,_) -> p = package && v = version) 
              |> Seq.map (fun (_,_,d) -> d) 
              |> Seq.head 
              |> List.map (fun (p,v) -> { Name = p; VersionRange = v; SourceType = sourceType; Source = source})
          member __.GetVersions package = 
              graph              
              |> Seq.filter (fun (p,_,_) -> p = package)
              |> Seq.map (fun (_,v,_) -> v) }

let resolve graph (dependencies: (string * VersionRange) seq) =
    let packages = dependencies |> Seq.map (fun (n,v) -> { Name = n; VersionRange = v; SourceType = ""; Source = ""})
    Resolver.Resolve(DictionaryDiscovery graph, packages)

let getVersion resolved =
    match resolved with
    | ResolvedVersion.Resolved x ->
        match x.DependentPackage.VersionRange with
        | Exactly v -> v

let getDefiningPackage resolved =
    match resolved with
    | ResolvedVersion.Resolved x -> x.DefiningPackage

let getDefiningVersion resolved =
    match resolved with
    | ResolvedVersion.Resolved x -> x.DefiningVersion

let getSourceType resolved =
    match resolved with
    | ResolvedVersion.Resolved x -> x.DependentPackage.SourceType

let getSource resolved =
    match resolved with
    | ResolvedVersion.Resolved x -> x.DependentPackage.Source