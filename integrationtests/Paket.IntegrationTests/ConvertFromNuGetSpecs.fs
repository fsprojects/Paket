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
open Paket.Requirements

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

[<Test>]
let ``#1225 should convert simple C# project with non-matching framework restrictions``() = 
    paket "convert-from-nuget" "i001225-convert-simple-project-non-matching-restrictions" |> ignore
    let lockFile = LockFile.LoadFrom(Path.Combine(scenarioTempPath "i001225-convert-simple-project-non-matching-restrictions","paket.lock"))
    let v = lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Newtonsoft.Json"].Version
    v.Major |> shouldEqual 7u
    v.Minor |> shouldEqual 0u
    v.Patch |> shouldEqual 1u

    let v2 = lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Castle.Core"].Version
    v2.Major |> shouldEqual 3u
    v2.Minor |> shouldEqual 3u
    v2.Patch |> shouldEqual 3u

    let depsFile = DependenciesFile.ReadFromFile(Path.Combine(scenarioTempPath "i001225-convert-simple-project-non-matching-restrictions","paket.dependencies"))
    let requirement = depsFile.GetGroup(Constants.MainDependencyGroup).Packages.Head
    requirement.Name |> shouldEqual (PackageName "Castle.Core")
    requirement.VersionRequirement.ToString() |> shouldEqual "3.3.3"
    requirement.ResolverStrategy |> shouldEqual None
    requirement.Settings.FrameworkRestrictions |> shouldEqual [FrameworkRestriction.AtLeast(FrameworkIdentifier.DotNetFramework(FrameworkVersion.V3_5))]

    let requirement2 = depsFile.GetGroup(Constants.MainDependencyGroup).Packages.Tail.Head
    requirement2.Name |> shouldEqual (PackageName "Newtonsoft.Json")
    requirement2.VersionRequirement.ToString() |> shouldEqual "7.0.1"
    requirement2.ResolverStrategy |> shouldEqual None
    requirement2.Settings.FrameworkRestrictions |> shouldEqual [FrameworkRestriction.AtLeast(FrameworkIdentifier.DotNetFramework(FrameworkVersion.V4_Client))]