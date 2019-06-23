module Paket.IntegrationTests.AddSpecs

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
let ``#320 paket add clitool``() =
    let scenario = "i000320-add-clitool"
    use __ = paket "add dotnet-fable --version 1.3.7 -t clitool --no-resolve" scenario |> fst

    let depsFile = DependenciesFile.ReadFromFile(Path.Combine(scenarioTempPath scenario,"paket.dependencies"))
    let requirement = depsFile.GetGroup(Constants.MainDependencyGroup).Packages |> List.exactlyOne
    requirement.Name |> shouldEqual (PackageName "dotnet-fable")
    requirement.VersionRequirement.ToString() |> shouldEqual "1.3.7"
    requirement.Kind |> shouldEqual Paket.Requirements.PackageRequirementKind.DotnetCliTool

[<Test>]
let ``#321 paket add nuget is the default``() =
    let scenario = "i000321-add-nuget"
    use __ = paket "add Argu --version 1.2.3 --no-resolve" scenario |> fst

    let depsFile = DependenciesFile.ReadFromFile(Path.Combine(scenarioTempPath scenario,"paket.dependencies"))
    let requirement = depsFile.GetGroup(Constants.MainDependencyGroup).Packages |> List.exactlyOne
    requirement.Name |> shouldEqual (PackageName "Argu")
    requirement.VersionRequirement.ToString() |> shouldEqual "1.2.3"
    requirement.Kind |> shouldEqual Paket.Requirements.PackageRequirementKind.Package

[<Test>]
let ``#310 paket add nuget should not resolve inconsistent dependency graph``() = 
    try
        use __ = paket "add nuget Castle.Windsor version 3.3.0" "i000310-add-should-not-create-invalid-resolution" |> fst
        failwith "resolver error expected"
    with
    | exn when exn.Message.Contains("There was a version conflict during package resolution") -> ()