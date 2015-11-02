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
    ()