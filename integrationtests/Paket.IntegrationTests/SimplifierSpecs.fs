module Paket.IntegrationTests.SimplifierSpecs

open NUnit.Framework
open Fake
open FsUnit

[<Test>]
let ``#1737 simplify should handle auto-detected framework``() =
    let scenario = "i001737-simplify-with-auto-framework"
    prepare scenario
    paket "install" scenario |> ignore

    let deps = Paket.Dependencies(scenarioTempPath scenario </> "paket.dependencies")
    deps.Simplify(false)
    
    deps.GetDependenciesFile().Groups.[Paket.Constants.MainDependencyGroup].Packages |> shouldHaveLength 1


