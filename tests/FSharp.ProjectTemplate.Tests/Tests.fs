module FSharp.ProjectScaffold.Tests

open FSharp.ProjectTemplate
open NUnit.Framework
open FSharp.ProjectTemplate.ConfigDSL

let config1 = """
source "http://nuget.org/api/v2"

printfn "hello world from config"

nuget "Castle.Windsor-log4net" "~> 3.2"
nuget "Rx-Main" "~> 2.0"
"""

let shouldEqual (expected:'a) (actual:'a) = Assert.AreEqual(expected,actual)

[<Test>]
let ``should read easy config`` () =
    let cfg = FromCode config1
    cfg.["Rx-Main"].Version |> shouldEqual "~> 2.0"
