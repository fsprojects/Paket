module Paket.Simplifier.BasicScenarioSpecs

open Paket

open NUnit.Framework
open FsUnit
open Paket.Domain
open Paket.TestHelpers

let lookup1 = 
    [ "A", [ "B"; "C" ]
      "B", []
      "C", []
      "D", [ "B"; "C" ] ]
    |> List.map (fun (k,v) -> PackageName k |> NormalizedPackageName, v |> List.map PackageName |> Set.ofList) 
    |> Map.ofList

let depFile1 = """
source http://nuget.org/api/v2

nuget A 3.3.0
nuget B 3.3.1
nuget C 1.0
nuget D 2.1""" |> DependenciesFile.FromCode


let refFiles1 = [
    ReferencesFile.FromLines [|"A";"B";"C";"D"|]
    ReferencesFile.FromLines [|"B";"C"|]
]

[<Test>]
let ``should remove one level deep indirect dependencies from dep and ref files``() = 
    let result = Simplifier.analyze(depFile1, refFiles1, lookup1, false)
    let depFile,refFiles = result.DependenciesFileSimplifyResult |> snd, result.ReferencesFilesSimplifyResult |> List.map snd
    
    depFile.Packages |> List.map (fun p -> p.Name) |> shouldEqual [PackageName"A";PackageName"D"]

    refFiles.Head.NugetPackages |> shouldEqual [PackageName "A";PackageName "D"]
    refFiles.Tail.Head.NugetPackages |> shouldEqual [PackageName "B";PackageName "C"]

let lookup2 = 
    [ "A", [ "B"; "D"; "E"; "F" ]
      "B", [ "D"; "E"; "F" ]
      "C", [ "E"; "F" ]
      "D", [ "E"; "F" ]
      "E", [ "F" ]
      "F", [ ] ]
    |> List.map (fun (k,v) -> PackageName k |> NormalizedPackageName, v |> List.map PackageName |> Set.ofList) 
    |> Map.ofList

let depFile2 = """
source http://nuget.org/api/v2

nuget A 1.0
nuget B 1.0
nuget C 1.0
nuget D 1.0
nuget E 1.0
nuget F 1.0""" |> DependenciesFile.FromCode

let refFiles2 = [
    ReferencesFile.FromLines [|"A";"B";"C";"D";"F"|]
    ReferencesFile.FromLines [|"C";"D";"E"|]
]

[<Test>]
let ``should remove all indirect dependencies from dep file recursively``() =
    let result = Simplifier.analyze(depFile2, refFiles2, lookup2, false)
    let depFile,refFiles = result.DependenciesFileSimplifyResult |> snd, result.ReferencesFilesSimplifyResult |> List.map snd
    
    depFile.Packages |> List.map (fun p -> p.Name) |> shouldEqual [PackageName"A";PackageName"C"]

    refFiles.Head.NugetPackages |>  shouldEqual [PackageName "A";PackageName "C"]
    refFiles.Tail.Head.NugetPackages |>  shouldEqual [PackageName "C";PackageName "D"]