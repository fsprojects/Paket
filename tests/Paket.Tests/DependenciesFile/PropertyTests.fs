module Paket.DependenciesFile.PropertyTests

open System
open FsCheck
open NUnit.Framework
open Paket

let ``round trip`` (lines : string[]) =
    let df = DependenciesFile(DependenciesFileParser.parseDependenciesFile "dummy" lines)
    df.ToString() = String.concat Environment.NewLine lines 

[<Test>]
let ``round trip test``() =
    Check.QuickThrowOnFailure(``round trip``)