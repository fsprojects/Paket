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
    refFile.RemoteFiles.Length |> shouldEqual 1
    refFile.RemoteFiles.Head.Name |> shouldEqual "FsUnit.fs"
    refFile.RemoteFiles.Head.Link |> shouldEqual "paket-files"

[<Test>]
let ``should serialize itself correctly``() = 
    let refFile = {FileName = ""; NugetPackages = ["A"; "B"]; RemoteFiles = [{Name = "FromGithub.fs"; Link = ReferencesFile.DefaultLink}]}
    let expected = [|"A"; "B"; "File:FromGithub.fs"|]

    refFile.ToString() |> toLines |> shouldEqual expected

let refFileWithCustomPath = """
File:FsUnit.fs Tests\Common
"""

[<Test>]
let ``should parse custom path correctly``() = 
    let refFile = ReferencesFile.FromLines(toLines refFileWithCustomPath)
    refFile.NugetPackages.Length |> shouldEqual 0
    refFile.RemoteFiles.Length |> shouldEqual 1
    refFile.RemoteFiles.Head.Name |> shouldEqual "FsUnit.fs"
    refFile.RemoteFiles.Head.Link |> shouldEqual "Tests\Common"

[<Test>]
let ``should serialize customPath correctly``() = 
    let refFile = {FileName = ""; NugetPackages = []; RemoteFiles = [{Name = "FromGithub.fs"; Link = "CustomPath\Dir"}]}
    let expected = [|"File:FromGithub.fs CustomPath\Dir"|]

    refFile.ToString() |> toLines |> shouldEqual expected