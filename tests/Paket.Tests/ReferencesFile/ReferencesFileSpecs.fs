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
    refFile.GitHubFiles.Length |> shouldEqual 1
    refFile.GitHubFiles.Head.Name |> shouldEqual "FsUnit.fs"
    refFile.GitHubFiles.Head.Link |> shouldEqual ReferencesFile.DefaultLink

[<Test>]
let ``should serialize itself correctly``() = 
    let refFile = {FileName = ""; NugetPackages = ["A"; "B"]; GitHubFiles = [{Name = "FromGithub.fs"; Link = ReferencesFile.DefaultLink}]}
    let expected = [|"A"; "B"; "File:FromGithub.fs"|]

    refFile.ToString() |> toLines |> shouldEqual expected

let refFileWithCustomPath = """
File:FsUnit.fs Tests\Common
"""

[<Test>]
let ``should parse custom path correctly``() = 
    let refFile = ReferencesFile.FromLines(toLines refFileWithCustomPath)
    refFile.NugetPackages.Length |> shouldEqual 0
    refFile.GitHubFiles.Length |> shouldEqual 1
    refFile.GitHubFiles.Head.Name |> shouldEqual "FsUnit.fs"
    refFile.GitHubFiles.Head.Link |> shouldEqual "Tests\Common"

[<Test>]
let ``should serialize customPath correctly``() = 
    let refFile = {FileName = ""; NugetPackages = []; GitHubFiles = [{Name = "FromGithub.fs"; Link = "CustomPath\Dir"}]}
    let expected = [|"File:FromGithub.fs CustomPath\Dir"|]

    refFile.ToString() |> toLines |> shouldEqual expected

let refFileWithTrailingWhitespace = """
Castle.Windsor  
Newtonsoft.Json 
"""

[<Test>]
let ``should parse lines with trailing whitspace correctly``() = 
    let refFile = ReferencesFile.FromLines(toLines refFileWithTrailingWhitespace)
    refFile.NugetPackages.Length |> shouldEqual 2
    refFile.NugetPackages.Head |> shouldEqual "Castle.Windsor"
    refFile.NugetPackages.Tail.Head |> shouldEqual "Newtonsoft.Json"