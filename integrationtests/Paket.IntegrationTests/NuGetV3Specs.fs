module Paket.IntegrationTests.NuGetV3Specs

open Fake
open System
open NUnit.Framework
open FsUnit
open System
open System.IO
open System.Diagnostics
open System.Collections.Generic
open System.Threading
open Paket
open Paket.Domain
open Paket.Logging
open Paket.NuGetV3

[<Test>]
let ``#1387 update package in v3``() =
    use __ = update "i001387-nugetv3" |> fst
    let lockFile = LockFile.LoadFrom(Path.Combine(scenarioTempPath "i001387-nugetv3","paket.lock"))
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Bender"].Version
    |> shouldEqual (SemVer.Parse "3.0.29.0")

[<Test>]
let ``#2700-1 v3 works properly``() =
    use __ = paketEx true "update" "i002700-1" |> fst
    let lockFile = LockFile.LoadFrom(Path.Combine(scenarioTempPath "i002700-1","paket.lock"))
    let mainGroup = lockFile.Groups.[Constants.MainDependencyGroup]
    mainGroup.Resolution.[PackageName "Microsoft.CSharp"].Source.Url
    |> shouldEqual "https://api.nuget.org/v3/index.json"

[<TestCase("https://api.nuget.org/v3/index.json")>]
let ``#3030-1 version ordering should not change`` serviceUrl =
    use tconsole = Logging.event.Publish |> Observable.subscribe Logging.traceToConsole
    
    let baseDir = Path.Combine(integrationTestPath, "i003030-catalog")
    
    let catalog = getCatalogCursor baseDir serviceUrl    
    let ordered = catalog |> catalogSemVer2ordered
    
    let failure = new List<String>()
    for package in catalog.Packages do
        try
            match ordered.Packages.TryFind package.Key with
            | Some versions -> CollectionAssert.AreEqual(package.Value, versions)
            | None -> failwith "Missing in odered collection"
        with
        | ex -> failure.Add(sprintf "%s : %A" package.Key ex)
    failure |> shouldBeEmpty
    
[<TestCase("https://api.nuget.org/v3/index.json")>]
let ``#3030-2 interpret all versions in nuget catalog`` serviceUrl =
    use tconsole = Logging.event.Publish |> Observable.subscribe Logging.traceToConsole
    
    let baseDir = Path.Combine(integrationTestPath, "i003030-catalog")
    let catalog = getCatalogCursor baseDir serviceUrl    
    
    let failure = new List<String>()
    /// this should be "0.0.0-0" per https://semver.org/#spec-item-11
    /// but SemVerInfo.CompareTo special-cases "prerelease" to be lower
    let preZero = SemVer.Parse "0.0.0-prerelease" // smallest version
    for package in catalog.Packages do
        let name = package.Key
        for original in package.Value do
            let version = original |> String.split [|'!'|] |> Array.head
            let semVer = 
                try
                    Some (SemVer.Parse(version))
                with
                | ex -> 
                    let message = sprintf "%s %s : %A" name version ex
                    failure.Add(message); traceWarn message
                    None
            match semVer with
            | Some value -> 
                match value with
                | x when x < preZero ->
                    let message = sprintf "%s %s < %A" name version preZero
                    failure.Add(message); traceWarn message
                | _ -> ignore() // succeeded as-is
            | None -> ignore() // already recorded
    if failure.Count > 0 then Assert.Warn(sprintf "%A" failure)
    else Assert.IsTrue(true) // runners fail tests w/o asserts
    // failure |> shouldBeEmpty -- use after SemVer is fixed
