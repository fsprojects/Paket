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
    Git.Handling.getCurrentHash gistDir |> shouldEqual (Some "b14d55a8844f092b44b9155c904c8a3f2d9d9f46")

    let chessieDir = Path.Combine(paketFilesRoot,"github.com","AskMe")
    Git.Handling.getCurrentHash chessieDir |> shouldEqual (Some "23c2cf83495c9096f9bdd9d629a0849e3e853f42")