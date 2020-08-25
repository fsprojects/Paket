module Paket.Resolver.GlobalKeepLatestPatchStrategySpecs

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers
open Paket.Domain
open Paket.PackageResolver

let resolve graph updateMode (cfg : DependenciesFile) =
    let groups = [Constants.MainDependencyGroup, None ] |> Map.ofSeq
    cfg.Resolve(true,noSha1,VersionsFromGraphAsSeq graph, (fun _ _ -> []),PackageDetailsFromGraph graph,(fun _ _ _ -> None),groups,updateMode).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()

let graph =
  OfSimpleGraph [
    "Nancy.Bootstrappers.Windsor","0.23",["Castle.Windsor",VersionRequirement(VersionRange.AtLeast "3.2.1",PreReleaseStatus.No)]
    "Castle.Windsor","3.2.1",[]
    "Castle.Windsor","3.2.2",[]
    "Castle.Windsor","3.3.0",[]
    "Castle.Windsor","4.1.0",[]
  ]

let config1 = sprintf """
strategy %s
source http://www.nuget.org/api/v2

nuget Nancy.Bootstrappers.Windsor ~> 0.23
"""

[<Test>]
let ``should resolve simple config1 with latest-patch``() =
    let resolved =
        DependenciesFile.FromSource(config1 "latest-patch")
        |> resolve graph UpdateMode.UpdateAll
    getVersion resolved.[PackageName "Castle.Windsor"] |> shouldEqual "3.2.2"
    getVersion resolved.[PackageName "Nancy.Bootstrappers.Windsor"] |> shouldEqual "0.23"


[<Test>]
let ``should resolve simple config1 with latest-minor``() =
    let resolved =
        DependenciesFile.FromSource(config1 "latest-minor")
        |> resolve graph UpdateMode.UpdateAll
    getVersion resolved.[PackageName "Castle.Windsor"] |> shouldEqual "3.3.0"
    getVersion resolved.[PackageName "Nancy.Bootstrappers.Windsor"] |> shouldEqual "0.23"

let config2 = sprintf """
strategy %s
source http://www.nuget.org/api/v2

nuget Castle.Windsor
nuget Nancy.Bootstrappers.Windsor ~> 0.23
"""

[<Test>]
let ``should resolve simple config2 with latest-patch``() =
    let resolved =
        DependenciesFile.FromSource(config2 "latest-patch")
        |> resolve graph UpdateMode.UpdateAll
    getVersion resolved.[PackageName "Castle.Windsor"] |> shouldEqual "3.2.2"
    getVersion resolved.[PackageName "Nancy.Bootstrappers.Windsor"] |> shouldEqual "0.23"


[<Test>]
let ``should resolve simple config2 with latest-minor``() =
    let resolved =
        DependenciesFile.FromSource(config2 "latest-minor")
        |> resolve graph UpdateMode.UpdateAll
    getVersion resolved.[PackageName "Castle.Windsor"] |> shouldEqual "3.3.0"
    getVersion resolved.[PackageName "Nancy.Bootstrappers.Windsor"] |> shouldEqual "0.23"

let config3 = sprintf """
strategy %s
source http://www.nuget.org/api/v2

nuget Nancy.Bootstrappers.Windsor ~> 0.23
nuget Castle.Windsor
"""

[<Test>]
let ``should resolve simple config3 with latest-patch``() =
    let resolved =
        DependenciesFile.FromSource(config3 "latest-patch")
        |> resolve graph UpdateMode.UpdateAll
    getVersion resolved.[PackageName "Castle.Windsor"] |> shouldEqual "3.2.2"
    getVersion resolved.[PackageName "Nancy.Bootstrappers.Windsor"] |> shouldEqual "0.23"

    
[<Test>]
let ``should resolve simple config3 with latest-minor``() =
    let resolved =
        DependenciesFile.FromSource(config3 "latest-minor")
        |> resolve graph UpdateMode.UpdateAll
    getVersion resolved.[PackageName "Castle.Windsor"] |> shouldEqual "3.3.0"
    getVersion resolved.[PackageName "Nancy.Bootstrappers.Windsor"] |> shouldEqual "0.23"
    
let graph2 =
  OfSimpleGraph [
    "Nancy.Bootstrappers.Windsor","0.23",["Castle.Windsor",VersionRequirement(VersionRange.AtLeast "3.2.1",PreReleaseStatus.No)]
    "Castle.Windsor","3.2.0",["Castle.Core",VersionRequirement(VersionRange.AtLeast "3.2.0",PreReleaseStatus.No)]
    "Castle.Windsor","3.2.1",["Castle.Core",VersionRequirement(VersionRange.AtLeast "3.2.0",PreReleaseStatus.No)]
    "Castle.Windsor","3.3.0",["Castle.Core",VersionRequirement(VersionRange.AtLeast "3.3.0",PreReleaseStatus.No)]
    "Castle.Windsor-NLog","3.2.0.1",["Castle.Core-NLog",VersionRequirement(VersionRange.AtLeast "3.2.0",PreReleaseStatus.No)]
    "Castle.Windsor-NLog","3.3.0",["Castle.Core-NLog",VersionRequirement(VersionRange.AtLeast "3.3.0",PreReleaseStatus.No)]
    "Castle.Core-NLog","3.2.0",["Castle.Core",VersionRequirement(VersionRange.AtLeast "3.2.0",PreReleaseStatus.No)]
    "Castle.Core-NLog","3.3.0",["Castle.Core",VersionRequirement(VersionRange.AtLeast "3.3.0",PreReleaseStatus.No)]
    "Castle.Core-NLog","3.3.1",["Castle.Core",VersionRequirement(VersionRange.AtLeast "3.3.1",PreReleaseStatus.No)]
    "Castle.Core","3.2.0",[]
    "Castle.Core","3.2.1",[]
    "Castle.Core","3.2.2",[]
    "Castle.Core","3.3.0",[]
    "Castle.Core","3.3.1",[]
  ]

let config5 = sprintf """
strategy %s
source http://www.nuget.org/api/v2

nuget Nancy.Bootstrappers.Windsor !~> 0.23
"""

[<Test>]
let ``should override global strategy with latest-patch``() =
    let resolved =
        DependenciesFile.FromSource(config5 "latest-patch")
        |> resolve graph2 UpdateMode.UpdateAll
    getVersion resolved.[PackageName "Castle.Windsor"] |> shouldEqual "3.2.1"
    getVersion resolved.[PackageName "Nancy.Bootstrappers.Windsor"] |> shouldEqual "0.23"