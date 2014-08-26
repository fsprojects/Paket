module Paket.DependencyGraphSpecs

open Paket
open Paket.DependencyGraph
open NUnit.Framework
open FsUnit

let graph = [
    "FAKE","3.3",[("A",VersionRange.AtLeast "3.0")]
    "FAKE","3.7",[("A",VersionRange.AtLeast "3.1"); ("B",VersionRange.Exactly "1.1")]
    "FAKE","4.0",[("A",VersionRange.AtLeast "3.3"); ("B",VersionRange.Exactly "1.3"); ("E",VersionRange.AtLeast "2.0")]

    "A","3.0",[("B",VersionRange.AtLeast "1.0")]
    "A","3.1",[("B",VersionRange.AtLeast "1.0")]
    "A","3.3",[("B",VersionRange.AtLeast "1.0")]

    "B","1.1",[]
    "B","1.2",[]
    "B","1.3",["C",VersionRange.AtLeast "1.0"]

    "C","1.0",[]
    "C","1.1",[]

    "D","1.0",[]
    "D","1.1",[]

    "E","1.0",[]
    "E","1.1",[]
    "E","2.0",[]
    "E","2.1",[("F",VersionRange.AtLeast "1.0")]

    "F","1.0",[]
    "F","1.1",[("G",VersionRange.AtLeast "1.0")]

    "G","1.0",[]
]

let discovery = DictionaryDiscovery graph

[<Test>]
let ``should analyze graph one level deep``() = 
    let node = Resolve(discovery, Map.add "FAKE" (VersionRange.AtLeast "3.3") Map.empty)
    node.["FAKE"] |> shouldEqual "4.0"
    node.["A"] |> shouldEqual "3.3"
    node.["B"] |> shouldEqual "1.3"
    node.["C"] |> shouldEqual "1.1"

    node.ContainsKey "D" |> shouldEqual false

[<Test>]
let ``should analyze graph completly``() = 
    let node = Resolve(discovery, Map.add "FAKE" (VersionRange.AtLeast "3.3") Map.empty)
    node.["FAKE"] |> shouldEqual "4.0"
    node.["E"] |> shouldEqual "2.1"
    node.["F"] |> shouldEqual "1.1"
    node.["G"] |> shouldEqual "1.0"