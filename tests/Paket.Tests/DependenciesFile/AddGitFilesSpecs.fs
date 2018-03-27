module Paket.DependenciesFile.AddGitFilesSpecs


open Paket
open NUnit.Framework
open FsUnit
open TestHelpers
open Paket.Domain
open Paket.Requirements

[<Test>]
let ``should add new git repositories to the end``() = 
    let config = """source http://www.nuget.org/api/v2

git https://github.com/fsharp/FAKE.git"""

    let cfg = DependenciesFile.FromSource(config).AddGit(Constants.MainDependencyGroup, "https://github.com/fsprojects/FsUnit.git")
    
    let expected = """source http://www.nuget.org/api/v2

git https://github.com/fsharp/FAKE.git
git https://github.com/fsprojects/FsUnit.git"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)


[<Test>]
let ``should add git repository with version``() =

    let cfg = DependenciesFile.FromSource("").AddGit(Constants.MainDependencyGroup, "C:\Users\Steffen\AskMe", "2.13.5")

    let expected = """git C:\Users\Steffen\AskMe 2.13.5"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should add git repository with branch``() =

    let cfg = DependenciesFile.FromSource("").AddGit(Constants.MainDependencyGroup, "C:\Users\Steffen\AskMe", "master")

    let expected = """git C:\Users\Steffen\AskMe master"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should not error when adding existing git repository``() =
    let existing = "git https://github.com/fsprojects/FsUnit.git"

    let cfg = DependenciesFile.FromSource(existing).AddGit(Constants.MainDependencyGroup, "https://github.com/fsprojects/FsUnit.git", "")

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings existing)


[<Test>]
let ``should add new git repositories to the main group``() = 
    let config = """source http://www.nuget.org/api/v2

group Test
git https://github.com/fsprojects/FAKE.git"""

    let cfg = DependenciesFile.FromSource(config).AddGit(Constants.MainDependencyGroup, "https://github.com/fsprojects/FsUnit.git")
    
    let expected = """source http://www.nuget.org/api/v2
git https://github.com/fsprojects/FsUnit.git

group Test
git https://github.com/fsprojects/FAKE.git"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should add new repositories to the specified group``() = 
    let config = """source http://www.nuget.org/api/v2

group Test
git https://github.com/fsprojects/FAKE.git

group Test2
git https://github.com/fsprojects/SQLProvider.git"""

    let cfg = DependenciesFile.FromSource(config).AddGit(GroupName "Test", "https://github.com/fsprojects/FsUnit.git")
    
    let expected = """source http://www.nuget.org/api/v2

group Test
git https://github.com/fsprojects/FAKE.git
git https://github.com/fsprojects/FsUnit.git

group Test2
git https://github.com/fsprojects/SQLProvider.git"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should add new group if not already exists``() = 
    let config = """source http://www.nuget.org/api/v2

group Test
git https://github.com/fsprojects/SQLProvider.git"""

    let cfg = DependenciesFile.FromSource(config).AddGit(GroupName "Test2", "https://github.com/fsprojects/FsUnit.git")
    
    let expected = """source http://www.nuget.org/api/v2

group Test
git https://github.com/fsprojects/SQLProvider.git

group Test2
source https://www.nuget.org/api/v2

git https://github.com/fsprojects/FsUnit.git"""

    cfg.ToString()
    |> shouldEqual (normalizeLineEndings expected)
