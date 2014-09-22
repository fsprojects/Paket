module Paket.TestHelpers

open Paket
open System

let PackageDetailsFromGraph (graph : seq<string * string * (string * VersionRange) list>) sources package resolverStrategy version = 
    let dependencies = 
        graph
        |> Seq.filter (fun (p, v, _) -> p = package && v = version)
        |> Seq.map (fun (_, _, d) -> d)
        |> Seq.head
        |> List.map (fun (p, v) -> 
                { Name = p
                  VersionRange = v
                  ResolverStrategy = resolverStrategy
                  Sources = sources })
    { Name = package
      Source = Seq.head sources
      DownloadLink = ""
      DirectDependencies = dependencies }

let VersionsFromGraph (graph : seq<string * string * (string * VersionRange) list>) sources (package : string) = 
    graph
    |> Seq.filter (fun (p, _, _) -> p.ToLower() = package.ToLower())
    |> Seq.map (fun (_, v, _) -> SemVer.parse v)
    |> Seq.toList

let safeResolve graph (dependencies : (string * VersionRange) seq) = 
    let packages = 
        dependencies |> Seq.map (fun (n, v) -> 
                            { Name = n
                              VersionRange = v
                              Sources = [ Nuget "" ]
                              ResolverStrategy = ResolverStrategy.Max })
    PackageResolver.Resolve(VersionsFromGraph graph, PackageDetailsFromGraph graph, packages)

let resolve graph dependencies = 
    safeResolve graph dependencies
    |> UpdateProcess.getResolvedPackagesOrFail

let getVersion (resolved:ResolvedPackage) = resolved.Version.ToString()

let getSource (resolved:ResolvedPackage) = resolved.Source

let normalizeLineEndings (text : string) = 
    text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", Environment.NewLine)
let toLines (text : string) = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n')
