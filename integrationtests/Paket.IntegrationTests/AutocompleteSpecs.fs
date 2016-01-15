module Paket.IntegrationTests.AutocompleteSpecs

open Fake
open System
open NUnit.Framework
open FsUnit
open System
open System.IO
open System.Diagnostics
open System.IO.Compression
open Paket
open Paket.PackageSources

[<Test>]
let ``#1298 should autocomplete for FAKE on NuGet 2``() = 
    let result = Dependencies.FindPackagesByName([PackageSources.DefaultNuGetSource],"fake")
    result |> shouldContain "FAKE"
    result |> shouldContain "FAKE.IIS"

[<Test>]
let ``#1298 should autocomplete for FAKE on NuGet3``() = 
    let result = Dependencies.FindPackagesByName([PackageSource.NuGetV3Source Constants.DefaultNuGetV3Stream],"fake")
    result |> shouldContain "FAKE"
    result |> shouldContain "FAKE.IIS"