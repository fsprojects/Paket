module Paket.IntegrationTests.InitSpecs

open Fake
open System
open NUnit.Framework
open FsUnit
open System
open System.IO
open System.Diagnostics

[<Test>]
let ``#1040 init should download release version of bootstrapper``() = 
    paket "init" "i001040-init-downloads-bootstrapper"
    let bootstrapperPath = Path.Combine(scenarioTempPath "i001040-init-downloads-bootstrapper",".paket","paket.bootstrapper.exe")
   
    let productVersion = FileVersionInfo.GetVersionInfo(bootstrapperPath).ProductVersion
    String.IsNullOrWhiteSpace productVersion |> shouldEqual false
    productVersion.Contains("-") |> shouldEqual false