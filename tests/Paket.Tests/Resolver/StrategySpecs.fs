module StrategySpecs

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

let config = """
source http://www.nuget.org/api/v2

nuget Castle.Windsor @= 3.2.0
nuget Castle.Core-NLog != 3.2.0
"""

[<Test>]
let ``should favor max strategy to resolve strategy override conflicts``() = 
    let resolved =
        DependenciesFile.FromSource(config)
        |> resolve graph UpdateMode.UpdateAll
    getVersion resolved.[PackageName "Castle.Windsor"] |> shouldEqual "3.2.0"
    getVersion resolved.[PackageName "Castle.Core-NLog"] |> shouldEqual "3.2.0"
    getVersion resolved.[PackageName "Castle.Core"] |> shouldEqual "3.3.1"

let configWithStrategy = """
source http://www.nuget.org/api/v2

nuget Castle.Windsor = 3.2.0 strategy:max
nuget Castle.Core-NLog = 3.2.0 strategy:min
"""

[<Test>]
let ``should favor max strategy to resolve strategy override conflicts (with keywords)``() = 
    let resolved =
        DependenciesFile.FromSource(configWithStrategy)
        |> resolve graph UpdateMode.UpdateAll
    getVersion resolved.[PackageName "Castle.Windsor"] |> shouldEqual "3.2.0"
    getVersion resolved.[PackageName "Castle.Core-NLog"] |> shouldEqual "3.2.0"
    getVersion resolved.[PackageName "Castle.Core"] |> shouldEqual "3.3.1"

let config2 = """
source "http://www.nuget.org/api/v2"

nuget Microsoft.AspNet.Mvc >= 6.0.0 lowest_matching: true
"""

let graph2 = 
    OfSimpleGraph [
        "Microsoft.AspNet.Mvc","6.0.0",[]
        "Microsoft.AspNet.Mvc","6.0.1",[]
        "Microsoft.AspNet.Mvc","6.0.13",[]
        "Microsoft.AspNet.Mvc","5.2.3",[]
    ]

[<Test>]
let ``should resolve config with min requirement``() = 
    let cfg = DependenciesFile.FromSource(config2)
    let resolved = ResolveWithGraph(cfg,noSha1,VersionsFromGraphAsSeq graph2, PackageDetailsFromGraph graph2).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    getVersion resolved.[PackageName "Microsoft.AspNet.Mvc"] |> shouldEqual "6.0.0"

let config3 = """
source http://www.nuget.org/api/v2
lowest_matching: true

nuget Microsoft.AspNet.Mvc >= 6.0.0 
"""

[<Test>]
let ``should resolve config with global min requirement``() = 
    let cfg = DependenciesFile.FromSource(config3)
    let resolved = ResolveWithGraph(cfg,noSha1,VersionsFromGraphAsSeq graph2, PackageDetailsFromGraph graph2).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    getVersion resolved.[PackageName "Microsoft.AspNet.Mvc"] |> shouldEqual "6.0.0"

let config4 = """
source http://www.nuget.org/api/v2
lowest_matching: true

nuget Microsoft.AspNet.Mvc >= 6.0.0  lowest_matching:false
"""

[<Test>]
let ``should resolve config with local max requirement``() = 
    let cfg = DependenciesFile.FromSource(config4)
    let resolved = ResolveWithGraph(cfg,noSha1,VersionsFromGraphAsSeq graph2, PackageDetailsFromGraph graph2).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    getVersion resolved.[PackageName "Microsoft.AspNet.Mvc"] |> shouldEqual "6.0.13"