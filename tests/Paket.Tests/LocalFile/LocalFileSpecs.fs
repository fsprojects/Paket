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
            LocalSourceOverride (PackageName "NUnit", LocalNuGet ("./local_source", None)) 
        ]
        |> Trial.ok

    let actual = LocalFile.parse (toLines contents |> Array.toList)

    actual |> shouldEqual expected


[<Test>]
let ``should ignore comments``() = 
    let contents = """
        // override NUnit with nupkg from local directory
        nuget NUnit -> source ./local_source
        # override FAKE with nupkg built from git repository
        nuget FAKE -> git file:\\\c:/github/FAKE fature_branch build:"build.cmd", Packages: /bin/
        """
    
    let actual = LocalFile.parse (toLines contents |> Array.toList)

    match actual with
    | Ok (LocalFile overrides, _) ->
        overrides |> shouldHaveLength 2
    | Bad msgs ->
        Assert.Fail (msgs |> String.concat System.Environment.NewLine)