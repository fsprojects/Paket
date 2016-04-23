module Paket.IntegrationTests.LoadingScriptGenerationTests
open NUnit.Framework
open Paket.IntegrationTests.TestHelpers
open System.IO
open Paket

let makeScenarioPath scenario    = Path.Combine("loading-scripts-scenarios", scenario)
let paket command scenario       = paket command (makeScenarioPath scenario)
let directPaket command scenario = directPaket command (makeScenarioPath scenario)
let scenarioTempPath scenario    = scenarioTempPath (makeScenarioPath scenario)

let getGeneratedScriptFiles framework scenario =
  let directory =
      Path.Combine(scenarioTempPath scenario, "paket-files", "include-scripts", (FrameworkDetection.Extract framework).Value |> string)
      |> DirectoryInfo
  
  directory.GetFiles()

[<Test>]
let ``simple dependencies generates expected scripts``() = 
  let scenario = "simple-dependencies"
  let framework = "net4"
  paket "install" scenario |> ignore


  directPaket (sprintf "generate-include-scripts framework %s" framework) scenario |> ignore
  
  let files = getGeneratedScriptFiles framework scenario
  let actualFiles = files |> Array.map (fun f -> f.Name)
  let expectedFiles = [|
      "include.argu.csx"
      "include.argu.fsx"
      "include.log4net.csx"
      "include.log4net.fsx"
      "include.nunit.csx"
      "include.nunit.fsx"
  |]
  Assert.AreEqual(expectedFiles, actualFiles)
  
