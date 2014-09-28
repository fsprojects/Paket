module Paket.TestHelpers

open Paket
open System

let PackageDetailsFromGraph (graph : seq<string * string * (string * VersionRequirement) list>) sources (package:string) version = 
    let name,dependencies = 
        graph
        |> Seq.filter (fun (p, v, _) -> p.ToLower() = package.ToLower() && v = version)
        |> Seq.map (fun (n, _, d) -> n,d)
        |> Seq.head

    { Name = name
      Source = Seq.head sources
      DownloadLink = ""
      DirectDependencies = dependencies }

let VersionsFromGraph (graph : seq<string * string * (string * VersionRequirement) list>) sources (package : string) = 
    graph
    |> Seq.filter (fun (p, _, _) -> p.ToLower() = package.ToLower())
    |> Seq.map (fun (_, v, _) -> SemVer.parse v)
    |> Seq.toList

let safeResolve graph (dependencies : (string * VersionRange) list)  = 
    let packages = 
        dependencies |> List.map (fun (n, v) -> 
                            { Name = n
                              VersionRequirement = VersionRequirement(v,PreReleaseStatus.No)
                              Sources = [ PackageSource.NugetSource "" ]
                              Parent = None
                              ResolverStrategy = ResolverStrategy.Max })
    PackageResolver.Resolve(VersionsFromGraph graph, PackageDetailsFromGraph graph, packages)

let resolve graph dependencies = 
    safeResolve graph dependencies
    |> UpdateProcess.extractResolvedPackagesOrFail

let getVersion (resolved:ResolvedPackage) = resolved.Version.ToString()

let getSource (resolved:ResolvedPackage) = resolved.Source

let normalizeLineEndings (text : string) = 
    text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", Environment.NewLine)
let toLines (text : string) = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n')

let noSha1 owner repo branch = failwith "no github configured"

let fakeSha1 owner repo branch = "12345"
