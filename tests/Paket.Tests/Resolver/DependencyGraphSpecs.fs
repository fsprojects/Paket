module Paket.DependencyGraphSpecs

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers
open Paket.Domain

let graph = [
    "FAKE","3.3",[("A",VersionRequirement(VersionRange.AtLeast "3.0",PreReleaseStatus.No))]
    "FAKE","3.7",[("A",VersionRequirement(VersionRange.AtLeast "3.1",PreReleaseStatus.No)); ("B",VersionRequirement(VersionRange.Exactly "1.1",PreReleaseStatus.No))]
    "FAKE","4.0",[("A",VersionRequirement(VersionRange.AtLeast "3.3",PreReleaseStatus.No)); ("B",VersionRequirement(VersionRange.Exactly "1.3",PreReleaseStatus.No)); ("E",VersionRequirement(VersionRange.AtLeast "2.0",PreReleaseStatus.No))]

    "A","3.0",[("B",VersionRequirement(VersionRange.AtLeast "1.0",PreReleaseStatus.No))]
    "A","3.1",[("B",VersionRequirement(VersionRange.AtLeast "1.0",PreReleaseStatus.No))]
    "A","3.3",[("B",VersionRequirement(VersionRange.AtLeast "1.0",PreReleaseStatus.No))]

    "B","1.1",[]
    "B","1.2",[]
    "B","1.3",["C",VersionRequirement(VersionRange.AtLeast "1.0",PreReleaseStatus.No)]

    "C","1.0",[]
    "C","1.1",[]

    "D","1.0",[]
    "D","1.1",[]

    "E","1.0",[]
    "E","1.1",[]
    "E","2.0",[]
    "E","2.1",[("F",VersionRequirement(VersionRange.AtLeast "1.0",PreReleaseStatus.No))]

    "F","1.0",[]
    "F","1.1",[("G",VersionRequirement(VersionRange.AtLeast "1.0",PreReleaseStatus.No))]

    "G","1.0",[]
]


[<Test>]
let ``should analyze graph one level deep``() = 
    let resolved = resolve graph ["FAKE",VersionRange.AtLeast "3.3"]
    getVersion resolved.[PackageName "FAKE"] |> shouldEqual "4.0"
    getVersion resolved.[PackageName "A"] |> shouldEqual "3.3"
    getVersion resolved.[PackageName "B"] |> shouldEqual "1.3"
    getVersion resolved.[PackageName "C"] |> shouldEqual "1.1"

    resolved.ContainsKey (PackageName "D") |> shouldEqual false

[<Test>]
let ``should analyze graph completly``() = 
    let resolved = resolve graph ["FAKE",VersionRange.AtLeast "3.3"]
    getVersion resolved.[PackageName "FAKE"] |> shouldEqual "4.0"
    getVersion resolved.[PackageName "E"] |> shouldEqual "2.1"
    getVersion resolved.[PackageName "F"] |> shouldEqual "1.1"
    getVersion resolved.[PackageName "G"] |> shouldEqual "1.0"

let graph2 = [
    "A","1.0",["B",VersionRequirement(VersionRange.Exactly "1.1",PreReleaseStatus.No);"C",VersionRequirement(VersionRange.Exactly "2.4",PreReleaseStatus.No)]
    "A","1.1",["B",VersionRequirement(VersionRange.Exactly "1.1",PreReleaseStatus.No);"C",VersionRequirement(VersionRange.Exactly "2.4",PreReleaseStatus.No)]
    "B","1.1",["D",VersionRequirement(VersionRange.Between("1.3","1.6"),PreReleaseStatus.No)]
    "C","2.4",["D",VersionRequirement(VersionRange.Between("1.4","1.7"),PreReleaseStatus.No)]
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
    getVersion resolved.[PackageName "A"] |> shouldEqual "1.1"
    getVersion resolved.[PackageName "B"] |> shouldEqual "1.1"
    getVersion resolved.[PackageName "C"] |> shouldEqual "2.4"
    getVersion resolved.[PackageName "D"] |> shouldEqual "1.5"
    
    resolved.ContainsKey (PackageName "E") |> shouldEqual false

[<Test>]
let ``should analyze graph2 completely with multiple starting nodes``() =
    let resolved = resolve graph2 ["A",VersionRange.AtLeast "1.0"; "E",VersionRange.AtLeast "1.0"]
    getVersion resolved.[PackageName "A"] |> shouldEqual "1.1"
    getVersion resolved.[PackageName "B"] |> shouldEqual "1.1"
    getVersion resolved.[PackageName "C"] |> shouldEqual "2.4"
    getVersion resolved.[PackageName "D"] |> shouldEqual "1.5"
    getVersion resolved.[PackageName "E"] |> shouldEqual "1.0"

let graphWithoutAnyDependencyVersion = [
    "A","3.0",[("B",VersionRequirement(VersionRange.AtLeast "2.0",PreReleaseStatus.No))]
]

[<Test>]
let ``should report missing versions``() = 
    try
        resolve graphWithoutAnyDependencyVersion ["A",VersionRange.AtLeast "0"] |> ignore
        failwith "expected error"
    with exn ->
        if not <| exn.Message.Contains("package B") then
            reraise()

let graphWithoutAnyTopLevelVersion = [
    "A","3.0",[]
]

[<Test>]
let ``should report missing top-level versions``() = 
    try
        resolve graphWithoutAnyTopLevelVersion ["A",VersionRange.LessThan(SemVer.Parse "1.0")] |> ignore
        failwith "expected error"
    with exn ->
        if not <| exn.Message.Contains("package A") then
            reraise()