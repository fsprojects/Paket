module Paket.DependencyGraphSpecs

open Paket
open Paket.DependencyGraph
open NUnit.Framework
open System.Collections.Generic
open FsUnit

let packages = new Dictionary<string*string,(string*VersionRange) list>()
packages.Add(("FAKE","3.3"),[("A",VersionRange.AtLeast "3.0")])
packages.Add(("FAKE","3.7"),[("A",VersionRange.AtLeast "3.1"); ("B",VersionRange.Exactly "1.1")])
packages.Add(("FAKE","4.0"),[("A",VersionRange.AtLeast "3.3"); ("B",VersionRange.Exactly "1.3")])

let discovery = 
  { new IDiscovery with
      member __.GetDirectDependencies(package,version) = packages.[package,version] |> Map.ofList
      member __.GetVersions package = packages.Keys |> Seq.filter (fun (k,_) -> k = package) |> Seq.map snd }

[<Test>]
let ``should analyze simple node``() = 
    let node = analyzeNode discovery ("FAKE",VersionRange.AtLeast "3.3")
    node.Version |> shouldEqual "4.0"
    node.Dependencies.["A"] |> shouldEqual (VersionRange.AtLeast "3.3")
    node.Dependencies.["B"] |> shouldEqual (VersionRange.Exactly "1.3")