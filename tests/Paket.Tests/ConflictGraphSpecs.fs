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

let defaultPackage = { Name = ""; VersionRange = VersionRange.Exactly "1.0"; Sources = [Nuget ""]; ResolverStrategy = ResolverStrategy.Max }

[<Test>]
let ``should analyze graph and report conflict``() = 
    match safeResolve graph [ "A", VersionRange.AtLeast "1.0" ] with
    | Ok _ -> failwith "we expected an error"
    | Conflict(_,stillOpen) ->
        let conflicting = stillOpen |> Seq.head 
        conflicting.Name |> shouldEqual "D"
        conflicting.VersionRange |> shouldEqual (VersionRange.Exactly "1.6")

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
    match safeResolve graph2 [ "A", VersionRange.AtLeast "1.0" ] with
    | Ok _ -> failwith "we expected an error"
    | Conflict(_,stillOpen) ->
        let conflicting = stillOpen |> Seq.head 
        conflicting.Name |> shouldEqual "D"
        conflicting.VersionRange |> shouldEqual (VersionRange.Between("1.6", "1.7"))