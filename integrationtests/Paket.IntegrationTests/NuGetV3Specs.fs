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
let ``#1211 update with V3 API should work exactly like V2 API``() =
    let lockFileV2 = update "i001211-top-level-v2"
    let lockFileV3 = update "i001211-top-level-v3"

    lockFileV3.ToString() 
    |> shouldEqual 
        (lockFileV2.ToString().Replace("remote: https://nuget.org/api/v2", "remote: http://api.nuget.org/v3/index.json"))
    lockFileV3.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "FsLexYacc.Runtime"].Version
    |> shouldBeGreaterThan (SemVer.Parse "6")