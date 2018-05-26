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
#if NETCOREAPP2_0
[<Ignore("use an api of FakeLib (net40) unsupported on .net core")>]
#endif
let ``#2812 Lowercase package names in package cache: old csproj, packages folder enabled``() =
    let scenario = "i002812-old-csproj-storage-default"
    let projectName = "project"
    let packageName = "AutoMapper"
    let packageNameLowercase = packageName.ToLower()
    let workingDir = scenarioTempPath scenario
    let csprojFile = workingDir @@ projectName @@ sprintf "%s.csproj" projectName
    let packagesDir = workingDir @@ "packages" 

    [ packageName; packageNameLowercase ] |> Seq.iter clearPackage
    
    prepareSdk scenario
    directPaket "restore" scenario |> ignore
    isPackageCachedWithOnlyLowercaseNames packageName |> shouldEqual true
    packagesDir
        |> Directory.GetDirectories 
        |> Array.map Path.GetFileName
        |> shouldEqual [| packageName |]
    
    Fake.MSBuildHelper.MSBuildLoggers <- [] //There is a fsharp.core binding redirect issue on the FakeLib.dll logger
    MSBuildRelease workingDir "Build" [ csprojFile ] |> ignore

[<Test>]
let ``#2812 Lowercase package names in package cache: new csproj, packages folder enabled``() =
    let scenario = "i002812-new-csproj-storage-default"
    let projectName = "project"
    let packageName = "AutoMapper"
    let packageNameLowercase = packageName.ToLower()
    let workingDir = scenarioTempPath scenario
    let projectDir = workingDir @@ projectName
    let emptyFeedPath = workingDir @@ "emptyFeed"
    let packagesDir = workingDir @@ "packages" 

    [ packageName; packageNameLowercase ] |> Seq.iter clearPackage
    
    prepareSdk scenario
    directPaket "restore" scenario |> ignore
    isPackageCachedWithOnlyLowercaseNames packageName |> shouldEqual true
    packagesDir
        |> Directory.GetDirectories 
        |> Array.map Path.GetFileName
        |> shouldEqual [| packageName |]
    directDotnet false (sprintf "restore --source \"%s\"" emptyFeedPath) projectDir |> ignore
    directDotnet false "build --no-restore" projectDir |> ignore

[<Test>]
let ``#2812 Lowercase package names in package cache: new csproj, packages folder disabled``() =
    let scenario = "i002812-new-csproj-storage-default"
    let projectName = "project"
    let packageName = "AutoMapper"
    let packageNameLowercase = packageName.ToLower()
    let workingDir = scenarioTempPath scenario
    let projectDir = workingDir @@ projectName
    let emptyFeedPath = workingDir @@ "emptyFeed"

    [ packageName; packageNameLowercase ] |> Seq.iter clearPackage
    
    prepareSdk scenario
    directPaket "restore" scenario |> ignore
    isPackageCachedWithOnlyLowercaseNames packageName |> shouldEqual true
    directDotnet false (sprintf "restore --source \"%s\"" emptyFeedPath) projectDir |> ignore
    directDotnet false "build --no-restore" projectDir |> ignore


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