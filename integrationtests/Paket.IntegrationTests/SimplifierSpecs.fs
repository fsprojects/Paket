module Paket.IntegrationTests.SimplifierSpecs

open NUnit.Framework
open Fake
open FsUnit
open Paket

[<Test>]
let ``#1737 simplify should handle auto-detected framework``() =
    let scenario = "i001737-simplify-with-auto-framework"
    use __ = prepare scenario
    directPaket "install" scenario |> ignore<string>

    let deps = Paket.Dependencies(scenarioTempPath scenario </> "paket.dependencies")
    deps.Simplify(false)
    
    deps.GetDependenciesFile().Groups.[Paket.Constants.MainDependencyGroup].Packages |> shouldHaveLength 1


