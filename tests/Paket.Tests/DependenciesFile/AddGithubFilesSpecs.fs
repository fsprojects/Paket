module Paket.DependenciesFile.AddGithubFilesSpecs


open Paket
open NUnit.Framework
open FsUnit
open TestHelpers
open Paket.Domain
open Paket.Requirements

[<Test>]
let ``should add new repositories to the end``() = 
    let config = """source http://www.nuget.org/api/v2

github fsprojects/FAKE"""

    let cfg = DependenciesFile.FromSource(config).AddGithub(Constants.MainDependencyGroup, "fsprojects/FsUnit")
    
    let expected = """source http://www.nuget.org/api/v2

github fsprojects/FAKE
github fsprojects/FsUnit"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should add repository with file-name``() =

    let cfg = DependenciesFile.FromSource("").AddGithub(Constants.MainDependencyGroup, "fsprojects/FsUnit", "FsUnit.fs", "")
    
    let expected = """github fsprojects/FsUnit FsUnit.fs"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)


[<Test>]
let ``should add repository with version``() =

    let cfg = DependenciesFile.FromSource("").AddGithub(Constants.MainDependencyGroup, "tpetricek/FSharp.Formatting", "", "2.13.5")

    let expected = """github tpetricek/FSharp.Formatting:2.13.5"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)