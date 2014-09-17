module Paket.DependencyGraphSpecs

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers

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
    let resolved = resolve graph ["FAKE",VersionRange.AtLeast "3.3"]
    getVersion resolved.["FAKE"] |> shouldEqual "4.0"
    getVersion resolved.["A"] |> shouldEqual "3.3"
    getVersion resolved.["B"] |> shouldEqual "1.3"
    getVersion resolved.["C"] |> shouldEqual "1.1"

    resolved.ContainsKey "D" |> shouldEqual false

[<Test>]
let ``should analyze graph completly``() = 
    let resolved = resolve graph ["FAKE",VersionRange.AtLeast "3.3"]
    getVersion resolved.["FAKE"] |> shouldEqual "4.0"
    getVersion resolved.["E"] |> shouldEqual "2.1"
    getVersion resolved.["F"] |> shouldEqual "1.1"
    getVersion resolved.["G"] |> shouldEqual "1.0"

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
    let resolved = resolve graph2 ["A",VersionRange.AtLeast "1.0"]
    getVersion resolved.["A"] |> shouldEqual "1.1"
    getVersion resolved.["B"] |> shouldEqual "1.1"
    getVersion resolved.["C"] |> shouldEqual "2.4"
    getVersion resolved.["D"] |> shouldEqual "1.5"
    
    resolved.ContainsKey "E" |> shouldEqual false

[<Test>]
let ``should analyze graph2 completely with multiple starting nodes``() =
    let resolved = resolve graph2 ["A",VersionRange.AtLeast "1.0"; "E",VersionRange.AtLeast "1.0"]
    getVersion resolved.["A"] |> shouldEqual "1.1"
    getVersion resolved.["B"] |> shouldEqual "1.1"
    getVersion resolved.["C"] |> shouldEqual "2.4"
    getVersion resolved.["D"] |> shouldEqual "1.5"
    getVersion resolved.["E"] |> shouldEqual "1.0"