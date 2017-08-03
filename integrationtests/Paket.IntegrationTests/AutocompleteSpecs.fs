module Paket.IntegrationTests.AutocompleteSpecs

open Fake
open System
open NUnit.Framework
open FsUnit
open System
open System.IO
open Pri.LongPath
open System.Diagnostics
open System.IO.Compression
open Paket
open Paket.PackageSources

[<Test>]
let ``#1298 should autocomplete for dapper on local feed``() = 
    let result = Dependencies.FindPackagesByName([PackageSource.LocalNuGet(Path.Combine(originalScenarioPath "i001219-props-files", "nuget_repo"),None)],"dapp")
    result |> shouldContain "Dapper"
    result |> shouldNotContain "dapper"
    
[<Test>]
let ``#1298 should autocomplete for fake on local feed``() = 
    let result = Dependencies.FindPackagesByName([PackageSource.LocalNuGet(Path.Combine(originalScenarioPath "i001219-props-files", "nuget_repo"),None)],"fake")
    result |> shouldContain "FAKE.Core"
    result |> shouldNotContain "Dapper"
    result |> shouldNotContain "dapper"

[<Test>]
let ``#1298 should autocomplete versions for FAKE on NuGet3``() = 
    let result = Dependencies.FindPackageVersions("",[PackageSource.NuGetV3Source Constants.DefaultNuGetV3Stream],"fake")
    result |> shouldContain "2.6.15"
    result |> shouldContain "4.14.9"
    result.Length |> shouldEqual 1000
    result |> shouldNotContain "FAKE.Core"

[<Test>]
[<Ignore("it's only working on forki's machine")>]
let ``#1298 should autocomplete versions for msu on local teamcity``() = 
    let result = Dependencies.FindPackageVersions("",[PackageSource.NuGetV2Source "http://teamcity/guestAuth/app/nuget/v1/FeedService.svc/"],"msu.Addins")
    result |> shouldNotContain "msu.Addins"
    result |> shouldContain "03.03.7"

[<Test>]
let ``#1298 should autocomplete versions for dapper on local feed``() = 
    let result = Dependencies.FindPackageVersions("",[PackageSource.LocalNuGet(DirectoryInfo(Path.Combine(originalScenarioPath "i001219-props-files", "nuget_repo")).FullName,None)],"Dapper")
    result |> shouldEqual [|"1.42.0"; "1.40"|]
    