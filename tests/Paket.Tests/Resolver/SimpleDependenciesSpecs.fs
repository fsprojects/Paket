module Paket.Resolver.SimpleDependenciesSpecs

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers
open Paket.Domain

let config1 = """
source "https://nuget.org/api/v2"

nuget "Castle.Windsor-log4net" "~> 3.2"
nuget "Rx-Main" "~> 2.0"
"""

let graph = [
    "Castle.Windsor-log4net","3.2",[]
    "Castle.Windsor-log4net","3.3",["Castle.Windsor",VersionRequirement(VersionRange.AtLeast "2.0",PreReleaseStatus.No);"log4net",VersionRequirement(VersionRange.AtLeast "1.0",PreReleaseStatus.No)]
    "Castle.Windsor","2.0",[]
    "Castle.Windsor","2.1",[]
    "Rx-Main","2.0",["Rx-Core",VersionRequirement(VersionRange.AtLeast "2.1",PreReleaseStatus.No)]
    "Rx-Core","2.0",[]
    "Rx-Core","2.1",[]
    "log4net","1.0",["log",VersionRequirement(VersionRange.AtLeast "1.0",PreReleaseStatus.No)]
    "log4net","1.1",["log",VersionRequirement(VersionRange.AtLeast "1.0",PreReleaseStatus.No)]
    "log","1.0",[]
    "log","1.2",[]
]

[<Test>]
let ``should resolve simple config1``() = 
    let cfg = DependenciesFile.FromCode(config1)
    let resolved = ResolveWithGraph(cfg,noSha1,VersionsFromGraphAsSeq graph, PackageDetailsFromGraph graph).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    getVersion resolved.[PackageName "Rx-Main"] |> shouldEqual "2.0"
    getVersion resolved.[PackageName "Rx-Core"] |> shouldEqual "2.1"
    getVersion resolved.[PackageName "Castle.Windsor-log4net"] |> shouldEqual "3.3"
    getVersion resolved.[PackageName "Castle.Windsor"] |> shouldEqual "2.1"
    getVersion resolved.[PackageName "log4net"] |> shouldEqual "1.1"
    getVersion resolved.[PackageName "log"] |> shouldEqual "1.2"
    getSource resolved.[PackageName "log"] |> shouldEqual PackageSources.DefaultNuGetSource


let config2 = """
source "http://nuget.org/api/v2"

nuget NUnit ~> 2.6
nuget FsUnit ~> 1.3
"""

let graph2 = [
    "FsUnit","1.3.1",["NUnit",DependenciesFileParser.parseVersionRequirement ">= 2.6.3"]
    "NUnit","2.6.2",[]
    "NUnit","2.6.3",[]    
]

[<Test>]
let ``should resolve simple config2``() = 
    let cfg = DependenciesFile.FromCode(config2)
    let resolved = ResolveWithGraph(cfg,noSha1,VersionsFromGraphAsSeq graph2, PackageDetailsFromGraph graph2).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    getVersion resolved.[PackageName "FsUnit"] |> shouldEqual "1.3.1"
    getVersion resolved.[PackageName "NUnit"] |> shouldEqual "2.6.3"

let config3 = """
source "http://nuget.org/api/v2"

nuget "Castle.Core" "= 3.2.0"
nuget "Castle.Windsor-log4net" "= 3.2.0.1"
"""

let graph3 = [
    "Castle.Core","3.2.0",[]
    "Castle.Core","3.3.0",[]
    "Castle.Core","3.3.1",[]
    "Castle.Windsor-log4net","3.2.0.1",["Castle.Core-log4net",DependenciesFileParser.parseVersionRequirement ">= 3.2.0"]
    "Castle.Core-log4net","3.2.0",["Castle.Core",DependenciesFileParser.parseVersionRequirement ">= 3.2.0"]
    "Castle.Core-log4net","3.3.0",["Castle.Core",DependenciesFileParser.parseVersionRequirement ">= 3.3.0"]
    "Castle.Core-log4net","3.3.1",["Castle.Core",DependenciesFileParser.parseVersionRequirement ">= 3.3.1"]
]

[<Test>]
let ``should resolve fixed config``() = 
    let cfg = DependenciesFile.FromCode(config3)
    let resolved = ResolveWithGraph(cfg,noSha1,VersionsFromGraphAsSeq graph3, PackageDetailsFromGraph graph3).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    getVersion resolved.[PackageName "Castle.Core"] |> shouldEqual "3.2.0"
    getVersion resolved.[PackageName "Castle.Windsor-log4net"] |> shouldEqual "3.2.0.1"
    getVersion resolved.[PackageName "Castle.Core-log4net"] |> shouldEqual "3.2.0"


let config4 = """
source "http://nuget.org/api/v2"

nuget "Castle.Core" "= 3.2.0"
nuget "Castle.Windsor-log4net" "~> 3.2"
"""

[<Test>]
let ``should resolve fixed config4``() = 
    let cfg = DependenciesFile.FromCode(config4)
    let resolved = ResolveWithGraph(cfg,noSha1,VersionsFromGraphAsSeq graph3, PackageDetailsFromGraph graph3).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    getVersion resolved.[PackageName "Castle.Core"] |> shouldEqual "3.2.0"
    getVersion resolved.[PackageName "Castle.Windsor-log4net"] |> shouldEqual "3.2.0.1"
    getVersion resolved.[PackageName "Castle.Core-log4net"] |> shouldEqual "3.2.0"

let config5 = """
source "http://nuget.org/api/v2"

nuget Microsoft.AspNet.Mvc >= 6.0.0 prerelease
"""

let graph5 = [
    "Microsoft.AspNet.Mvc","6.0.0-beta6",[]
    "Microsoft.AspNet.Mvc","6.0.0-beta1",[]
    "Microsoft.AspNet.Mvc","5.2.3",[]
]

[<Test>]
let ``should resolve prerelease config``() = 
    let cfg = DependenciesFile.FromCode(config5)
    let resolved = ResolveWithGraph(cfg,noSha1,VersionsFromGraphAsSeq graph5, PackageDetailsFromGraph graph5).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    getVersion resolved.[PackageName "Microsoft.AspNet.Mvc"] |> shouldEqual "6.0.0-beta6"