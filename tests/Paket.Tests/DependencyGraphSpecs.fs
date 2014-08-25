module Paket.DependencyGraphSpecs

open Paket
open Paket.DependencyGraph
open NUnit.Framework
open System.Collections.Generic
open FsUnit

let graph = new Dictionary<string*string,(string*VersionRange) list>()
graph.Add(("FAKE","3.3"),[("A",VersionRange.AtLeast "3.0")])
graph.Add(("FAKE","3.7"),[("A",VersionRange.AtLeast "3.1"); ("B",VersionRange.Exactly "1.1")])
graph.Add(("FAKE","4.0"),[("A",VersionRange.AtLeast "3.3"); ("B",VersionRange.Exactly "1.3")])

graph.Add(("A","3.0"),[("B",VersionRange.AtLeast "1.0")])
graph.Add(("A","3.1"),[("B",VersionRange.AtLeast "1.0")])
graph.Add(("A","3.3"),[("B",VersionRange.AtLeast "1.0")])

graph.Add(("B","1.1"),[])
graph.Add(("B","1.2"),[])
graph.Add(("B","1.3"),["C",VersionRange.AtLeast "1.0"])

graph.Add(("C","1.0"),[])
graph.Add(("C","1.1"),[])

graph.Add(("D","1.0"),[])
graph.Add(("D","1.1"),[])

let discovery = 
  { new IDiscovery with
      member __.GetDirectDependencies(package,version) = graph.[package,version] |> Map.ofList
      member __.GetVersions package = graph.Keys |> Seq.filter (fun (k,_) -> k = package) |> Seq.map snd }

[<Test>]
let ``should analyze simple node``() = 
    let node = analyzeNode discovery ("FAKE",VersionRange.AtLeast "3.3")
    node.Version |> shouldEqual "4.0"
    node.Dependencies.["A"] |> shouldEqual (VersionRange.AtLeast "3.3")
    node.Dependencies.["B"] |> shouldEqual (VersionRange.Exactly "1.3")
    node.Dependencies.["C"] |> shouldEqual (VersionRange.AtLeast "1.0")

[<Test>]
let ``should analyze graph one level deep``() = 
    let node = analyzeGraph discovery ("FAKE",VersionRange.AtLeast "3.3")
    node.Version |> shouldEqual "4.0"
    node.Dependencies.["A"] |> shouldEqual (VersionRange.AtLeast "3.3")
    node.Dependencies.["B"] |> shouldEqual (VersionRange.Exactly "1.3")
    node.Dependencies.["C"] |> shouldEqual (VersionRange.AtLeast "1.0")

    node.Dependencies.ContainsKey "D" |> shouldEqual false