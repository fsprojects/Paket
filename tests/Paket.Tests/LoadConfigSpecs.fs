module Paket.LoadConfigSpecs

open Paket
open Paket.DependencyGraph
open NUnit.Framework
open Paket.ConfigDSL
open FsUnit

let config1 = """
source "http://nuget.org/api/v2"

nuget "Castle.Windsor-log4net" "~> 3.2"
nuget "Rx-Main" "~> 2.0"
nuget "FAKE" "= 1.1"
nuget "SignalR" "= 3.3.2"
"""

[<Test>]
let ``should read simple config1``() = 
    let cfg = FromCode "src1" config1
    cfg.["Rx-Main"].Version |> shouldEqual (VersionRange.Between("2.0", "3.0"))
    cfg.["Rx-Main"].Source |> shouldEqual "src1"
    cfg.["Castle.Windsor-log4net"].Version |> shouldEqual (VersionRange.Between("3.2", "4.0"))
    cfg.["FAKE"].Version |> shouldEqual (VersionRange.Exactly "1.1")
    cfg.["SignalR"].Version |> shouldEqual (VersionRange.Exactly "3.3.2")

let config2 = """
source "http://nuget.org/api/v2"

nuget "FAKE" "~> 3.0"
nuget "Rx-Main" "~> 2.2"
nuget "MinPackage" "1.1.3"
"""

[<Test>]
let ``should read simple config2``() = 
    let cfg = FromCode "src2" config2
    cfg.["Rx-Main"].Version |> shouldEqual (VersionRange.Between("2.2", "3.0"))
    cfg.["Rx-Main"].Source |> shouldEqual "src2"
    cfg.["FAKE"].Version |> shouldEqual (VersionRange.Between("3.0", "4.0"))
    cfg.["MinPackage"].Version |> shouldEqual (VersionRange.AtLeast "1.1.3")
