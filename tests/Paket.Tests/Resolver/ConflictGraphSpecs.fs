module Paket.ConflictGraphSpecs

open Paket
open Paket.Requirements
open Paket.PackageSources
open Paket.PackageResolver
open NUnit.Framework
open FsUnit
open TestHelpers
open Paket.Domain

let graph = 
    [ "A", "1.0", 
      [ "B", VersionRequirement(VersionRange.Exactly "1.1",PreReleaseStatus.No)
        "C", VersionRequirement(VersionRange.Exactly "2.4",PreReleaseStatus.No) ]
      "B", "1.1", 
      [ "E", VersionRequirement(VersionRange.Exactly "4.3",PreReleaseStatus.No)
        "D", VersionRequirement(VersionRange.Exactly "1.4",PreReleaseStatus.No) ]
      "C", "2.4", 
      [ "F", VersionRequirement(VersionRange.Exactly "1.2",PreReleaseStatus.No)
        "D", VersionRequirement(VersionRange.Exactly "1.6",PreReleaseStatus.No) ]
      "D", "1.4", []
      "D", "1.6", []
      "E", "4.3", []
      "F", "1.2", [] ]

let defaultPackage = 
    { Name = PackageName ""
      Parent = PackageRequirementSource.DependenciesFile ""
      Graph = []
      VersionRequirement = VersionRequirement(VersionRange.Exactly "1.0", PreReleaseStatus.No)
      Settings = InstallSettings.Default
      ResolverStrategy = Some ResolverStrategy.Max }

[<Test>]
let ``should analyze graph and report conflict``() = 
    match safeResolve graph [ "A", VersionRange.AtLeast "1.0" ] with
    | Resolution.Ok _ -> failwith "we expected an error"
    | Resolution.Conflict(_,_,stillOpen,_,_) ->
        let conflicting = stillOpen |> Seq.head 
        conflicting.Name |> shouldEqual (PackageName "D")
        conflicting.VersionRequirement.Range |> shouldEqual (VersionRange.Exactly "1.6")

let graph2 = 
    [ "A", "1.0", 
      [ "B", VersionRequirement(VersionRange.Exactly "1.1",PreReleaseStatus.No)
        "C", VersionRequirement(VersionRange.Exactly "2.4",PreReleaseStatus.No) ]
      "B", "1.1", [ "D", VersionRequirement(VersionRange.Between("1.4", "1.5"),PreReleaseStatus.No) ]
      "C", "2.4", [ "D", VersionRequirement(VersionRange.Between("1.6", "1.7"),PreReleaseStatus.No) ]
      "D", "1.4", []
      "D", "1.6", [] ]

[<Test>]
let ``should analyze graph2 and report conflict``() = 
    match safeResolve graph2 [ "A", VersionRange.AtLeast "1.0" ] with
    | Resolution.Ok _ -> failwith "we expected an error"
    | Resolution.Conflict(_,_,stillOpen,_,_) ->
        let conflicting = stillOpen |> Seq.head 
        conflicting.Name |> shouldEqual (PackageName "D")
        conflicting.VersionRequirement.Range |> shouldEqual (VersionRange.Between("1.6", "1.7"))

[<Test>]
let ``should override graph2 conflict to first version``() = 
    let resolved = resolve graph2 ["A",VersionRange.AtLeast "1.0"; "D",VersionRange.OverrideAll(SemVer.Parse "1.4")]
    getVersion resolved.[PackageName "D"] |> shouldEqual "1.4"


[<Test>]
let ``should override graph2 conflict to second version``() = 
    let resolved = resolve graph2 ["A",VersionRange.AtLeast "1.0"; "D",VersionRange.OverrideAll(SemVer.Parse "1.6")]
    getVersion resolved.[PackageName "D"] |> shouldEqual "1.6"

let graph3 = 
    [ "A", "1.0", 
        [ "B", VersionRequirement(VersionRange.Exactly "1.1",PreReleaseStatus.No)
          "C", VersionRequirement(VersionRange.Exactly "1.0",PreReleaseStatus.No) ]
      "A", "1.1", []
      "B", "1.0", 
        [ "A", VersionRequirement(VersionRange.Exactly "1.1",PreReleaseStatus.No)
          "C", VersionRequirement(VersionRange.Exactly "2.0",PreReleaseStatus.No) ]
      "B", "1.1", []
      "C", "1.0", []
      "C", "2.0", [] ]

[<Test>]
let ``should override graph3 conflict to package C``() = 
    let resolved =
         safeResolve graph3 
            ["A",VersionRange.OverrideAll(SemVer.Parse "1.0")
             "B",VersionRange.OverrideAll(SemVer.Parse "1.0")]

    match resolved with
    | Resolution.Ok _ -> failwith "we expected an error"
    | Resolution.Conflict(_,_,stillOpen,_,_) ->
        let conflicting = stillOpen |> Seq.head 
        conflicting.Name 
        |> shouldEqual (PackageName "C")

let configWithServices = """
source https://nuget.org/api/v2

nuget Service 1.1.31.2
nuget Service.Contracts 1.1.31.2
"""

let graphWithServices = [
    "Service","1.1.31.2",["Service.Core",VersionRequirement(VersionRange.AtLeast "1.1.31.2",PreReleaseStatus.No)]
    "Service","1.1.47",["Service.Core",VersionRequirement(VersionRange.AtLeast "1.1.47",PreReleaseStatus.No)]
    "Service.Core","1.1.31.2",["Service.Contracts",VersionRequirement(VersionRange.AtLeast "1.1.31.2",PreReleaseStatus.No)]
    "Service.Core","1.1.47",["Service.Contracts",VersionRequirement(VersionRange.AtLeast "1.1.47",PreReleaseStatus.No)]
    "Service.Contracts","1.1.31.2",[]
    "Service.Contracts","1.1.47",[]
]

[<Test>]
let ``should resolve simple config with services``() = 
    let cfg = DependenciesFile.FromCode(configWithServices)
    let resolved = ResolveWithGraph(cfg,noSha1,VersionsFromGraphAsSeq graphWithServices, PackageDetailsFromGraph graphWithServices).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    getVersion resolved.[PackageName "Service.Core"] |> shouldEqual "1.1.31.2"
    getVersion resolved.[PackageName "Service.Contracts"] |> shouldEqual "1.1.31.2"
    getVersion resolved.[PackageName "Service"] |> shouldEqual "1.1.31.2"