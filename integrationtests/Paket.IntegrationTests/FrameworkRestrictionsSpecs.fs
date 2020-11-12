module Paket.IntegrationTests.FrameworkRestrictionsSpecs

open Fake
open Paket
open System
open NUnit.Framework
open FsUnit
open System
open System.IO
open Paket.Domain
open Paket.Requirements

[<Test>]
let ``#140 windsor should resolve framework dependent dependencies``() =
    let cleanup, lockFile = update "i000140-resolve-framework-restrictions"
    use __ = cleanup
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "TaskParallelLibrary"].Settings.FrameworkRestrictions
    |> getExplicitRestriction
    |> shouldEqual (FrameworkRestriction.Between(DotNetFramework(FrameworkVersion.V3_5), DotNetFramework(FrameworkVersion.V4)))

[<Test>]
#if NO_UNIT_PLATFORMATTRIBUTE
[<Ignore "PlatformAttribute not supported by netstandard NUnit">]
#else
[<Platform("Mono")>] // PATH TOO LONG on Windows...
[<Flaky>] // failure on assert
#endif
let ``#1190 paket add nuget should handle transitive dependencies``() = 
    use __ = paket "add nuget xunit version 2.1.0" "i001190-transitive-dependencies-with-restr" |> fst
    let lockFile = LockFile.LoadFrom(Path.Combine(scenarioTempPath "i001190-transitive-dependencies-with-restr","paket.lock"))
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "xunit.abstractions"].Settings.FrameworkRestrictions
    |> getExplicitRestriction
    |> fun res -> res.ToString() |> shouldEqual "|| (>= dnx451) (>= dnxcore50) (>= portable-net45+win8+wp8+wpa81)"
    
[<Test>]
let ``#1190 paket add nuget should handle transitive dependencies with restrictions``() = 
    use __ = paket "add nuget xunit version 2.1.0" "i001190-transitive-deps" |> fst
    
    let lockFile = LockFile.LoadFrom(Path.Combine(scenarioTempPath "i001190-transitive-deps","paket.lock"))
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "xunit.abstractions"].Settings.FrameworkRestrictions
    |> getExplicitRestriction
    |> shouldEqual FrameworkRestriction.NoRestriction
    
    
[<Test>]
let ``#1197 framework dependencies are not restricting each other``() = 
    let cleanup, lockFile = update "i001197-too-strict-frameworks"
    use __ = cleanup
    
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "log4net"].Version
    |> shouldBeGreaterThan (SemVer.Parse "0")

    
[<Test>]
let ``#1213 framework dependencies propagate``() = 
    let cleanup, lockFile = update "i001213-framework-propagation"
    use __ = cleanup
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Newtonsoft.Json"].Settings.FrameworkRestrictions
    |> getExplicitRestriction
    |> shouldEqual FrameworkRestriction.NoRestriction

[<Test>]
let ``#1215 framework dependencies propagate``() = 
    let cleanup, lockFile = update "i001215-framework-propagation-no-restriction"
    use __ = cleanup
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Microsoft.Bcl.Async"].Settings.FrameworkRestrictions
    |> getExplicitRestriction
    |> shouldEqual FrameworkRestriction.NoRestriction

[<Test>]
let ``#1232 framework dependencies propagate``() = 
    let cleanup, lockFile = update "i001232-sql-lite"
    use __ = cleanup
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
    use __ = update "i001494-download" |> fst
    
    ()