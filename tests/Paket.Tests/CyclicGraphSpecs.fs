module Paket.CyclicGraphSpecs

open Paket
open Paket.DependencyGraph
open NUnit.Framework
open System.Collections.Generic
open FsUnit

let graph = new Dictionary<string*string,(string*VersionRange) list>()
graph.Add(("A","3.0"),[("B",VersionRange.AtLeast "1.0")])
graph.Add(("A","3.1"),[("B",VersionRange.AtLeast "1.0")])
graph.Add(("A","3.3"),[("B",VersionRange.AtLeast "1.0")])

graph.Add(("B","1.0"),[])
graph.Add(("B","1.1"),[])
graph.Add(("B","1.2"),["A",VersionRange.AtLeast "3.3"])

let discovery = 
  { new IDiscovery with
      member __.GetDirectDependencies(package,version) = graph.[package,version] |> Map.ofList
      member __.GetVersions package = graph.Keys |> Seq.filter (fun (k,_) -> k = package) |> Seq.map snd }

[<Test>]
let ``should analyze graph completely``() = 
    let node = AnalyzeGraph discovery ("A",VersionRange.AtLeast "3.3")
    node.["A"] |> shouldEqual (VersionRange.Exactly "3.3")
    node.["B"] |> shouldEqual (VersionRange.AtLeast "1.0")