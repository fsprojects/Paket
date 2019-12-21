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
    use __ = prepare scenario
    directPaket "github add forki/FsUnit" scenario |> ignore<string>

    let depsFile = DependenciesFile.ReadFromFile(Path.Combine(scenarioTempPath scenario,"paket.dependencies"))
    let requirement = depsFile.GetGroup(Constants.MainDependencyGroup).RemoteFiles |> List.exactlyOne
    requirement.Origin |> shouldEqual ModuleResolver.Origin.GitHubLink
    requirement.Owner |> shouldEqual "forki"
    requirement.Project |> shouldEqual "FsUnit"