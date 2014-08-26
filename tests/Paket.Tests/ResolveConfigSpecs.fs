module Paket.ResolveConfigSpecs

open Paket
open Paket.DependencyGraph
open NUnit.Framework
open Paket.ConfigDSL
open FsUnit

let config1 = """
source "http://nuget.org/api/v2"

printfn "hello world from config"

nuget "Castle.Windsor-log4net" "~> 3.2"
nuget "Rx-Main" "~> 2.0"
"""

let graph = [
    "Castle.Windsor-log4net","3.2",[]
    "Rx-Main","2.0",[]
]

let discovery = DictionaryDiscovery graph

[<Test>]
let ``should resolve simple config1``() = 
    let cfg = FromCode config1
    let resolved = cfg.Resolve(discovery)
    resolved.["Rx-Main"] |> shouldEqual "2.0"
    resolved.["Castle.Windsor-log4net"] |> shouldEqual "3.2"
