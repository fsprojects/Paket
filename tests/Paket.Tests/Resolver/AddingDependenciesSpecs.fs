module Paket.AddingDependenciesSpecs

open Paket
open NUnit.Framework
open FsUnit

open TestHelpers
open Paket.Domain

let graph =
  OfSimpleGraph [
    "Castle.Windsor","3.2.1",[("Castle.Core",VersionRequirement(VersionRange.AtLeast "3.2.0",PreReleaseStatus.No) )]
    "Castle.Windsor","3.3.0",[("Castle.Core",VersionRequirement(VersionRange.AtLeast "3.3.0",PreReleaseStatus.No) )]
    "Castle.Core","3.2.0",[]
    "Castle.Core","3.2.1",[]
    "Castle.Core","3.2.2",[]
    "Castle.Core","3.3.0",[]
  ]

[<Test>]
let ``should find castle.core alone``() =
    let resolved = resolve graph ["Castle.Core",VersionRange.Between("3.2","3.3")]
    getVersion resolved.[PackageName "Castle.Core"] |> shouldEqual "3.2.2"
    resolved.ContainsKey(PackageName "Castle.Windsor") |> shouldEqual false

[<Test>]
let ``should find castle.core + castle.windsor alone``() =
    let resolved = resolve graph ["Castle.Core",VersionRange.Between("3.2","3.3");"Castle.Windsor",VersionRange.AtLeast "0"]
    getVersion resolved.[PackageName "Castle.Core"] |> shouldEqual "3.2.2"
    getVersion resolved.[PackageName "Castle.Windsor"] |> shouldEqual "3.2.1"

[<Test>]
let ``should find castle.core with explicit version + castle.windsor alone``() =
    let resolved = resolve graph ["Castle.Core",VersionRange.Exactly("3.2.2");"Castle.Windsor",VersionRange.AtLeast "0"]
    getVersion resolved.[PackageName "Castle.Core"] |> shouldEqual "3.2.2"
    getVersion resolved.[PackageName "Castle.Windsor"] |> shouldEqual "3.2.1"