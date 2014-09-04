module Paket.ConflictGraphSpecs

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers

let graph = 
    [ "A", "1.0", 
      [ "B", VersionRange.Exactly "1.1"
        "C", VersionRange.Exactly "2.4" ]
      "B", "1.1", 
      [ "E", VersionRange.Exactly "4.3"
        "D", VersionRange.Exactly "1.4" ]
      "C", "2.4", 
      [ "F", VersionRange.Exactly "1.2"
        "D", VersionRange.Exactly "1.6" ]
      "D", "1.4", []
      "D", "1.6", []
      "E", "4.3", []
      "F", "1.2", [] ]

let defaultPackage = { Name = ""; VersionRange = VersionRange.Exactly "1.0"; SourceType = Nuget; DirectDependencies = []; ResolverStrategy = ResolverStrategy.Max; Source = "" }

[<Test>]
let ``should analyze graph and report conflict``() = 
    let resolved = resolve graph [ "A", VersionRange.AtLeast "1.0" ]
    getVersion resolved.["A"] |> shouldEqual "1.0"
    getVersion resolved.["B"] |> shouldEqual "1.1"
    getVersion resolved.["C"] |> shouldEqual "2.4"
    let conflict = 
        FromPackage { Defining = { defaultPackage with Name = "B"; VersionRange = VersionRange.Exactly "1.1" }
                      Referenced = { defaultPackage with Name = "D"; VersionRange = VersionRange.Exactly "1.4" } },
        FromPackage { Defining = { defaultPackage with Name = "C"; VersionRange = VersionRange.Exactly "2.4" }
                      Referenced = { defaultPackage with Name = "D"; VersionRange = VersionRange.Exactly "1.6" } }

    resolved.["D"] |> shouldEqual (ResolvedDependency.Conflict conflict)
    getVersion resolved.["E"] |> shouldEqual "4.3"
    getDefiningPackage resolved.["E"] |> shouldEqual "B"
    getDefiningVersion resolved.["E"] |> shouldEqual "1.1"
    getVersion resolved.["F"] |> shouldEqual "1.2"
    getDefiningPackage resolved.["F"] |> shouldEqual "C"
    getDefiningVersion resolved.["F"] |> shouldEqual "2.4"

let graph2 = 
    [ "A", "1.0", 
      [ "B", VersionRange.Exactly "1.1"
        "C", VersionRange.Exactly "2.4" ]
      "B", "1.1", [ "D", VersionRange.Between("1.4", "1.5") ]
      "C", "2.4", [ "D", VersionRange.Between("1.6", "1.7") ]
      "D", "1.4", []
      "D", "1.6", [] ]

[<Test>]
let ``should analyze graph2 and report conflict``() = 
    let resolved = resolve graph2 [ "A", VersionRange.AtLeast "1.0" ]
    getVersion resolved.["A"] |> shouldEqual "1.0"
    getVersion resolved.["B"] |> shouldEqual "1.1"
    getVersion resolved.["C"] |> shouldEqual "2.4"
    let conflict = 
        FromPackage { Defining = { defaultPackage with Name = "B"; VersionRange = VersionRange.Exactly "1.1" }
                      Referenced = { defaultPackage with Name = "D"; VersionRange = VersionRange.Between("1.4", "1.5") } },
        FromPackage { Defining = { defaultPackage with Name = "C"; VersionRange = VersionRange.Exactly "2.4" }
                      Referenced = { defaultPackage with Name = "D"; VersionRange = VersionRange.Between("1.6", "1.7") } }
    resolved.["D"] |> shouldEqual (ResolvedDependency.Conflict conflict)
