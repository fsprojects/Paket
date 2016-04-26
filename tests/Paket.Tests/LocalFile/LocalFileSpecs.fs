module Paket.LocalFileSpecs

open Chessie.ErrorHandling

open NUnit.Framework

open FsUnit

open Paket.Domain
open Paket.PackageSources
open Paket.TestHelpers

[<Test>]
let ``should parse single dev source override``() = 
    let contents = """
        nuget NUnit -> source ./local_source
        """
    let expected = 
        LocalFile [
            DevNugetSourceOverride (PackageName "NUnit", LocalNuGet ("./local_source", None)) 
        ]
        |> Trial.ok

    let actual = LocalFile.parse (toLines contents)

    actual |> shouldEqual expected