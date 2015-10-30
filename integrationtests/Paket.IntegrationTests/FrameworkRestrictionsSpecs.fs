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
    let lockFile = update "i000140-resolve-framework-restrictions"
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "TaskParallelLibrary"].Settings.FrameworkRestrictions
    |> shouldEqual [FrameworkRestriction.Exactly(DotNetFramework(FrameworkVersion.V3_5))]