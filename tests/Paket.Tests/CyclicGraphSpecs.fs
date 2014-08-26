module Paket.CyclicGraphSpecs

open Paket
open Paket.DependencyGraph
open NUnit.Framework
open FsUnit

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
    let resolved = Resolve(DictionaryDiscovery graph, ["A",VersionRange.AtLeast "1.0"])
    resolved.["A"] |> shouldEqual (ResolvedVersion.Resolved "3.3")
    resolved.["B"] |> shouldEqual (ResolvedVersion.Resolved "1.2")