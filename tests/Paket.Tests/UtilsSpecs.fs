module Paket.UtilsSpecs

open Paket
open NUnit.Framework
open FsUnit

[<Test>]
let ``createRelativePath should handle spaces``() =
    "C:/some file" |> createRelativePath "C:/a/b" |> shouldEqual "..\\some file"