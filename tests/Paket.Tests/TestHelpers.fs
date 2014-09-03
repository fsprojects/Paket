module Paket.TestHelpers

open Paket
open System

let DictionaryDiscovery(graph : seq<string * string * (string * VersionRange) list>) = 
    { new IDiscovery with
          
          member __.GetPackageDetails(force, sourceType, source, package, version) = 
              async { 
                  let dependencies =
                    graph
                         |> Seq.filter (fun (p, v, _) -> p = package && v = version)
                         |> Seq.map (fun (_, _, d) -> d)
                         |> Seq.head
                         |> List.map (fun (p, v) -> 
                                { Name = p
                                  VersionRange = v
                                  SourceType = sourceType
                                  Source = source })
                  return "",dependencies
              }
          
          member __.GetVersions(sourceType, source, package) = 
              async { 
                  return graph
                         |> Seq.filter (fun (p, _, _) -> p = package)
                         |> Seq.map (fun (_, v, _) -> v)
              } }

let resolve graph (dependencies: (string * VersionRange) seq) =
    let packages = dependencies |> Seq.map (fun (n,v) -> { Name = n; VersionRange = v; SourceType = ""; Source = "" })
    Resolver.Resolve(true, DictionaryDiscovery graph, packages)

let getVersion resolved =
    match resolved with
    | ResolvedDependency.Resolved x ->
        match x.Referenced.VersionRange with
        | Specific v -> v.ToString()

let getDefiningPackage resolved =
    match resolved with
    | ResolvedDependency.Resolved (FromPackage x) -> x.Defining.Name

let getDefiningVersion resolved =
    match resolved with
    | ResolvedDependency.Resolved (FromPackage x) -> 
        match x.Defining.VersionRange with
        | Specific v -> v.ToString()

let getSourceType resolved =
    match resolved with
    | ResolvedDependency.Resolved x -> x.Referenced.SourceType

let getSource resolved =
    match resolved with
    | ResolvedDependency.Resolved x -> x.Referenced.Source


let normalizeLineEndings (text:string) = text.Replace("\r\n","\n").Replace("\r","\n").Replace("\n",Environment.NewLine)

let toLines (text:string) = text.Replace("\r\n","\n").Replace("\r","\n").Split('\n')