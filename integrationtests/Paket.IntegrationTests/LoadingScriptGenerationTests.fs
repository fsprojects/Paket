module Paket.IntegrationTests.LoadingScriptGenerationTests
open System
open System.IO
open NUnit.Framework
open Paket.IntegrationTests.TestHelpers
open Paket

let makeScenarioPath scenario    = Path.Combine("loading-scripts-scenarios", scenario)
let paket command scenario       = paket command (makeScenarioPath scenario)
let directPaket command scenario = directPaket command (makeScenarioPath scenario)
let scenarioTempPath scenario    = scenarioTempPath (makeScenarioPath scenario)
let scriptRoot scenario = Path.Combine(scenarioTempPath scenario, "paket-files", "include-scripts") |> DirectoryInfo

let getGeneratedScriptFiles framework scenario =
  let frameworkDir = Path.Combine((scriptRoot scenario).FullName, framework |> FrameworkDetection.Extract |> Option.get |> string) |> DirectoryInfo
  frameworkDir.GetFiles()

[<Test; Category("scriptgen")>]
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
  

[<Test;Category("scriptgen")>]
let ``framework specified``() = 
  let scenario = "framework-specified"
  paket "install" scenario |> ignore

  directPaket "generate-include-scripts" scenario |> ignore
  
  let files =
    getGeneratedScriptFiles "net35" scenario
    |> Seq.map (fun f -> f.Name, f)
    |> dict

  let expectations = [
    "include.iesi.collections.csx", ["Net35/Iesi.Collections.dll"]
    "include.iesi.collections.fsx", ["Net35/Iesi.Collections.dll"]
    "include.nhibernate.csx", ["Net35/NHibernate.dll";"#load \"include.iesi.collections.csx\""]
    "include.nhibernate.fsx", ["Net35/NHibernate.dll";"#load @\"include.iesi.collections.fsx\""]
  ]

  let failures = seq {
    for (file, contains) in expectations do
      match files.TryGetValue file with
      | false, _ -> yield sprintf "file %s was not found" file
      | true, file -> 
        let text = file.FullName |> File.ReadAllText
        for expectedText in contains do
          if not (text.Contains expectedText) then
            yield sprintf "file %s didn't contain %s" file.FullName expectedText
  }

  if not (Seq.isEmpty failures) then
    Assert.Fail (failures |> String.concat Environment.NewLine)

[<Test;Category("scriptgen")>]
let ``don't generate scripts when no references are found``() = 
    (* The deps file for this scenario just includes FAKE, which has no lib or framework references, so no script should be generated for it. *)
    let scenario = "no-references"
    paket "install" scenario |> ignore

    directPaket "generate-include-scripts" scenario |> ignore
    let scriptRootDir = scriptRoot scenario
    Assert.IsFalse(scriptRootDir.Exists)

[<TestCase("csx");TestCase("fsx")>]
[<Test;Category("scriptgen")>]
let ``only generates scripts for language provided`` (language : string) = 
    let scenario = "single-file-type"
    paket "install" scenario |> ignore

    directPaket (sprintf "generate-include-scripts type %s" language) scenario |> ignore

    let scriptRootDir = scriptRoot scenario
    let scriptFiles = scriptRootDir.GetFiles("", SearchOption.AllDirectories)
    let allMatching = scriptFiles |> Array.map (fun fi -> fi.Extension) |> Array.forall ((=) language)
    Assert.IsTrue(allMatching)
     
[<Test; Category("scriptgen")>]
let ``fails on wrong framework given`` () =
    let scenario = "wrong-framework-or-scripttype"

    paket "install" scenario |> ignore

    let failure = Assert.Throws (fun () ->
        let result = directPaket (sprintf "generate-include-scripts framework foo framework bar framework net45") scenario
        printf "%s" result
    )
    let message = failure.ToString()
    printfn "%s" message
    Assert.IsTrue(message.Contains "Cannot generate include scripts.")
    Assert.IsTrue(message.Contains "Unrecognized Framework(s)")
    Assert.IsTrue(message.Contains "foo, bar")

[<Test; Category("scriptgen")>]
let ``fails on wrong scripttype given`` () =
    let scenario = "wrong-framework-or-scripttype"

    paket "install" scenario |> ignore

    let failure = Assert.Throws (fun () ->
        let result = directPaket (sprintf "generate-include-scripts type foo type bar framework net45") scenario
        printf "%s" result
    )
    let message = failure.ToString()
    printfn "%s" message
    Assert.IsTrue(message.Contains "Cannot generate include scripts.")
    Assert.IsTrue(message.Contains "Unrecognized Script Type(s)")
    Assert.IsTrue(message.Contains "foo, bar")
