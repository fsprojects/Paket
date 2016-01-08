module Paket.IntegrationTests.FullGitSpecs

open Fake
open System
open NUnit.Framework
open FsUnit
open System
open System.IO
open System.Diagnostics
open Paket
open Paket.Domain

[<Test>]
let ``#284 should use git.exe to restore``() = 
    let newLockFile = install "i000284-full-git"
    let paketFilesRoot = Path.Combine(FileInfo(newLockFile.FileName).Directory.FullName,"paket-files")
    let gistDir = Path.Combine(paketFilesRoot,"gist.github.com","89d8f9fd6d466224da04")

    Directory.Exists (Path.Combine(paketFilesRoot,"gist.github.com")) |> shouldEqual true
    Directory.Exists gistDir |> shouldEqual true
    Git.Handling.getCurrentHash gistDir |> shouldEqual (Some "b14d55a8844f092b44b9155c904c8a3f2d9d9f46")

    let askMeDir = Path.Combine(paketFilesRoot,"github.com","AskMe")
    Git.Handling.getCurrentHash askMeDir |> shouldEqual (Some "97ee5ae7074bdb414a3e5dd7d2f2d752547d0542")

    let fsUnitDir = Path.Combine(paketFilesRoot,"github.com","FsUnit")
    Git.Handling.getCurrentHash fsUnitDir |> shouldEqual (Some "96a9c7bda5a84e225450d83ab8fd58fdeced7f6d")

[<Test>]
let ``#1353 should restore NuGet source from git repo``() = 
    let lockFile = restore "i001353-git-as-source-restore"
    let paketFilesRoot = Path.Combine(scenarioTempPath "i001353-git-as-source-restore","paket-files")
    let repoDir = Path.Combine(paketFilesRoot,"github.com","nupkgtest")
    Git.Handling.getCurrentHash repoDir |> shouldEqual (Some "05366e390e7552a569f3f328a0f3094249f3b93b")

    let arguPackagesDir = Path.Combine(scenarioTempPath "i001353-git-as-source-restore","packages","Argu")
    Directory.Exists arguPackagesDir |> shouldEqual true

[<Test>]
let ``#1353 should use NuGet source from git repo``() = 
    let lockFile = update "i001353-git-as-source"
    let paketFilesRoot = Path.Combine(FileInfo(lockFile.FileName).Directory.FullName,"paket-files")
    let repoDir = Path.Combine(paketFilesRoot,"github.com","nupkgtest")
    Git.Handling.getCurrentHash repoDir |> shouldEqual (Some "05366e390e7552a569f3f328a0f3094249f3b93b")

    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Argu"].Version
    |> shouldEqual (SemVer.Parse "1.1.3")