module Paket.IntegrationTests.RepoToolSpecs

open NUnit.Framework
open Fake
open FsUnit

[<Test>]
let ``#3000 repo tool should work after restore``() =
    let scenario = "i003000-repo-tool"
    prepare scenario
    paket "restore" scenario |> ignore

    ()



