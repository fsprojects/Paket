module Paket.IntegrationTests.LoadingScriptGenerationTests
open System
open System.IO
open Pri.LongPath
open NUnit.Framework
open Paket.IntegrationTests.TestHelpers
open Paket

let makeScenarioPath scenario    = Path.Combine("loading-scripts", scenario)
let paket command scenario       = paket command (makeScenarioPath scenario)
let directPaket command scenario = directPaket command (makeScenarioPath scenario)
let scenarioTempPath scenario    = scenarioTempPath (makeScenarioPath scenario)
let scriptRoot scenario = DirectoryInfo(Path.Combine(scenarioTempPath scenario, ".paket", "load"))

let getLoadScriptDefaultFolder scenario = DirectoryInfo((scriptRoot scenario).FullName)
let getLoadScriptFolder framework scenario = DirectoryInfo(Path.Combine((getLoadScriptDefaultFolder scenario).FullName, framework |> FrameworkDetection.Extract |> Option.get |> string))

let getGeneratedScriptFiles framework scenario =
    let frameworkDir = getLoadScriptFolder framework scenario
    frameworkDir.GetFiles() |> Array.sortBy (fun f -> 
        printfn "%s" f.FullName
        f.FullName)

let getGeneratedScriptFilesDefaultFolder scenario =
    let frameworkDir = getLoadScriptDefaultFolder scenario
    frameworkDir.GetFiles() |> Array.sortBy (fun f -> 
        printfn "%s" f.FullName
        f.FullName)

let getScriptContentsFailedExpectations (scriptFolder: DirectoryInfo) (expectations: #seq<string * #seq<string>>) =
    let files =
        scriptFolder.GetFiles()
        |> Seq.map (fun f -> 
            f.Name.ToLower(), f
        ) |> dict

    seq {
        for (file, contains) in expectations do
            match files.TryGetValue file with
            | false, _ -> yield sprintf "file %s was not found" file
            | true, file -> 
                let text = (file.FullName |> File.ReadAllText).ToLower()
                for expectedText in contains do
                    let expect = expectedText.ToLower()
                    if not (text.Contains expect) then
                        yield sprintf "file %s didn't contain %s" file.FullName expectedText
    }


[<Test; Category("scriptgen")>]
let ``simple dependencies generates expected scripts``() = 
    let scenario = "simple-dependencies"
    let framework = "net4"
    paket "install" scenario |> ignore

    directPaket (sprintf "generate-load-scripts framework %s" framework) scenario |> ignore
  
    let files = getGeneratedScriptFiles framework scenario
    
    let actualFiles = 
        (files |> Array.map (fun f -> f.Name) |> Array.sortBy id |> Array.map (fun x -> x.ToLower()))
        |> Set.ofArray

    let expectedFiles = 
        [   "argu.csx"
            "argu.fsx"
            "log4net.csx"
            "log4net.fsx"
            "main.group.csx"
            "main.group.fsx"
            "nunit.csx"
            "nunit.fsx"
        ] |> Set.ofList
  
    if not isMonoRuntime then // TODO: Fix me
        Assert.AreEqual(expectedFiles,actualFiles)


let assertNhibernateForFramework35IsThere scenario =
    let expectations = [
        "iesi.collections.csx", ["Net35/Iesi.Collections.dll"]
        "iesi.collections.fsx", ["Net35/Iesi.Collections.dll"]
        "nhibernate.csx", ["Net35/NHibernate.dll";"#load \"iesi.collections.csx\""]
        "nhibernate.fsx", ["Net35/NHibernate.dll";"#load @\"iesi.collections.fsx\""]
    ]
    let folder = getLoadScriptDefaultFolder scenario
    let failures = getScriptContentsFailedExpectations folder expectations

    if not (Seq.isEmpty failures) then
        Assert.Fail (failures |> String.concat Environment.NewLine)


[<Test;Category("scriptgen")>]
let ``framework specified``() = 
    let scenario = "framework-specified"
    paket "install" scenario |> ignore

    directPaket "generate-load-scripts" scenario |> ignore

    assertNhibernateForFramework35IsThere scenario


[<Test; Category("scriptgen"); Ignore("group script is always generated")>]
let ``don't generate scripts when no references are found``() = 
    (* The deps file for this scenario just includes FAKE, which has no lib or framework references, so no script should be generated for it. *)
    let scenario = "no-references"
    paket "install" scenario |> ignore

    directPaket "generate-load-scripts" scenario |> ignore
    let scriptRootDir = scriptRoot scenario
    Assert.IsFalse(scriptRootDir.Exists)


[<TestCase("csx");TestCase("fsx")>]
[<Test;Category("scriptgen")>]
let ``only generates scripts for language provided`` (language : string) = 
    let scenario = "single-file-type"
    paket "install" scenario |> ignore

    directPaket (sprintf "generate-load-scripts type %s" language) scenario |> ignore

    let scriptRootDir = scriptRoot scenario
    let scriptFiles = scriptRootDir.GetFiles("", SearchOption.AllDirectories)
    let allMatching = scriptFiles |> Array.map (fun fi -> fi.Extension) |> Array.forall ((=) language)
    Assert.IsTrue(allMatching)
     

[<Test; Category("scriptgen")>]
let ``fails on wrong framework given`` () =
    let scenario = "wrong-args"

    paket "install" scenario |> ignore

    let failure = Assert.Throws (fun () ->
        let result = directPaket "generate-load-scripts framework foo framework bar framework net45" scenario
        printf "%s" result
    )
    let message = failure.ToString()
    printfn "%s" message
    Assert.IsTrue(message.Contains "Cannot generate include scripts.")
    Assert.IsTrue(message.Contains "Unrecognized Framework(s)")
    Assert.IsTrue(message.Contains "foo, bar")


[<Test; Category("scriptgen")>]
let ``fails on wrong scripttype given`` () =
    let scenario = "wrong-args"

    paket "install" scenario |> ignore

    let failure = Assert.Throws (fun () ->
        let result = directPaket (sprintf "generate-load-scripts type foo type bar framework net45") scenario
        printf "%s" result
    )
    let message = failure.ToString()
    printfn "%s" message

    // This is handled at the parser level.
    Assert.IsTrue(message.Contains "parameter 'type' must be followed by <csx|fsx>, but was 'foo'")


[<Test; Category("scriptgen")>]
let ``issue 1676 casing`` () =
    let scenario = "issue-1676"
    paket "install" scenario |> ignore

    directPaket "generate-load-scripts framework net46" scenario |> ignore

    let expectations = [
        "entityframework.csx", [
            "../../../packages/EntityFramework/lib/net45/EntityFramework.dll"
            "../../../packages/EntityFramework/lib/net45/EntityFramework.SqlServer.dll"
            ]
        "entityframework.fsx", [
            "../../../packages/EntityFramework/lib/net45/EntityFramework.dll"
            "../../../packages/EntityFramework/lib/net45/EntityFramework.SqlServer.dll"
            ]
    ]
    let folder = getLoadScriptFolder "net46" scenario
    printfn "folder - %s" folder.FullName
    let failures = getScriptContentsFailedExpectations folder expectations

    if not (Seq.isEmpty failures) then
        Assert.Fail (failures |> String.concat Environment.NewLine)


[<Test; Category("scriptgen")>]
let ``mscorlib excluded from f# script`` () =
    let scenario = "mscorlib"
    paket "install" scenario |> ignore

    directPaket "generate-load-scripts framework net46" scenario |> ignore

    let scriptRootDir = scriptRoot scenario
    let hasFilesWithMsCorlib =
        scriptRootDir.GetFiles("*.fsx", SearchOption.AllDirectories) 
        |> Seq.exists (fun f -> 
            f.FullName 
            |> File.ReadAllText 
            |> String.containsIgnoreCase "mscorlib"
        )

    Assert.False hasFilesWithMsCorlib


[<Test; Category("scriptgen")>]
let ``fsharp.core excluded from f# script`` () =
    let scenario = "fsharpcore"
    paket "install" scenario |> ignore

    directPaket "generate-load-scripts framework net46" scenario |> ignore

    let scriptRootDir = scriptRoot scenario
    let hasFilesWithFsharpCore =
        scriptRootDir.GetFiles("*.fsx", SearchOption.AllDirectories) 
        |> Seq.exists (fun f -> 
            f.FullName 
            |> File.ReadAllText 
            |> String.containsIgnoreCase "fsharp.core.dll"
        )

    Assert.False hasFilesWithFsharpCore

[<Test; Category("scriptgen dependencies")>]
let ``generates script on install`` () =
    let scenario = "dependencies-file-flag"
    paket "install" scenario |> ignore

    assertNhibernateForFramework35IsThere scenario

[<Test; Category("scriptgen dependencies")>]
let ``issue 2156 netstandard`` () =
    let scenario = "issue-2156"
    paket "install" scenario |> ignore
    directPaket "generate-load-scripts" scenario |> ignore
    // note: no assert for now, I don't know what we are exactly expecting