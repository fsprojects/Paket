module Paket.IntegrationTests.PackSpecs

open Fake
open System
open NUnit.Framework
open FsUnit
open System
open System.IO
open System.Diagnostics

[<Test>]
let ``#1234 empty assembly name``() = 
    let outPath = Path.Combine(scenarioTempPath "i001234-missing-assemblyname","out")
    try
        paket ("pack -v output \"" + outPath + "\"") "i001234-missing-assemblyname" |> ignore
        failwith "Expected an exeption"
    with
    | exn when exn.Message.Contains("PaketBug.dll") -> ()

    File.Delete(Path.Combine(scenarioTempPath "i001234-missing-assemblyname","PaketBug","paket.template"))