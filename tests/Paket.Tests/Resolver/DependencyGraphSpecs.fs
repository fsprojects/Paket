module Paket.DependencyGraphSpecs

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers
open Paket.Domain

let graph = 
  OfSimpleGraph [
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

let graph2 =
  OfSimpleGraph [
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

let graphWithoutAnyDependencyVersion = 
  OfSimpleGraph [
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

let graphWithoutAnyTopLevelVersion =
  OfSimpleGraph [
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

let graphWithMissingDependency = 
  OfSimpleGraph [
   "P44","9.44.25", []
   "P44","43.24.27.26", ["P33",VersionRequirement(VersionRange.Exactly "21.30.42",PreReleaseStatus.No)] 
  ]

[<Test>]
let ``should solve graph with missing specific dependency``() = 
    let resolved = resolve graphWithMissingDependency ["P44",VersionRange.AtLeast "9.44.25" ]
    getVersion resolved.[PackageName "P44"] |> shouldEqual "9.44.25"




[<Test>]
let ``should solve strange graph``() = 
    let graph = 
      OfSimpleGraph [
        "P1","10.11.11", ["P7", VersionRequirement (VersionRange.AtMost "4.2.11.10",PreReleaseStatus.No)]
        "P3","1.1.3",    ["P8", VersionRequirement (VersionRange.AtMost "0.2.8",PreReleaseStatus.No)]
        "P3","5.5.7.9", ["P1", VersionRequirement (VersionRange.AtMost "10.11.11",PreReleaseStatus.No)]
        "P7","4.2.11.10", []
        "P7","10.3.5.7", []
        "P7","11.10.10.3", []
      ]

    let resolved = resolve graph ["P3",VersionRange.AtLeast "0"; "P7",VersionRange.AtMost "11.10.10.3"]
    getVersion resolved.[PackageName "P1"] |> shouldEqual "10.11.11" 
    getVersion resolved.[PackageName "P3"] |> shouldEqual "5.5.7.9"
    getVersion resolved.[PackageName "P7"] |> shouldEqual "4.2.11.10"