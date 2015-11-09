module StrategySpecs

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers
open Paket.Domain
open Paket.PackageResolver

let resolve graph updateMode (cfg : DependenciesFile) =
    let groups = [Constants.MainDependencyGroup, None ] |> Map.ofSeq
    cfg.Resolve(true,noSha1,VersionsFromGraphAsSeq graph,PackageDetailsFromGraph graph,groups,updateMode).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
    
let graph = [
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

let config10 = """
source http://nuget.org/api/v2

nuget Castle.Windsor @= 3.2.0
nuget Castle.Core-NLog != 3.2.0
"""

[<Test>]
let ``should favor max strategy to resolve strategy override conflicts``() = 
    let resolved =
        DependenciesFile.FromCode(config10)
        |> resolve graph UpdateMode.UpdateAll
    getVersion resolved.[PackageName "Castle.Windsor"] |> shouldEqual "3.2.0"
    getVersion resolved.[PackageName "Castle.Core-NLog"] |> shouldEqual "3.2.0"
    getVersion resolved.[PackageName "Castle.Core"] |> shouldEqual "3.3.1"