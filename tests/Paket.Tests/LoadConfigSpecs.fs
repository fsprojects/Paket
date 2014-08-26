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
    let cfg = FromCode config1
    cfg.Dependencies.["Rx-Main"] |> shouldEqual (VersionRange.Between("2.0", "3.0"))
    cfg.Dependencies.["Castle.Windsor-log4net"] |> shouldEqual (VersionRange.Between("3.2", "4.0"))
    cfg.Dependencies.["FAKE"] |> shouldEqual (VersionRange.Exactly "1.1")
    cfg.Dependencies.["SignalR"] |> shouldEqual (VersionRange.Exactly "3.3.2")

let config2 = """
source "http://nuget.org/api/v2"

nuget "FAKE" "~> 3.0"
nuget "Rx-Main" "~> 2.2"
nuget "MinPackage" "1.1.3"
"""

[<Test>]
let ``should read simple config2``() = 
    let cfg = FromCode config2
    cfg.Dependencies.["Rx-Main"] |> shouldEqual (VersionRange.Between("2.2", "3.0"))
    cfg.Dependencies.["FAKE"] |> shouldEqual (VersionRange.Between("3.0", "4.0"))
    cfg.Dependencies.["MinPackage"] |> shouldEqual (VersionRange.AtLeast "1.1.3")
