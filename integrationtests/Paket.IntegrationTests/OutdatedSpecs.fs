module Paket.IntegrationTests.OutdatedSpecs

open Fake
open System
open NUnit.Framework
open FsUnit
open System
open System.IO
open Pri.LongPath
open System.Diagnostics

[<Test>]
let ``#183 outdated without params``() =
    let msg = paket "outdated" "i000183-outdated-with-special-parameters"
    msg |> shouldContainText "Newtonsoft.Json 6.0.7 -> 6.0.8"
    msg |> shouldContainText "FSharp.Formatting 2.4 ->"

[<Test>]
[<Platform "Mono">] // PATH TOO LONG on Windows...
let ``#183 outdated --ignore-constraint``() =
    let msg = paket "outdated --ignore-constraints" "i000183-outdated-with-special-parameters"
    msg.Contains("Newtonsoft.Json 6.0.7 -> 6.0.8") |> shouldEqual false


[<Test>]
[<Platform "Mono">] // PATH TOO LONG on Windows...
let ``#183 outdated --include-prereleases``() =
    let msg = paket "outdated --include-prereleases" "i000183-outdated-with-special-parameters"
    msg |> shouldContainText "Newtonsoft.Json 6.0.7 ->"
    msg.Contains("Newtonsoft.Json 6.0.7 -> 6.0.8") |> shouldEqual false