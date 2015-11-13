module Paket.IntegrationTests.InstallSpecs

open Fake
open System
open NUnit.Framework
open FsUnit
open System
open System.IO
open System.Diagnostics
open Paket

[<Test>]
let ``#1135 should keep lockfile stable when using framework restrictions``() = 
    let newLockFile = install "i001135-stable-install-on-framework-restrictions"
    let oldLockFile = LockFile.LoadFrom(Path.Combine(originalScenarioPath "i001135-stable-install-on-framework-restrictions","paket.lock"))
    newLockFile.ToString()
    |> shouldEqual (oldLockFile.ToString())

[<Test>]
let ``#1219 install props``() = 
    let newLockFile = install "i001219-props-files"
    let newFile = Path.Combine(scenarioTempPath "i001219-props-files","MyClassLibrary","MyClassLibrary","MyClassLibrary.csproj")
    let oldFile = Path.Combine(originalScenarioPath "i001219-props-files","MyClassLibrary","MyClassLibrary","MyClassLibrary.csprojtemplate")
    let s1 = File.ReadAllText oldFile |> normalizeLineEndings
    let s2 = File.ReadAllText newFile |> normalizeLineEndings
    s1 |> shouldEqual s2

    let newFile = Path.Combine(scenarioTempPath "i001219-props-files","MyClassLibrary","MyClassLibrary2","MyClassLibrary.csproj")
    let oldFile = Path.Combine(originalScenarioPath "i001219-props-files","MyClassLibrary","MyClassLibrary2","MyClassLibrary.csprojtemplate")
    let s1 = File.ReadAllText oldFile |> normalizeLineEndings
    let s2 = File.ReadAllText newFile |> normalizeLineEndings
    s1 |> shouldEqual s2