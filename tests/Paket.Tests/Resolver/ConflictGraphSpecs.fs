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
    |> OfSimpleGraph

let defaultPackage = 
    { Name = PackageName ""
      Parent = PackageRequirementSource.DependenciesFile("",0)
      Graph = Set.empty
      Sources = []
      VersionRequirement = VersionRequirement(VersionRange.Exactly "1.0", PreReleaseStatus.No)
      Settings = InstallSettings.Default
      Kind = PackageRequirementKind.Package
      TransitivePrereleases = false
      ResolverStrategyForDirectDependencies = Some ResolverStrategy.Max 
      ResolverStrategyForTransitives = Some ResolverStrategy.Max }

[<Test>]
let ``should analyze graph and report conflict``() = 
    match safeResolve graph [ "A", VersionRange.AtLeast "1.0" ] with
    | Resolution.Ok _ -> failwith "we expected an error"
    | Resolution.Conflict { ResolveStep = step  } ->
        let conflicting = step.OpenRequirements |> Seq.head 
        conflicting.Name |> shouldEqual (PackageName "B")

let graph2 = 
    [ "A", "1.0", 
      [ "B", VersionRequirement(VersionRange.Exactly "1.1",PreReleaseStatus.No)
        "C", VersionRequirement(VersionRange.Exactly "2.4",PreReleaseStatus.No) ]
      "B", "1.1", [ "D", VersionRequirement(VersionRange.Between("1.4", "1.5"),PreReleaseStatus.No) ]
      "C", "2.4", [ "D", VersionRequirement(VersionRange.Between("1.6", "1.7"),PreReleaseStatus.No) ]
      "D", "1.4", []
      "D", "1.6", [] ]
    |> OfSimpleGraph

[<Test>]
let ``should analyze graph2 and report conflict``() = 
    match safeResolve graph2 [ "A", VersionRange.AtLeast "1.0" ] with
    | Resolution.Ok _ -> failwith "we expected an error"
    | Resolution.Conflict { ResolveStep = step } ->
        let conflicting = step.OpenRequirements |> Seq.head 
        conflicting.Name |> shouldEqual (PackageName "B")

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
    |> OfSimpleGraph

[<Test>]
let ``should override graph3 conflict to package C``() = 
    let resolved =
         safeResolve graph3 
            ["A",VersionRange.OverrideAll(SemVer.Parse "1.0")
             "B",VersionRange.OverrideAll(SemVer.Parse "1.0")]

    match resolved with
    | Resolution.Ok _ -> failwith "we expected an error"
    | Resolution.Conflict { ResolveStep = step } ->
        let conflicting = step.OpenRequirements |> Seq.head 
        conflicting.Name 
        |> shouldEqual (PackageName "B")

let configWithServices = """
source https://www.nuget.org/api/v2

nuget Service 1.1.31.2
nuget Service.Contracts 1.1.31.2
"""

let graphWithServices = 
  OfSimpleGraph [
    "Service","1.1.31.2",["Service.Core",VersionRequirement(VersionRange.AtLeast "1.1.31.2",PreReleaseStatus.No)]
    "Service","1.1.47",["Service.Core",VersionRequirement(VersionRange.AtLeast "1.1.47",PreReleaseStatus.No)]
    "Service.Core","1.1.31.2",["Service.Contracts",VersionRequirement(VersionRange.AtLeast "1.1.31.2",PreReleaseStatus.No)]
    "Service.Core","1.1.47",["Service.Contracts",VersionRequirement(VersionRange.AtLeast "1.1.47",PreReleaseStatus.No)]
    "Service.Contracts","1.1.31.2",[]
    "Service.Contracts","1.1.47",[]
  ]

[<Test>]
let ``should resolve simple config with services``() = 
    let cfg = DependenciesFile.FromSource(configWithServices)
    let resolved = ResolveWithGraph(cfg,noSha1,VersionsFromGraphAsSeq graphWithServices, PackageDetailsFromGraph graphWithServices).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    getVersion resolved.[PackageName "Service.Core"] |> shouldEqual "1.1.31.2"
    getVersion resolved.[PackageName "Service.Contracts"] |> shouldEqual "1.1.31.2"
    getVersion resolved.[PackageName "Service"] |> shouldEqual "1.1.31.2"


let configWithServers = """
source https://www.nuget.org/api/v2


nuget My.Company.PackageA.Server prerelease
nuget My.Company.PackageB.Server prerelease
nuget My.Company.PackageC.Server prerelease"""

let graphWithServers =
  OfSimpleGraph [
    "My.Company.PackageA.Server","1.0.0-pre18038",["My.Company.PackageC.Server",VersionRequirement(VersionRange.AtLeast "1.0",PreReleaseStatus.No)]
    "My.Company.PackageB.Server","1.0.0-pre18038",["My.Company.PackageC.Server",VersionRequirement(VersionRange.AtLeast "1.0",PreReleaseStatus.No)]
    "My.Company.PackageC.Server","1.0.0-pre18038",[]
  ]

[<Test>]
let ``should resolve simple config with servers``() = 
    let cfg = DependenciesFile.FromSource(configWithServers)
    let resolved = ResolveWithGraph(cfg,noSha1,VersionsFromGraphAsSeq graphWithServers, PackageDetailsFromGraph graphWithServers).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    getVersion resolved.[PackageName "My.Company.PackageC.Server"] |> shouldEqual "1.0.0-pre18038"

let configWithServersWithRCRequirement = """
source https://www.nuget.org/api/v2


nuget My.Company.PackageA.Server rc
nuget My.Company.PackageB.Server rc
nuget My.Company.PackageC.Server rc"""

[<Test>]
let ``should resolve simple config with servers with RC requirement``() = 
    let cfg = DependenciesFile.FromSource(configWithServersWithRCRequirement)
    try
        ResolveWithGraph(cfg,noSha1,VersionsFromGraphAsSeq graphWithServers, PackageDetailsFromGraph graphWithServers).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
        |> ignore
        failwith "expected exception"
    with
    | exn when exn.Message.Contains " package My.Company.PackageA.Server" -> ()

let configWithServersWithVersionRequirement = """
source https://www.nuget.org/api/v2


nuget My.Company.PackageA.Server > 0.1
nuget My.Company.PackageB.Server > 0.1
nuget My.Company.PackageC.Server > 0.1"""


[<Test>]
let ``should resolve simple config with servers with version requirement``() = 
    let cfg = DependenciesFile.FromSource(configWithServersWithVersionRequirement)
    try
        ResolveWithGraph(cfg,noSha1,VersionsFromGraphAsSeq graphWithServers, PackageDetailsFromGraph graphWithServers).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
        |> ignore
        failwith "expected exception"
    with
    | exn when exn.Message.Contains " package My.Company.PackageA.Server" -> ()


let configWithServersWithoutVersionRequirement = """
source https://www.nuget.org/api/v2


nuget My.Company.PackageA.Server
nuget My.Company.PackageB.Server
nuget My.Company.PackageC.Server"""


[<Test>]
let ``should resolve simple config with servers without version requirement``() = 
    let cfg = DependenciesFile.FromSource(configWithServersWithoutVersionRequirement)
    let resolved = ResolveWithGraph(cfg,noSha1,VersionsFromGraphAsSeq graphWithServers, PackageDetailsFromGraph graphWithServers).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    getVersion resolved.[PackageName "My.Company.PackageC.Server"] |> shouldEqual "1.0.0-pre18038"