module Paket.MergeSimpleConfigsSpecs

open Paket
open NUnit.Framework
open Paket.ConfigDSL
open FsUnit

let config1 = """
source "http://nuget.org/api/v2"

nuget "Castle.Windsor-log4net" "~> 3.2"
nuget "Rx-Main" "~> 2.0"
"""

let config2 = """
source "http://nuget.org/api/v2"

nuget "FAKE" "~> 3.0"
nuget "Rx-Main" "~> 2.2"
nuget "Castle.Windsor-log4net" "~> 1.2"
"""

[<Test>]
let ``should merge simple configs`` () =
    let cfg1 = FromCode config1
    let cfg2 = FromCode config2

    let cfg = merge cfg1 cfg2

    cfg.["Rx-Main"].Version |> shouldEqual "~> 2.2"    
    cfg.["Castle.Windsor-log4net"].Version |> shouldEqual "~> 3.2"    
    cfg.["FAKE"].Version |> shouldEqual "~> 3.0"

