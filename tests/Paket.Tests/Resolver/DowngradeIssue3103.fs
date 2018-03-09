module Paket.DowngradeIssue3103

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers
open Paket.Domain

let exactVersion s =
    let info = SemVer.Parse s |> VersionRange.Specific
    VersionRequirement(info, PreReleaseStatus.No)

let anyVersion = VersionRequirement(VersionRange.AtLeast "0",PreReleaseStatus.No)

let graph = 
  OfSimpleGraph [
    "delphi-tf-latest-convert ","0.0.75",[("delphi-TaxDoc", anyVersion); ("delphi-tf-latest-calc", anyVersion)]
    "delphi-tf-latest-convert ","0.0.74",[("delphi-TaxDoc", anyVersion); ("delphi-tf-latest-calc", anyVersion)]
    "delphi-TaxDoc","17.4.0.16",["dummy", (exactVersion "17.4.0.16")]
    "delphi-TaxDoc","17.3.0.41",["dummy", (exactVersion "17.3.0.41")]
    "delphi-tf-latest-calc","1.0.127",["delphi-CchData", (exactVersion "17.3.0.41")]
    "delphi-CchData","17.3.0.41",["dummy", (exactVersion "17.3.0.41")]
    "dummy","17.4.0.16",[]
    "dummy","17.3.0.41",[]
  ]

let config = """
source "https://www.nuget.org/api/v2"

nuget delphi-tf-latest-convert
"""


[<Test>]
let ``should resolve latets version for #3103``() = 
    let cfg = DependenciesFile.FromSource(config)
    let resolved = ResolveWithGraph(cfg,noSha1, VersionsFromGraphAsSeq graph, PackageDetailsFromGraph graph).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    getVersion resolved.[PackageName "delphi-tf-latest-convert"] |> shouldEqual "0.0.75"
