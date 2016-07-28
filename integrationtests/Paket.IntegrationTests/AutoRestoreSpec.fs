module Paket.IntegrationTests.AutoRestoreSpec

open System
open System.IO
open Fake
open NUnit.Framework
open FsUnit

[<Test>]
let ``#1835 auto-restore on downloads targets and updates proj file``() = 
    let scenario = "i001835-auto-restore-on-twice"
    paket "auto-restore on" scenario |> ignore
    let scenarioPath = scenarioTempPath scenario
    Path.Combine(scenarioPath , ".paket","paket.targets") |> checkFileExists
    let projectFile = File.ReadAllText(Path.Combine(scenarioPath, "AutoRestoreTwice" ,"AutoRestoreTwice.csproj"))
    StringAssert.Contains("<Import Project=\"..\.paket\paket.targets\" />", projectFile)



