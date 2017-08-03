module Paket.IntegrationTests.InitSpecs

open Fake
open System
open NUnit.Framework
open FsUnit
open System
open System.IO
open Pri.LongPath
open System.Diagnostics

[<Test>]
let ``#1040 init should download release version of bootstrapper``() = 
    paket "init" "i001040-init-downloads-bootstrapper" |> ignore
    let bootstrapperPath = Path.Combine(scenarioTempPath "i001040-init-downloads-bootstrapper",".paket","paket.exe")
   
    let productVersion = FileVersionInfo.GetVersionInfo(bootstrapperPath).ProductVersion
    String.IsNullOrWhiteSpace productVersion |> shouldEqual false
    productVersion.Contains("-") |> shouldEqual false

[<Test>]
let ``#1743 empty log file``() =
    try
        paket "init --log-file" "i001040-init-downloads-bootstrapper" |> ignore
        failwith "expected error"
    with
    | exn when exn.Message.Split('\n').[0].Contains "--log-file" -> ()

[<Test>]
let ``#1240 current bootstrapper should work``() = 
    CleanDir (scenarioTempPath "i001240-bootstrapper")
    let paketToolPath = FullName(__SOURCE_DIRECTORY__ + "../../../bin/paket.bootstrapper.exe")
    CopyFile (scenarioTempPath "i001240-bootstrapper") paketToolPath

    let result =
        ExecProcessAndReturnMessages (fun info ->
          info.FileName <- scenarioTempPath "i001240-bootstrapper" </> "paket.bootstrapper.exe"
          info.WorkingDirectory <- scenarioTempPath "i001240-bootstrapper"
          info.Arguments <- "") (System.TimeSpan.FromMinutes 5.)
    if result.ExitCode <> 0 then 
        let errors = String.Join(Environment.NewLine,result.Errors)
        printfn "%s" <| String.Join(Environment.NewLine,result.Messages)
        failwith errors

    String.Join(Environment.NewLine,result.Messages).Contains("latest stable")
    |> shouldEqual true

    File.Exists(scenarioTempPath "i001240-bootstrapper" </> "paket.exe")
    |> shouldEqual true