module Paket.CyclicGraphSpecs

open Paket
open NUnit.Framework
open FsUnit

open TestHelpers
open Paket.Domain

let graph = [
    "A","3.0",[("B",VersionRequirement.New(VersionRange.AtLeast "1.0",PreReleaseStatus.No) )]
    "A","3.1",[("B",VersionRequirement.New(VersionRange.AtLeast "1.0",PreReleaseStatus.No))]
    "A","3.3",[("B",VersionRequirement.New(VersionRange.AtLeast "1.0",PreReleaseStatus.No))]

    "B","1.0",[]
    "B","1.1",[]
    "B","1.2",["A",VersionRequirement.New(VersionRange.AtLeast "3.3",PreReleaseStatus.No)]
]

[<Test>]
let ``should analyze graph completely``() =
    let resolved = resolve graph ["A",VersionRange.AtLeast "1.0"]
    getVersion resolved.[NormalizedPackageName (PackageName "A")] |> shouldEqual "3.3"
    getVersion resolved.[NormalizedPackageName (PackageName "B")] |> shouldEqual "1.2"