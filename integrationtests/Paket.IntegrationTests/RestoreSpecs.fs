module Paket.IntegrationTests.RestoreSpec

open System
open System.IO
open Fake
open NUnit.Framework
open FsUnit
open Paket
open Paket.Utils

[<Test>]
let ``#2496 Paket fails on projects that target multiple frameworks``() = 
    let project = "EmptyTarget"
    let scenario = "i002496"
    prepareSdk scenario

    let wd = (scenarioTempPath scenario) @@ project
    directDotnet true (sprintf "restore %s.csproj" project) wd
        |> ignore

[<Test>]
let ``#3527 BaseIntermediateOutputPath``() =
    let project = "project"
    let scenario = "i003527"
    prepareSdk scenario

    let wd = (scenarioTempPath scenario) @@ project
    directDotnet true (sprintf "restore %s.fsproj" project) wd
        |> ignore

    let defaultObjDir = DirectoryInfo (Path.Combine (scenarioTempPath scenario, project, "obj"))
    let customObjDir = DirectoryInfo (Path.Combine (scenarioTempPath scenario, project, "obj", "custom"))

    defaultObjDir.GetFiles() |> shouldBeEmpty
    customObjDir.GetFiles().Length |> shouldBeGreaterThan 0

[<Test>]
let ``#3000-a dotnet restore``() =
    let scenario = "i003000-netcoreapp2"
    let projectName = "c1"
    let packageName = "AutoMapper"
    let workingDir = scenarioTempPath scenario
    let projectDir = workingDir @@ projectName

    [ packageName; (packageName.ToLower()) ] |> Seq.iter clearPackage
    
    prepareSdk scenario
    directDotnet false "restore" projectDir |> ignore
    directDotnet false "build --no-restore" projectDir |> ignore

[<Test>]
let ``#3012 Paket restore silently fails when TargetFramework(s) are specified in Directory.Build.props and not csproj`` () =
    let scenario = "i003012"
    let projectName = "dotnet"
    let packageName = "AutoMapper"
    let workingDir = scenarioTempPath scenario
    let projectDir = workingDir @@ projectName

    [ packageName; (packageName.ToLower()) ] |> Seq.iter clearPackage
    
    prepareSdk scenario
    directPaket "install" scenario |> ignore
    directDotnet false "build" projectDir |> ignore