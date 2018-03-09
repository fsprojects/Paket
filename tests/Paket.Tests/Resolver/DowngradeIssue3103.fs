module Paket.DowngradeIssue3103

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers
open Paket.Domain

let graph = 
  OfSimpleGraph [
    "delphi-tf-latest-convert ","0.0.75",["delphi-TaxDoc", VersionRequirement(VersionRange.AtLeast "0",PreReleaseStatus.No)]
    "delphi-tf-latest-convert ","0.0.74",["delphi-TaxDoc", VersionRequirement(VersionRange.AtLeast "0",PreReleaseStatus.No)]
    "delphi-TaxDoc","17.4.0.16",[]
    "delphi-TaxDoc","17.3.0.41",[]
    "delphi-tf-latest-calc,","1.0.127",[]
  ]

let config = """
source "https://www.nuget.org/api/v2"

nuget delphi-tf-latest-convert
"""


[<Test>]
let ``should resolve config``() = 
    let cfg = DependenciesFile.FromSource(config)
    let resolved = ResolveWithGraph(cfg,noSha1, VersionsFromGraphAsSeq graph, PackageDetailsFromGraph graph).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    getVersion resolved.[PackageName "delphi-tf-latest-convert"] |> shouldEqual "0.0.75"
