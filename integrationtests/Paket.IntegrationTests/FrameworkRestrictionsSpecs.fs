module Paket.IntegrationTests.FrameworkRestrictionsSpecs

open Fake
open Paket
open System
open NUnit.Framework
open FsUnit
open System
open System.IO
open Pri.LongPath
open Paket.Domain
open Paket.Requirements

[<Test>]
let ``#140 windsor should resolve framework dependent dependencies``() =
    let lockFile = update "i000140-resolve-framework-restrictions"
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "TaskParallelLibrary"].Settings.FrameworkRestrictions
    |> getExplicitRestriction
    |> shouldEqual (FrameworkRestriction.Between(DotNetFramework(FrameworkVersion.V3_5), DotNetFramework(FrameworkVersion.V4)))

[<Test>]
let ``#1182 framework restrictions overwrite each other``() =
    let lockFile = update "i001182-framework-restrictions"
    let lockFile = lockFile.ToString()
    lockFile.Contains("Microsoft.Data.OData (>= 5.6.2)") |> shouldEqual true
    lockFile.Contains("framework: winv4.5") |> shouldEqual false

[<Test>]
[<Platform("Mono")>] // PATH TOO LONG on Windows...
let ``#1190 paket add nuget should handle transitive dependencies``() = 
    paket "add nuget xunit version 2.1.0" "i001190-transitive-dependencies-with-restr" |> ignore
    let lockFile = LockFile.LoadFrom(Path.Combine(scenarioTempPath "i001190-transitive-dependencies-with-restr","paket.lock"))
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "xunit.abstractions"].Settings.FrameworkRestrictions
    |> getExplicitRestriction
    |> fun res -> res.ToString() |> shouldEqual "|| (>= dnx451) (>= dnxcore50) (>= portable-net45+win8+wp8+wpa81)"
    
[<Test>]
let ``#1190 paket add nuget should handle transitive dependencies with restrictions``() = 
    paket "add nuget xunit version 2.1.0" "i001190-transitive-deps" |> ignore
    
    let lockFile = LockFile.LoadFrom(Path.Combine(scenarioTempPath "i001190-transitive-deps","paket.lock"))
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "xunit.abstractions"].Settings.FrameworkRestrictions
    |> getExplicitRestriction
    |> shouldEqual FrameworkRestriction.NoRestriction
    
    
[<Test>]
let ``#1197 framework dependencies are not restricting each other``() = 
    let lockFile = update "i001197-too-strict-frameworks"
    
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "log4net"].Version
    |> shouldBeGreaterThan (SemVer.Parse "0")

    
[<Test>]
let ``#1213 framework dependencies propagate``() = 
    let lockFile = update "i001213-framework-propagation"
    
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Newtonsoft.Json"].Settings.FrameworkRestrictions
    |> getExplicitRestriction
    |> shouldEqual FrameworkRestriction.NoRestriction

[<Test>]
let ``#1215 framework dependencies propagate``() = 
    let lockFile = update "i001215-framework-propagation-no-restriction"
    
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Microsoft.Bcl.Async"].Settings.FrameworkRestrictions
    |> getExplicitRestriction
    |> shouldEqual FrameworkRestriction.NoRestriction

[<Test>]
let ``#1232 framework dependencies propagate``() = 
    let lockFile = update "i001232-sql-lite"
    let restriction =
        lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "System.Data.SQLite.Core"].Settings.FrameworkRestrictions
        |> getExplicitRestriction
    (FrameworkRestriction.Between(DotNetFramework(FrameworkVersion.V4), DotNetFramework(FrameworkVersion.V4_5))).IsSubsetOf restriction
    |> shouldEqual true
    (FrameworkRestriction.Between(DotNetFramework(FrameworkVersion.V4_5), DotNetFramework(FrameworkVersion.V4_5_1))).IsSubsetOf restriction
    |> shouldEqual true
    (FrameworkRestriction.AtLeast(DotNetFramework(FrameworkVersion.V4_6))).IsSubsetOf restriction
    |> shouldEqual true

[<Test>]
let ``#1494 detect platform 5.0``() = 
    let lockFile = update "i001494-download"
    
    ()