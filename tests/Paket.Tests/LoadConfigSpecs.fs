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
    let cfg = FromCode config1
    cfg.["Rx-Main"].Version |> shouldEqual "~> 2.0"    
    cfg.["Castle.Windsor-log4net"].Version |> shouldEqual "~> 3.2"

let config2 = """
source "http://nuget.org/api/v2"

nuget "FAKE" "~> 3.0"
nuget "Rx-Main" "~> 2.2"
"""

[<Test>]
let ``should read simple config2`` () =
    let cfg = FromCode config2
    cfg.["Rx-Main"].Version |> shouldEqual "~> 2.2"    
    cfg.["FAKE"].Version |> shouldEqual "~> 3.0"