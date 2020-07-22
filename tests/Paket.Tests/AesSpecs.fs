module Paket.AesSpecs

open FsUnit
open NUnit.Framework
open Paket.Core.Common

[<Test>]
let ``should be able to decrypt encrypted password`` () =
    let password = (PlainTextPassword "Super Secret 123!")
    Aes.encrypt password
    ||> Aes.decrypt
    |> shouldEqual password