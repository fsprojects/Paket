module Paket.IntegrationTests.LoadingScriptGenerationTests
open NUnit.Framework
open Paket.IntegrationTests.TestHelpers
open System.IO

let paket command scenario =
  paket command (Path.Combine("loading-scripts-scenarios", scenario))

[<Test>]
let ``simple dependencies generates expected scripts``() = 
  paket "install" "simple-dependencies" |> ignore
  // todo
  ()
  
