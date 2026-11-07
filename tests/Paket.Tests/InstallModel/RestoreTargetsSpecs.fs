module Paket.InstallModel.RestoreTargetsSpecs

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers
open System.IO

let private targetsFile root = Path.Combine(root, ".paket", "Paket.Restore.targets")

let private toggleLineEndings (text: string) =
    if text.Contains "\r\n" then text.Replace("\r\n", "\n") else text.Replace("\n", "\r\n")

[<Test>]
let ``the restore targets are kept when only their line endings differ``() =
    withTempDir (fun root ->
        RestoreProcess.extractElement root "Paket.Restore.targets" |> ignore
        let path = targetsFile root
        let checkedOut = toggleLineEndings (File.ReadAllText path)
        File.WriteAllText(path, checkedOut)

        let written, _ = RestoreProcess.extractElement root "Paket.Restore.targets"

        written |> shouldEqual false
        File.ReadAllText path |> shouldEqual checkedOut)
