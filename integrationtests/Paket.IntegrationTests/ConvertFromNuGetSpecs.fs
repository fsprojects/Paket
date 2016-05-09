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
open Paket.ProjectJson

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
    requirement.ResolverStrategyForTransitives |> shouldEqual None
    requirement.Settings.FrameworkRestrictions  |> getRestrictionList |> shouldEqual [FrameworkRestriction.AtLeast(FrameworkIdentifier.DotNetFramework(FrameworkVersion.V3_5))]

    let requirement2 = depsFile.GetGroup(Constants.MainDependencyGroup).Packages.Tail.Head
    requirement2.Name |> shouldEqual (PackageName "Newtonsoft.Json")
    requirement2.VersionRequirement.ToString() |> shouldEqual "7.0.1"
    requirement2.ResolverStrategyForTransitives |> shouldEqual None
    requirement2.Settings.FrameworkRestrictions  |> getRestrictionList |> shouldEqual [FrameworkRestriction.AtLeast(FrameworkIdentifier.DotNetFramework(FrameworkVersion.V4_Client))]

[<Test>]
let ``#1217 should replace packages.config files in project``() = 
    let originalProjectFile = ProjectFile.loadFromFile(Path.Combine(originalScenarioPath "i001217-convert-simple-project", "ClassLibrary1", "ClassLibrary1.csprojtemplate"))
    originalProjectFile.Document.OuterXml.Contains("packages.config") |> shouldEqual true
    originalProjectFile.Document.OuterXml.Contains("paket.references") |> shouldEqual false

    paket "convert-from-nuget" "i001217-convert-simple-project" |> ignore
    let lockFile = LockFile.LoadFrom(Path.Combine(scenarioTempPath "i001217-convert-simple-project","paket.lock"))
    let projectFile = ProjectFile.loadFromFile(Path.Combine(scenarioTempPath "i001217-convert-simple-project", "ClassLibrary1", "ClassLibrary1.csproj"))
    projectFile.Document.OuterXml.Contains("packages.config") |> shouldEqual false
    projectFile.Document.OuterXml.Contains("paket.references") |> shouldEqual true

[<Test>]
let ``#1591 should convert denormalized versions``() = 
    paket "convert-from-nuget" "i001591-convert-denormalized" |> ignore
    let lockFile = LockFile.LoadFrom(Path.Combine(scenarioTempPath "i001591-convert-denormalized","paket.lock"))
    let v = lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "EntityFramework"].Version
    v.Major |> shouldEqual 6u
    v.Minor |> shouldEqual 1u
    v.Patch |> shouldEqual 0u

    let depsFile = File.ReadAllText(Path.Combine(scenarioTempPath "i001591-convert-denormalized","paket.dependencies"))
    depsFile.Contains "6.1.0" |> shouldEqual true

[<Test>]
let ``#1217 should convert simple project.json``() =
    if not Constants.UseProjectJson then () else
    let originalProjectFile = ProjectJsonFile.Load(Path.Combine(originalScenarioPath "i001217-convert-json-project", "project.jsontemplate"))
    originalProjectFile.GetGlobalDependencies() 
    |> List.map (fun (n,v) -> n.ToString(),v.ToString())
    |> shouldEqual ["Newtonsoft.Json", ">= 1.0"]

    paket "convert-from-nuget" "i001217-convert-json-project" |> ignore
    let lockFile = LockFile.LoadFrom(Path.Combine(scenarioTempPath "i001217-convert-json-project","paket.lock"))
    
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Newtonsoft.Json"].Version
    |> shouldBeGreaterThan (SemVer.Parse "1.0.0")

    let projectFile = ProjectJsonFile.Load(Path.Combine(scenarioTempPath "i001217-convert-json-project", "project.json"))
    projectFile.GetGlobalDependencies() 
    |> List.map (fun (n,v) ->n.ToString(),v.ToString())
    |> shouldEqual ["Newtonsoft.Json", lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Newtonsoft.Json"].Version.ToString()]

[<Test>]
let ``#1217 should convert project.json app``() = 
    if not Constants.UseProjectJson then () else
    let originalProjectFile = ProjectJsonFile.Load(Path.Combine(originalScenarioPath "i001217-convert-json-projects", "TestApp", "project.jsontemplate"))
    originalProjectFile.GetGlobalDependencies() 
    |> List.map (fun (n,v) -> n.ToString(),v.ToString())
    |> shouldEqual ["Newtonsoft.Json", ">= 1.0"]

    let originalInterprojectDependencies = originalProjectFile.GetGlobalInterProjectDependencies()

    paket "convert-from-nuget" "i001217-convert-json-projects" |> ignore
    let lockFile = LockFile.LoadFrom(Path.Combine(scenarioTempPath "i001217-convert-json-projects","paket.lock"))
    
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Newtonsoft.Json"].Version
    |> shouldBeGreaterThan (SemVer.Parse "1.0.0")

    let projectFile = ProjectJsonFile.Load(Path.Combine(scenarioTempPath "i001217-convert-json-projects", "TestApp", "project.json"))
    projectFile.GetGlobalDependencies() 
    |> List.map (fun (n,v) ->n.ToString(),v.ToString())
    |> shouldEqual ["Newtonsoft.Json", lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Newtonsoft.Json"].Version.ToString()]

    projectFile.GetGlobalInterProjectDependencies()
    |> shouldEqual (originalProjectFile.GetGlobalInterProjectDependencies())

    let appReferences = ReferencesFile.FromFile(Path.Combine(scenarioTempPath "i001217-convert-json-projects", "TestApp", "paket.references"))
    let libReferences = ReferencesFile.FromFile(Path.Combine(scenarioTempPath "i001217-convert-json-projects", "TestApp", "paket.references"))

    appReferences.Groups.[Constants.MainDependencyGroup].NugetPackages |> List.map (fun n -> n.Name) |> shouldContain (PackageName "Newtonsoft.Json")
    libReferences.Groups.[Constants.MainDependencyGroup].NugetPackages |> List.map (fun n -> n.Name) |> shouldContain (PackageName "Newtonsoft.Json")
