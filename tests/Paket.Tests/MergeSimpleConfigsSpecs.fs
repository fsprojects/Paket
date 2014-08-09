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
    let cfg1 = FromCode "src1" config1
    let cfg2 = FromCode "src2" config2

    let cfg = merge cfg1 cfg2

    cfg.["Rx-Main"].Version.Min |> shouldEqual "2.2"    
    cfg.["Rx-Main"].Version.Max |> shouldEqual "3.0"    
    cfg.["Rx-Main"].Source |> shouldEqual "src2"    
    cfg.["Castle.Windsor-log4net"].Version.Min |> shouldEqual "3.2"    
    cfg.["Castle.Windsor-log4net"].Version.Max |> shouldEqual "4.0"    
    cfg.["Castle.Windsor-log4net"].Source |> shouldEqual "src1"    
    cfg.["FAKE"].Version.Min |> shouldEqual "3.0"
    cfg.["FAKE"].Version.Max |> shouldEqual "4.0"

