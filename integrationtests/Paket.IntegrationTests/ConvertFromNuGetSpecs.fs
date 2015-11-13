module Paket.IntegrationTests.ConvertFromNuGetSpecs

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
let ``#1217 should convert simple C# project``() = 
    paket "convert-from-nuget" "i001217-convert-simple-project" |> ignore
    let lockFile = LockFile.LoadFrom(Path.Combine(scenarioTempPath "i001217-convert-simple-project","paket.lock"))
    let v = lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Newtonsoft.Json"].Version
    v.Major |> shouldEqual 7u
    v.Minor |> shouldEqual 0u
    v.Patch |> shouldEqual 1u

    let depsFile = DependenciesFile.ReadFromFile(Path.Combine(scenarioTempPath "i001217-convert-simple-project","paket.dependencies"))
    let requirement = depsFile.GetGroup(Constants.MainDependencyGroup).Packages.Head
    requirement.Name |> shouldEqual (PackageName "Newtonsoft.Json")
    requirement.VersionRequirement.ToString() |> shouldEqual "7.0.1"