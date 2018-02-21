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

[<Test>]
let ``should not error when adding existing github-repository``() =
    let existing = "github fsprojects/FsUnit"

    let cfg = DependenciesFile.FromSource(existing).AddGithub(Constants.MainDependencyGroup, "fsprojects/FsUnit")

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings existing)


[<Test>]
let ``should add new repositories to the main group``() = 
    let config = """source http://www.nuget.org/api/v2

group Test
github fsprojects/FAKE"""

    let cfg = DependenciesFile.FromSource(config).AddGithub(Constants.MainDependencyGroup, "fsprojects/FsUnit")
    
    let expected = """source http://www.nuget.org/api/v2
github fsprojects/FsUnit

group Test
github fsprojects/FAKE"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should add new repositories to the specified group``() = 
    let config = """source http://www.nuget.org/api/v2

group Test
github fsprojects/FAKE

group Test2
github fsprojects/SQLProvider"""

    let cfg = DependenciesFile.FromSource(config).AddGithub(GroupName "Test", "fsprojects/FsUnit")
    
    let expected = """source http://www.nuget.org/api/v2

group Test
github fsprojects/FAKE
github fsprojects/FsUnit

group Test2
github fsprojects/SQLProvider"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should add new group if not already exists``() = 
    let config = """source http://www.nuget.org/api/v2

group Test
github fsprojects/SQLProvider"""

    let cfg = DependenciesFile.FromSource(config).AddGithub(GroupName "Test2", "fsprojects/FsUnit")
    
    let expected = """source http://www.nuget.org/api/v2

group Test
github fsprojects/SQLProvider

group Test2
source https://www.nuget.org/api/v2

github fsprojects/FsUnit"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)
