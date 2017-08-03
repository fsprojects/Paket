module Paket.IntegrationTests.NuGetV3Specs

open Fake
open System
open NUnit.Framework
open FsUnit
open System
open System.IO
open Pri.LongPath
open System.Diagnostics
open Paket
open Paket.Domain

[<Test>]
let ``#1387 update package in v3``() =
    update "i001387-nugetv3" |> ignore
    let lockFile = LockFile.LoadFrom(Path.Combine(scenarioTempPath "i001387-nugetv3","paket.lock"))
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Bender"].Version
    |> shouldEqual (SemVer.Parse "3.0.29.0")