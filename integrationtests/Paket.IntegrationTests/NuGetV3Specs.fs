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
open Paket
open Paket.Domain
open Paket.Logging
open Paket.NuGetV3

[<Test>]
let ``#1387 update package in v3``() =
    update "i001387-nugetv3" |> ignore
    let lockFile = LockFile.LoadFrom(Path.Combine(scenarioTempPath "i001387-nugetv3","paket.lock"))
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Bender"].Version
    |> shouldEqual (SemVer.Parse "3.0.29.0")

[<Test>]
let ``#2700-1 v3 works properly``() =
    paketEx true "update" "i002700-1" |> ignore
    let lockFile = LockFile.LoadFrom(Path.Combine(scenarioTempPath "i002700-1","paket.lock"))
    let mainGroup = lockFile.Groups.[Constants.MainDependencyGroup]
    mainGroup.Resolution.[PackageName "Microsoft.CSharp"].Source.Url
    |> shouldEqual "https://www.myget.org/F/dotnet-core-svc/api/v3/index.json"

[<TestCase("https://api.nuget.org/v3/index.json")>]
let ``#3030 interpret all versions in nuget catalog`` serviceUrl =
    let auth = None // nothing at this time, can change
    let errorLog = new List<String>()
    use canceler = new CancellationTokenSource(6000000)
    use tconsole = Logging.event.Publish |> Observable.subscribe Logging.traceToConsole
    
    let baseDir = Path.Combine(integrationTestPath,"i003030-catalog")
    let tempDir = Path.Combine(Path.GetTempPath(),"PaketTests\\nuget3")
    let catalog = getCatalogCursor baseDir serviceUrl
    let updated = (getCatalogUpdated auth tempDir catalog canceler.Token).Result  
    
    canceler.Cancel()
    setCatalogCursor baseDir updated
    
    let warnLog = new List<String>()
    /// this should be "0.0.0-0" per https://semver.org/#spec-item-11
    /// but SemVerInfo.CompareTo special-cases "prerelease" to be lower
    let preZero = SemVer.Parse "0.0.0-prerelease" // smallest version
    for package in updated.Packages do
        let name = package.Key
        for original in package.Value do
            let version = original |> String.split [|'!'|] |> Array.head
            let semVer = 
                try
                    Some (SemVer.Parse(version))
                with
                | ex -> 
                    let message = sprintf "%s %s : %A" name version ex
                    warnLog.Add(message); traceWarn message
                    None
            match semVer with
            | Some value -> 
                match value with
                | x when x < preZero ->
                    let message = sprintf "%s %s < %A" name version preZero
                    warnLog.Add(message); traceWarn message
                | _ -> ignore() // succeeded as-is
            | None -> ignore() // already recorded
        
    if warnLog.Count > 0 then Assert.Warn(sprintf "%A" warnLog)
    errorLog |> shouldBeEmpty
