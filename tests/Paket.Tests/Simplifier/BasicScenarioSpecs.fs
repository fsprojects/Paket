module Paket.Simplifier.BasicScenarioSpecs

open Paket

open NUnit.Framework
open FsUnit
open System.IO
open TestHelpers

let toPackages =
    List.map (fun (name,ver,deps) ->
        { ResolvedPackage.Name = name
          Version = SemVer.parse ver 
          Source = Nuget(Constants.DefaultNugetStream)
          Dependencies = deps
                               |> List.map (fun (name, verRan) ->
                                                name, Nuget.parseVersionRange verRan)
        })

let graph1 = 
    ["A", "3.3.0", ["B", "3.3.0"; "C", "1.0"]
     "B", "3.3.0", []
     "C", "1.0", []
     "D", "2.1", ["B", "3.0"; "C", "1.0"]] |> toPackages

let depFile1 = """
source http://nuget.org/api/v2

nuget A 3.3.0
nuget B 3.3.1
nuget C 1.0
nuget D 2.1"""

let cfg = DependenciesFile.FromCode(noSha1,depFile1)

let refFiles1 = [
    FileInfo("c:\dummy\1"), [|"A";"B";"C";"D"|]
    FileInfo("c:\dummy\2"), [|"B";"C"|]
]

[<Test>]
let ``should remove one level deep indirect dependencies from dep and ref files``() = 
    let depFile,refFiles = Simplifier.Analyze(graph1, cfg, refFiles1)
    
    depFile.Packages |> List.length |> shouldEqual [|"A";"D"|].Length
    depFile.DirectDependencies.["A"] |> shouldEqual (VersionRange.Exactly "3.3.0")
    depFile.DirectDependencies.["D"] |> shouldEqual (VersionRange.Exactly "2.1")

    refFiles.Head |> snd |> shouldEqual [|"A";"D"|]
    refFiles.Tail.Head |> snd |> shouldEqual [|"B";"C"|]


let graph2 = 
    ["A", "1.0", ["b", "1.5"]
     "b", "1.5", ["D", "2.0"]
     "C", "2.0", ["e", "3.0"]
     "d", "2.0", ["E", "3.0"]
     "E", "3.0", ["f", "4.0"]
     "F", "4.0", []] |> toPackages

let depFile2 = """
source http://nuget.org/api/v2

nuget A 1.0
nuget B 1.5
nuget c 2.0
nuget D 2.0
nuget E 3.0
nuget f 4.0""" 

let cfg2 = DependenciesFile.FromCode(noSha1,depFile2)

let refFiles2 = [
    FileInfo("c:\dummy\1"), [|"A";"B";"C";"D";"F"|]
    FileInfo("c:\dummy\2"), [|"C";"D";"E"|]
]

[<Test>]
let ``should remove all indirect dependencies from dep file recursively``() =
    let depFile,refFiles  = Simplifier.Analyze(graph2, cfg2, refFiles2)
    
    depFile.Packages |> List.length |> shouldEqual [|"A";"c"|].Length
    depFile.DirectDependencies.["A"] |> shouldEqual (VersionRange.Exactly "1.0")
    depFile.DirectDependencies.["c"] |> shouldEqual (VersionRange.Exactly "2.0")

    refFiles.Head |> snd |> shouldEqual [|"A";"C"|]
    refFiles.Tail.Head |> snd |> shouldEqual [|"C";"D"|]