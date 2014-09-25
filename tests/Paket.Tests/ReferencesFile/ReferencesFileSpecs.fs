module Paket.ReferencesFileSpecs

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers

let refFileContent = """
Castle.Windsor
Newtonsoft.Json
jQuery
File:FsUnit.fs
"""

[<Test>]
let ``should parse lines correctly``() = 
    let refFile = ReferencesFile.FromLines(toLines refFileContent)
    refFile.NugetPackages.Length |> shouldEqual 3
    refFile.NugetPackages.Head |> shouldEqual "Castle.Windsor"
    refFile.NugetPackages.Tail.Tail.Head |> shouldEqual "jQuery"
    refFile.GithubFiles.Length |> shouldEqual 1
    refFile.GithubFiles.Head |> shouldEqual "FsUnit.fs"

[<Test>]
let ``should serialize itself correctly``() = 
    let refFile = {FileName = ""; NugetPackages = ["A"; "B"]; GithubFiles = ["FromGithub.fs"]}
    let expected = [|"A"; "B"; "File:FromGithub.fs"|]

    refFile.ToString() |> toLines |> shouldEqual expected