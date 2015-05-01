module Paket.DependenciesFile.PropertyTests

open System
open FsCheck
open FsCheck.NUnit
open Paket

let source = Gen.constant "source https://nuget.org/api/v2"

let nuget = Gen.constant "nuget FsCheck"

let github = Gen.constant "github forki/FsUnit FsUnit.fs"

let gist = Gen.constant "gist Thorium/1972349 timestamp.fs"

let http = Gen.constant "http http://www.fssnip.net/1n decrypt.fs"

let depLine = Gen.oneof [source; nuget; github; gist; http]

type DFFileGenerator =
    static member StringArray() = 
        {new Arbitrary<string[]>() with
            override x.Generator = Gen.arrayOf depLine
            override x.Shrinker t = Seq.empty }

let _ = PropertyAttribute(Verbose = true)

[<Property(
    Arbitrary = [|typeof<DFFileGenerator>|],
    Verbose = true)>]
let ``round trip`` (lines : string[]) =
    let df = DependenciesFile(DependenciesFileParser.parseDependenciesFile "dummy" lines)
    df.ToString() = String.concat Environment.NewLine lines 