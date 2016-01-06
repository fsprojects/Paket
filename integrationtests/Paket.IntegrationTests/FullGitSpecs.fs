module Paket.IntegrationTests.FullGitSpecs

open Fake
open System
open NUnit.Framework
open FsUnit
open System
open System.IO
open System.Diagnostics
open Paket

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