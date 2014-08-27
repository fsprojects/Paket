module Paket.DependencyGraphSpecs

open Paket
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


[<Test>]
let ``should analyze graph one level deep``() = 
    let resolved = Resolver.Resolve(Discovery.DictionaryDiscovery graph, ["FAKE",VersionRange.AtLeast "3.3"])
    resolved.["FAKE"] |> shouldEqual (ResolvedVersion.Resolved "4.0")
    resolved.["A"] |> shouldEqual (ResolvedVersion.Resolved "3.3")
    resolved.["B"] |> shouldEqual (ResolvedVersion.Resolved "1.3")
    resolved.["C"] |> shouldEqual (ResolvedVersion.Resolved "1.1")

    resolved.ContainsKey "D" |> shouldEqual false

[<Test>]
let ``should analyze graph completly``() = 
    let resolved = Resolver.Resolve(Discovery.DictionaryDiscovery graph, ["FAKE",VersionRange.AtLeast "3.3"])
    resolved.["FAKE"] |> shouldEqual (ResolvedVersion.Resolved "4.0")
    resolved.["E"] |> shouldEqual (ResolvedVersion.Resolved "2.1")
    resolved.["F"] |> shouldEqual (ResolvedVersion.Resolved "1.1")
    resolved.["G"] |> shouldEqual (ResolvedVersion.Resolved "1.0")

let graph2 = [
    "A","1.0",["B",VersionRange.Exactly "1.1";"C",VersionRange.Exactly "2.4"]
    "A","1.1",["B",VersionRange.Exactly "1.1";"C",VersionRange.Exactly "2.4"]
    "B","1.1",["D",VersionRange.Between("1.3","1.6")]
    "C","2.4",["D",VersionRange.Between("1.4","1.7")]
    "D","1.3",[]
    "D","1.4",[]
    "D","1.5",[]
    "D","1.6",[]
    "D","1.7",[]
    "E","1.0",[]
]

[<Test>]
let ``should analyze graph2 completely``() =
    let resolved = Resolver.Resolve(Discovery.DictionaryDiscovery graph2, ["A",VersionRange.AtLeast "1.0"])
    resolved.["A"] |> shouldEqual (ResolvedVersion.Resolved "1.1")
    resolved.["B"] |> shouldEqual (ResolvedVersion.Resolved "1.1")
    resolved.["C"] |> shouldEqual (ResolvedVersion.Resolved "2.4")
    resolved.["D"] |> shouldEqual (ResolvedVersion.Resolved "1.5")

    resolved.ContainsKey "E" |> shouldEqual false

[<Test>]
let ``should analyze graph2 completely with multiple starting nodes``() =
    let resolved = Resolver.Resolve(Discovery.DictionaryDiscovery graph2, ["A",VersionRange.AtLeast "1.0"; "E",VersionRange.AtLeast "1.0"])
    resolved.["A"] |> shouldEqual (ResolvedVersion.Resolved "1.1")
    resolved.["B"] |> shouldEqual (ResolvedVersion.Resolved "1.1")
    resolved.["C"] |> shouldEqual (ResolvedVersion.Resolved "2.4")
    resolved.["D"] |> shouldEqual (ResolvedVersion.Resolved "1.5")
    resolved.["E"] |> shouldEqual (ResolvedVersion.Resolved "1.0")