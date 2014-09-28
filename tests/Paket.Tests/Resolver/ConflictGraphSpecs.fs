module Paket.ConflictGraphSpecs

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers

let graph = 
    [ "A", "1.0", 
      [ "B", VersionRequirement(VersionRange.Exactly "1.1",PreReleaseStatus.No)
        "C", VersionRequirement(VersionRange.Exactly "2.4",PreReleaseStatus.No) ]
      "B", "1.1", 
      [ "E", VersionRequirement(VersionRange.Exactly "4.3",PreReleaseStatus.No)
        "D", VersionRequirement(VersionRange.Exactly "1.4",PreReleaseStatus.No) ]
      "C", "2.4", 
      [ "F", VersionRequirement(VersionRange.Exactly "1.2",PreReleaseStatus.No)
        "D", VersionRequirement(VersionRange.Exactly "1.6",PreReleaseStatus.No) ]
      "D", "1.4", []
      "D", "1.6", []
      "E", "4.3", []
      "F", "1.2", [] ]

let defaultPackage = 
    { Name = ""
      Parent = PackageRequirementSource.DependenciesFile ""
      VersionRequirement = VersionRequirement(VersionRange.Exactly "1.0", PreReleaseStatus.No)
      Sources = [ PackageSource.NugetSource "" ]
      ResolverStrategy = ResolverStrategy.Max }

[<Test>]
let ``should analyze graph and report conflict``() = 
    match safeResolve graph [ "A", VersionRange.AtLeast "1.0" ] with
    | Ok _ -> failwith "we expected an error"
    | Conflict(_,stillOpen) ->
        let conflicting = stillOpen |> Seq.head 
        conflicting.Name |> shouldEqual "D"
        conflicting.VersionRequirement.Range |> shouldEqual (VersionRange.Exactly "1.6")

let graph2 = 
    [ "A", "1.0", 
      [ "B", VersionRequirement(VersionRange.Exactly "1.1",PreReleaseStatus.No)
        "C", VersionRequirement(VersionRange.Exactly "2.4",PreReleaseStatus.No) ]
      "B", "1.1", [ "D", VersionRequirement(VersionRange.Between("1.4", "1.5"),PreReleaseStatus.No) ]
      "C", "2.4", [ "D", VersionRequirement(VersionRange.Between("1.6", "1.7"),PreReleaseStatus.No) ]
      "D", "1.4", []
      "D", "1.6", [] ]

[<Test>]
let ``should analyze graph2 and report conflict``() = 
    match safeResolve graph2 [ "A", VersionRange.AtLeast "1.0" ] with
    | Ok _ -> failwith "we expected an error"
    | Conflict(_,stillOpen) ->
        let conflicting = stillOpen |> Seq.head 
        conflicting.Name |> shouldEqual "D"
        conflicting.VersionRequirement.Range |> shouldEqual (VersionRange.Between("1.6", "1.7"))