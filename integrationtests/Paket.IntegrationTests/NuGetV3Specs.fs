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
let ``#1211 update with V3 API``() =
    let lockFile = update "i001211-top-level"
        
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "FsLexYacc.Runtime"].Version
    |> shouldBeGreaterThan (SemVer.Parse "6")