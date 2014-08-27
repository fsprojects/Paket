module Paket.CyclicGraphSpecs

open Paket
open NUnit.Framework
open FsUnit

open TestHelpers

let graph = [
    "A","3.0",[("B",VersionRange.AtLeast "1.0")]
    "A","3.1",[("B",VersionRange.AtLeast "1.0")]
    "A","3.3",[("B",VersionRange.AtLeast "1.0")]

    "B","1.0",[]
    "B","1.1",[]
    "B","1.2",["A",VersionRange.AtLeast "3.3"]
]

[<Test>]
let ``should analyze graph completely``() =
    let resolved = resolve graph ["A",VersionRange.AtLeast "1.0"]
    getVersion resolved.["A"] |> shouldEqual "3.3"
    getVersion resolved.["B"] |> shouldEqual "1.2"