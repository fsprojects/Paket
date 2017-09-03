module Paket.IntegrationTests.RestoreSpec

open System
open System.IO
open Fake
open NUnit.Framework
open FsUnit

[<Test>]
let ``#2496 Paket fails on projects that target multiple frameworks``() = 
    let project = "EmptyTarget"
    let scenario = "i002496"
    prepareSdk scenario

    let wd = (scenarioTempPath scenario) @@ project
    directDotnet true (sprintf "restore %s.csproj" project) wd
        |> ignore
        
    
[<Test>]
let ``#2642 dotnet restore writes paket references file to correct obj dir``() = 
    let project = "ObjDir"
    let scenario = "i002642-obj-dir"
    prepareSdk scenario
    
    let wd = (scenarioTempPath scenario) @@ project
    directDotnet true (sprintf "restore %s.csproj" project) wd
        |> ignore

    let originalPath = wd @@ "obj"
    if Directory.Exists originalPath then
        let files = Directory.GetFiles (originalPath, "*", SearchOption.AllDirectories)
        if files.Length > 0 then
            failwithf "Expected no files in obj, but got %A" files
    let modifiedPath = wd @@ "MyCustomFancyObjDir"
    Directory.Exists modifiedPath |> shouldEqual true
    let expectedFiles =
        [ modifiedPath @@ "ObjDir.csproj.NuGet.Config"
          modifiedPath @@ "ObjDir.csproj.references" ]
        |> set
    let actualFiles = Directory.GetFiles (modifiedPath, "*", SearchOption.AllDirectories) |> set
    let missingFiles = expectedFiles - actualFiles
    Assert.AreEqual(Set.empty, missingFiles)