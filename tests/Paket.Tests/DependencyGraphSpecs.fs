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
    let resolved = Resolve(discovery, ["FAKE",VersionRange.AtLeast "3.3"])
    resolved.["FAKE"] |> shouldEqual (ResolvedVersion.Resolved "4.0")
    resolved.["A"] |> shouldEqual (ResolvedVersion.Resolved "3.3")
    resolved.["B"] |> shouldEqual (ResolvedVersion.Resolved "1.3")
    resolved.["C"] |> shouldEqual (ResolvedVersion.Resolved "1.1")

    resolved.ContainsKey "D" |> shouldEqual false

[<Test>]
let ``should analyze graph completly``() = 
    let resolved = Resolve(discovery, ["FAKE",VersionRange.AtLeast "3.3"])
    resolved.["FAKE"] |> shouldEqual (ResolvedVersion.Resolved "4.0")
    resolved.["E"] |> shouldEqual (ResolvedVersion.Resolved "2.1")
    resolved.["F"] |> shouldEqual (ResolvedVersion.Resolved "1.1")
    resolved.["G"] |> shouldEqual (ResolvedVersion.Resolved "1.0")