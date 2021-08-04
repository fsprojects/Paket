module Paket.IntegrationTests.LoadingScriptGenerationTests
open System
open System.IO
open NUnit.Framework
open Paket.IntegrationTests.TestHelpers
open Paket

#nowarn "0044" //  Warning FS0044 This construct is deprecated. Use PlatformMatching.extractPlatforms instead

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

type ExpectationType =
    | ShouldContain
    | ShouldNotContain

let getScriptContentsFailedExpectations (scriptFolder: DirectoryInfo) (expectations: #seq<string * #seq<string>>) expectationType =
    let files =
        scriptFolder.GetFiles()
        |> Seq.map (fun f -> 
            f.Name.ToLower(), f
        ) |> dict

    seq {
        for file, contains in expectations do
            match files.TryGetValue file with
            | false, _ -> yield sprintf "file %s was not found" file
            | true, file -> 
                let text = (file.FullName |> File.ReadAllText).ToLower()
                for expectedText in contains do
                    let expect = expectedText.ToLower()
                    match expectationType with
                    | ShouldContain -> 
                        if not (text.Contains expect) then
                            yield sprintf "file %s didn't contain %s" file.Name expectedText
                    | ShouldNotContain ->
                        if text.Contains expect then
                            yield sprintf "file %s contains %s but it shouldn't" file.Name expectedText
    }

let assertExpectations scenario expectationType expectations =
    let folder = getLoadScriptDefaultFolder scenario
    let failures = getScriptContentsFailedExpectations folder expectations expectationType

    if not (Seq.isEmpty failures) then
        Assert.Fail (failures |> String.concat Environment.NewLine)


[<Test; Category("scriptgen")>]
let ``simple dependencies generates expected scripts``() = 
    let scenario = "simple-dependencies"
    let framework = "net4"
    use __ = paket "install" scenario |> fst

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


[<Test; Category("scriptgen")>]
let ``fslab generates expected load of package loader script``() = 
    let scenario = "fslab"
    let framework = "net4"
    use __ = paket "install" scenario |> fst

    directPaket (sprintf "generate-load-scripts framework %s" framework) scenario |> ignore
  
    let files = getGeneratedScriptFiles framework scenario

    let fslabFsxOpt = files |> Array.tryFind (fun p -> p.Name = "FsLab.fsx")

    Assert.True(fslabFsxOpt.IsSome)

    let lines = File.ReadAllLines(fslabFsxOpt.Value.FullName) 
    Assert.True(lines |> Seq.exists (fun s -> s.StartsWith("#load \"../../../packages/FsLab/FsLab.fsx\"")))


let nHibernate35Expectations =
    [
        "iesi.collections.csx", ["Net35/Iesi.Collections.dll"]
        "iesi.collections.fsx", ["Net35/Iesi.Collections.dll"]
        "nhibernate.csx", ["Net35/NHibernate.dll";"#load \"iesi.collections.csx\""]
        "nhibernate.fsx", ["Net35/NHibernate.dll";"#load \"iesi.collections.fsx\""]
    ]

[<Test;Category("scriptgen")>]
let ``framework specified``() = 
    let scenario = "framework-specified"
    use __ = paket "install" scenario |> fst

    directPaket "generate-load-scripts" scenario |> ignore<string>
    
    nHibernate35Expectations |> assertExpectations scenario ExpectationType.ShouldContain


[<Test; Category("scriptgen"); Ignore("group script is always generated")>]
let ``don't generate scripts when no references are found``() = 
    (* The deps file for this scenario just includes FAKE, which has no lib or framework references, so no script should be generated for it. *)
    let scenario = "no-references"
    use __ = paket "install" scenario |> fst

    directPaket "generate-load-scripts" scenario |> ignore<string>
    let scriptRootDir = scriptRoot scenario
    Assert.IsFalse(scriptRootDir.Exists)


[<Test; Category("scriptgen")>]
let ``fails on wrong framework given`` () =
    let scenario = "wrong-args"

    use __ = paket "install" scenario |> fst

    let failure = Assert.Throws<ProcessFailedWithExitCode> (fun () ->
        let result = directPaket "generate-load-scripts framework foo framework bar framework net45" scenario
        printf "%s" result
    )
    let message = failure.ToString()
    printfn "%s" message
    Assert.IsTrue(message.Contains "Can't generate load scripts.")
    Assert.IsTrue(message.Contains "Unrecognized Framework(s)")
    Assert.IsTrue(message.Contains "foo, bar")


[<Test; Category("scriptgen")>]
let ``fails on wrong scripttype given`` () =
    let scenario = "wrong-args"

    use __ = paket "install" scenario |> fst

    let failure = Assert.Throws<ProcessFailedWithExitCode> (fun () ->
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
    use __ = paket "install" scenario |> fst

    directPaket "generate-load-scripts framework net46" scenario |> ignore<string>

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
    let failures = getScriptContentsFailedExpectations folder expectations ExpectationType.ShouldContain

    if not (Seq.isEmpty failures) then
        Assert.Fail (failures |> String.concat Environment.NewLine)


[<Test; Category("scriptgen")>]
let ``mscorlib excluded from f# script`` () =
    let scenario = "mscorlib"
    use __ = paket "install" scenario |> fst

    directPaket "generate-load-scripts framework net46" scenario |> ignore<string>

    let scriptRootDir = scriptRoot scenario
    let hasFilesWithMsCorlib =
        scriptRootDir.GetFiles("*.fsx", SearchOption.AllDirectories) 
        |> Seq.exists (fun f -> 
            f.FullName 
            |> File.ReadAllText 
            |> String.containsIgnoreCase "mscorlib"
        )

    Assert.False hasFilesWithMsCorlib

    // Important to have the correct relative path
    let expectedContent = "#load \"Microsoft.Rest.ClientRuntime.Azure.fsx\""
    let scriptPath = Path.Combine(scriptRootDir.FullName, "net46", "Microsoft.Azure.Management.ResourceManager.fsx")
    let scriptContent = File.ReadAllText(scriptPath)
    Assert.IsTrue(scriptContent.Contains expectedContent, sprintf "Should contain '%s' but script content was:\n%s" expectedContent scriptContent)

[<Test; Category("scriptgen")>]
let ``fsharp.core excluded from f# script`` () =
    let scenario = "fsharpcore"
    use __ = paket "install" scenario |> fst

    directPaket "generate-load-scripts framework net46" scenario |> ignore<string>

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
    use __ = paket "install" scenario |> fst
    
    nHibernate35Expectations |> assertExpectations scenario ExpectationType.ShouldContain

[<Test; Category("scriptgen dependencies")>]
let ``issue 2156 netstandard`` () =
    let scenario = "issue-2156"
    use __ = paket "install" scenario |> fst
    directPaket "generate-load-scripts" scenario |> ignore<string>
    // note: no assert for now, I don't know what we are exactly expecting

[<Test; Category("scriptgen")>]
let ``don't touch file if contents are same`` () =
    let scenario = "issue-2939"
    use __ = paket "install" scenario |> fst
    let scriptsFolder = scriptRoot scenario
    let newtonsoftScript = Path.Combine(scriptsFolder.FullName, "Newtonsoft.Json.fsx") |> FileInfo
    let modificationDate = newtonsoftScript.LastWriteTimeUtc
    directPaket "install" scenario |> ignore<string>
    newtonsoftScript.Refresh()
    Assert.AreEqual(modificationDate, newtonsoftScript.LastWriteTimeUtc)

    
[<Test; Category("scriptgen")>]
let ``ignore assemblies that are not expected by the specified framework`` () =
    let scenario = "issue-3381"
    paket "install" scenario |> ignore
    
    [
        "nlog.csx", ["Java.Interop"; "Mono.Android"; "System.Xml.Linq"; "System.Net"; "System.Windows"; "System.Windows.Browser"; "Xamarin.iOS" ]
        "nlog.fsx", ["Java.Interop"; "Mono.Android"; "System.Xml.Linq"; "System.Net"; "System.Windows"; "System.Windows.Browser"; "Xamarin.iOS" ]
    ] |> assertExpectations scenario ExpectationType.ShouldNotContain

[<Test; Category("scriptgen")>]
let ``f# scripts should contain the paket namespace`` () =
    let scenario = "add-namespace"
    paket "install" scenario |> ignore
    
    [
        "nlog.fsx", ["namespace PaketLoadScripts" ]
    ] |> assertExpectations scenario ExpectationType.ShouldContain
    
[<Test; Category("scriptgen")>]
let ``c# scripts should not contain the packet namespace`` () =
    let scenario = "add-namespace"
    paket "install" scenario |> ignore
    
    [
        "nlog.csx", ["namespace PaketLoadScripts" ]
    ] |> assertExpectations scenario ExpectationType.ShouldNotContain