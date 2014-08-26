module Paket.ConflictGraphSpecs

open Paket
open Paket.DependencyGraph
open NUnit.Framework
open FsUnit

let graph = [
    "A","1.0",["B",VersionRange.Exactly "1.1";"C",VersionRange.Exactly "2.4"]
    "B","1.1",["E",VersionRange.Exactly "4.3";"D",VersionRange.Exactly "1.4"]
    "C","2.4",["F",VersionRange.Exactly "1.2";"D",VersionRange.Exactly "1.6"]
    "D","1.4",[]
    "D","1.6",[]
    "E","4.3",[]
    "F","1.2",[]
]

[<Test>]
let ``should analyze graph completely``() =
    let resolved = Resolve(DictionaryDiscovery graph, ["A",VersionRange.AtLeast "1.0"])
    resolved.["A"] |> shouldEqual (VersionRange.Exactly "1.0")
    resolved.["B"] |> shouldEqual (VersionRange.Exactly "1.1")
    resolved.["C"] |> shouldEqual (VersionRange.Exactly "2.4")
    resolved.["D"] |> shouldEqual (VersionRange.Conflict (VersionRange.Exactly "1.4",VersionRange.Exactly "1.6"))
    resolved.["E"] |> shouldEqual (VersionRange.Exactly "4.3")
    resolved.["F"] |> shouldEqual (VersionRange.Exactly "1.2")