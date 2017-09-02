module Paket.IntegrationTests.NuGetV3Specs

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

[<Test>]
let ``#2700-2 v2 is not upgraded to v3``() =
    updateEx true "i002700-2" |> ignore
    let lockFile = LockFile.LoadFrom(Path.Combine(scenarioTempPath "i002700-2","paket.lock"))
    let mainGroup = lockFile.Groups.[Constants.MainDependencyGroup]
    mainGroup.Resolution.[PackageName "Microsoft.CSharp"].Source.Url
    |> shouldEqual "https://www.myget.org/F/dotnet-core-svc"