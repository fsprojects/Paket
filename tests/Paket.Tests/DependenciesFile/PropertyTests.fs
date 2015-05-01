module Paket.DependenciesFile.PropertyTests

open System
open FsCheck
open FsCheck.NUnit
open Paket

type DFFileGenerator =
    static member StringArray() = 
        {new Arbitrary<string[]>() with
            override x.Generator = Gen.elements [[|"nuget FsCheck"|]]
            override x.Shrinker t = Seq.empty }

[<Property(Arbitrary = [|typeof<DFFileGenerator>|])>]
let ``round trip`` (lines : string[]) =
    let df = DependenciesFile(DependenciesFileParser.parseDependenciesFile "dummy" lines)
    df.ToString() = String.concat Environment.NewLine lines 