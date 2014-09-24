module Paket.Resolver.PessimisticStrategySpecs

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers

let graph = [
    "Nancy.Bootstrappers.Windsor","0.23",["Castle.Windsor",VersionRange.AtLeast "3.2.1"]
    "Castle.Windsor","3.2.1",[]
    "Castle.Windsor","3.3.0",[]
]

let config1 = """
source "http://nuget.org/api/v2"

nuget "Nancy.Bootstrappers.Windsor" "!~> 0.23"
"""

[<Test>]
let ``should resolve simple config1``() = 
    let cfg = DependenciesFile.FromCode(noSha1,config1)
    let resolved = cfg.Resolve(VersionsFromGraph graph, PackageDetailsFromGraph graph) |> UpdateProcess.getResolvedPackagesOrFail
    getVersion resolved.["Castle.Windsor"] |> shouldEqual "3.2.1"
    getVersion resolved.["Nancy.Bootstrappers.Windsor"] |> shouldEqual "0.23"

let config2 = """
source "http://nuget.org/api/v2"

nuget "Castle.Windsor" "!>= 0"
nuget "Nancy.Bootstrappers.Windsor" "!~> 0.23"
"""

[<Test>]
let ``should resolve simple config2``() = 
    let cfg = DependenciesFile.FromCode(noSha1,config2)
    let resolved = cfg.Resolve(VersionsFromGraph graph, PackageDetailsFromGraph graph) |> UpdateProcess.getResolvedPackagesOrFail
    getVersion resolved.["Castle.Windsor"] |> shouldEqual "3.2.1"
    getVersion resolved.["Nancy.Bootstrappers.Windsor"] |> shouldEqual "0.23"


let config3 = """
source "http://nuget.org/api/v2"

nuget "Nancy.Bootstrappers.Windsor" "!~> 0.23"
nuget "Castle.Windsor" "!>= 0"
"""

[<Test>]
let ``should resolve simple config3``() = 
    let cfg = DependenciesFile.FromCode(noSha1,config3)
    let resolved = cfg.Resolve(VersionsFromGraph graph, PackageDetailsFromGraph graph) |> UpdateProcess.getResolvedPackagesOrFail
    getVersion resolved.["Castle.Windsor"] |> shouldEqual "3.2.1"
    getVersion resolved.["Nancy.Bootstrappers.Windsor"] |> shouldEqual "0.23"