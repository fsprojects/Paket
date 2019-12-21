module Paket.IntegrationTests.OutdatedSpecs

open Fake
open System
open NUnit.Framework
open FsUnit
open System
open System.IO
open System.Diagnostics

[<Test>]
let ``#183 outdated without params``() =
    let cleanup, msg = paket "outdated" "i000183-outdated-with-special-parameters"
    use cleanup = cleanup
    msg |> shouldContainText "Newtonsoft.Json 6.0.7 -> 6.0.8"
    msg |> shouldContainText "FSharp.Formatting 2.4 ->"

[<Test>]
#if NO_UNIT_PLATFORMATTRIBUTE
[<Ignore "PlatformAttribute not supported by netstandard NUnit">]
#else
[<Platform "Mono">] // PATH TOO LONG on Windows...
#endif
let ``#183 outdated --ignore-constraint``() =
    let cleanup, msg = paket "outdated --ignore-constraints" "i000183-outdated-with-special-parameters"
    use cleanup = cleanup
    msg.Contains("Newtonsoft.Json 6.0.7 -> 6.0.8") |> shouldEqual false


[<Test>]
#if NO_UNIT_PLATFORMATTRIBUTE
[<Ignore "PlatformAttribute not supported by netstandard NUnit">]
#else
[<Platform "Mono">] // PATH TOO LONG on Windows...
#endif
let ``#183 outdated --include-prereleases``() =
    let cleanup, msg = paket "outdated --include-prereleases" "i000183-outdated-with-special-parameters"
    use cleanup = cleanup
    msg |> shouldContainText "Newtonsoft.Json 6.0.7 ->"
    msg.Contains("Newtonsoft.Json 6.0.7 -> 6.0.8") |> shouldEqual false