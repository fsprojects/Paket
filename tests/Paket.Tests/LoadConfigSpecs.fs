module Paket.LoadConfigSpecs

open Paket
open NUnit.Framework
open Paket.ConfigDSL
open FsUnit

let config1 = """
source "http://nuget.org/api/v2"

nuget "Castle.Windsor-log4net" "~> 3.2"
nuget "Rx-Main" "~> 2.0"
"""

[<Test>]
let ``should read simple config1`` () =
    let cfg = FromCode "src1" config1
    cfg.["Rx-Main"].Version.Min |> shouldEqual "2.0"    
    cfg.["Rx-Main"].Version.Max |> shouldEqual "3.0"    
    cfg.["Rx-Main"].Source |> shouldEqual "src1"    
    cfg.["Castle.Windsor-log4net"].Version.Min |> shouldEqual "3.2"
    cfg.["Castle.Windsor-log4net"].Version.Max |> shouldEqual "4.0"

let config2 = """
source "http://nuget.org/api/v2"

nuget "FAKE" "~> 3.0"
nuget "Rx-Main" "~> 2.2"
"""

[<Test>]
let ``should read simple config2`` () =
    let cfg = FromCode "src2" config2
    cfg.["Rx-Main"].Version.Min |> shouldEqual "2.2"
    cfg.["Rx-Main"].Version.Max |> shouldEqual "3.0"
    cfg.["Rx-Main"].Source |> shouldEqual "src2"    
    cfg.["FAKE"].Version.Min |> shouldEqual "3.0"
    cfg.["FAKE"].Version.Max |> shouldEqual "4.0"