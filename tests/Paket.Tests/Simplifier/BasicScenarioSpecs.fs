module Paket.Simplifier.BasicScenarioSpecs

open Paket
open Paket.LockFile
open NUnit.Framework
open FsUnit
open TestHelpers
open System.IO


let lockFile1 = """
NUGET
  remote: http://nuget.org/api/v2
  specs:
    A (3.3.0)
      B (>= 3.3.0)
      C (= 1.0)
    B (3.3.1)
    C (1.0)
    D (2.1)
      B (>= 3.0)
      C (>= 1.0)""" |> toLines |> LockFile.Parse

let depFile1 = """
source http://nuget.org/api/v2

nuget A 3.3.0
nuget B 3.3.1
nuget C 1.0
nuget D 2.1""" |> DependenciesFile.FromCode

let refFiles1 = [
    FileInfo("c:\dummy\1"), [|"A";"B";"C";"D"|]
    FileInfo("c:\dummy\2"), [|"B";"C"|]
]

[<Test>]
let ``should remove one level deep indirect dependencies from dep and ref files``() = 
    let depFile,refFiles = Simplifier.Simplify(lockFile1, depFile1, refFiles1)
    
    depFile.Packages |> List.length |> shouldEqual [|"A";"D"|].Length
    depFile.DirectDependencies.["A"] |> shouldEqual (VersionRange.Exactly "3.3.0")
    depFile.DirectDependencies.["D"] |> shouldEqual (VersionRange.Exactly "2.1")

    refFiles.Head |> snd |> shouldEqual [|"A";"D"|]
    refFiles.Tail.Head |> snd |> shouldEqual [|"B";"C"|]


let lockFile2 = """
NUGET
  remote: http://nuget.org/api/v2
  specs:
    A (1.0)
      B (1.5)
    B (1.5)
      D (2.0)
    C (2.0)
      E (3.0)
    D (2.0)
      E (3.0)
    E (3.0)
      F (4.0)
    F (4.0)""" |> toLines |> LockFile.Parse

let depFile2 = """
source http://nuget.org/api/v2

nuget A 1.0
nuget B 1.5
nuget C 2.0
nuget D 2.0
nuget E 3.0
nuget F 4.0""" |> DependenciesFile.FromCode

let refFiles2 = [
    FileInfo("c:\dummy\1"), [|"A";"B";"C";"D";"F"|]
    FileInfo("c:\dummy\2"), [|"C";"D";"E"|]
]

[<Test>]
let ``should remove all indirect dependencies from dep file recursively``() =
    let depFile,refFiles  = Simplifier.Simplify(lockFile2, depFile2, refFiles2)
    
    depFile.Packages |> List.length |> shouldEqual [|"A";"C"|].Length
    depFile.DirectDependencies.["A"] |> shouldEqual (VersionRange.Exactly "1.0")
    depFile.DirectDependencies.["C"] |> shouldEqual (VersionRange.Exactly "2.0")

    refFiles.Head |> snd |> shouldEqual [|"A";"C"|]
    refFiles.Tail.Head |> snd |> shouldEqual [|"C";"D"|]