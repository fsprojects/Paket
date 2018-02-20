module Paket.IntegrationTests.AddGithubSpecs

open Fake
open System
open NUnit.Framework
open FsUnit
open System
open System.IO
open System.Diagnostics
open Paket
open Paket.Domain

[<Test>]
let ``#3014 paket github add clitool``() =
    let scenario = "i003014-add-github"
    prepare scenario
    paket "github add forki/FsUnit" scenario |> ignore

    let depsFile = DependenciesFile.ReadFromFile(Path.Combine(scenarioTempPath scenario,"paket.dependencies"))
    let requirement = depsFile.GetGroup(Constants.MainDependencyGroup).Packages |> List.exactlyOne
    requirement.Name |> shouldEqual (PackageName "dotnet-fable")
    requirement.VersionRequirement.ToString() |> shouldEqual "1.3.7"
    requirement.Kind |> shouldEqual Paket.Requirements.PackageRequirementKind.DotnetCliTool