module Paket.ResolveConfigSpecs

open Paket
open NUnit.Framework
open FsUnit

let config1 = """
source "http://nuget.org/api/v2"

nuget "Castle.Windsor-log4net" "~> 3.2"
nuget "Rx-Main" "~> 2.0"
"""

let graph = [
    "Castle.Windsor-log4net","3.2",[]
    "Castle.Windsor-log4net","3.3",["Castle.Windsor",VersionRange.AtLeast "2.0";"log4net",VersionRange.AtLeast "1.0"]
    "Castle.Windsor","2.0",[]
    "Castle.Windsor","2.1",[]
    "Rx-Main","2.0",["Rx-Core",VersionRange.AtLeast "2.1"]
    "Rx-Core","2.0",[]
    "Rx-Core","2.1",[]
    "log4net","1.0",["log",VersionRange.AtLeast "1.0"]
    "log4net","1.1",["log",VersionRange.AtLeast "1.0"]
    "log","1.0",[]
    "log","1.2",[]
]

let discovery = Discovery.DictionaryDiscovery graph

[<Test>]
let ``should resolve simple config1``() = 
    let cfg = Config.FromCode config1
    let resolved = cfg.Resolve(discovery)
    resolved.["Rx-Main"] |> shouldEqual (ResolvedVersion.Resolved "2.0")
    resolved.["Rx-Core"] |> shouldEqual (ResolvedVersion.Resolved "2.1")
    resolved.["Castle.Windsor-log4net"] |> shouldEqual (ResolvedVersion.Resolved "3.3")
    resolved.["Castle.Windsor"] |> shouldEqual (ResolvedVersion.Resolved "2.1")
    resolved.["log4net"] |> shouldEqual (ResolvedVersion.Resolved "1.1")
    resolved.["log"] |> shouldEqual (ResolvedVersion.Resolved "1.2")
