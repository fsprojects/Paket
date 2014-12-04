module Paket.Simplifier.BasicScenarioSpecs

open Paket

open NUnit.Framework
open FsUnit
open Paket.Domain
open Paket.TestHelpers


let lockFile1 = """
NUGET
  remote: https://nuget.org/api/v2
  specs:
    A (1.0)
      B (1.0)
      C (1.0)
    B (1.0)
    C (1.0)
    D (1.0)
      B (1.0)
      C (1.0)""" |> (fun x -> LockFile.Parse("", toLines x))

let depFile1 = """
source http://nuget.org/api/v2

nuget A 3.3.0
nuget B 3.3.1
nuget C 1.0
nuget D 2.1"""

let cfg = DependenciesFile.FromCode(depFile1)

let refFiles1 = [
    ReferencesFile.FromLines [|"A";"B";"C";"D"|]
    ReferencesFile.FromLines [|"B";"C"|]
]

[<Test>]
let ``should remove one level deep indirect dependencies from dep and ref files``() = 
    let depFile,refFiles = Simplifier.Analyze(lockFile1, cfg, refFiles1, false)
    
    depFile.Packages |> List.map (fun p -> p.Name) |> shouldEqual [PackageName"A";PackageName"D"]

    refFiles.Head.NugetPackages |> shouldEqual [PackageName "A";PackageName "D"]
    refFiles.Tail.Head.NugetPackages |> shouldEqual [PackageName "B";PackageName "C"]


let lockFile2 = """
NUGET
  remote: https://nuget.org/api/v2
  specs:
    A (1.0)
      B (1.0)
    B (1.0)
      D (1.0)
    C (1.0)
      E (1.0)
    D (1.0)
      E (1.0)
    E (1.0)
      F (1.0)
    F (1.0)""" |> (fun x -> LockFile.Parse("", toLines x))

let depFile2 = """
source http://nuget.org/api/v2

nuget A 1.0
nuget B 1.0
nuget C 1.0
nuget D 1.0
nuget E 1.0
nuget F 1.0""" 

let cfg2 = DependenciesFile.FromCode(depFile2)

let refFiles2 = [
    ReferencesFile.FromLines [|"A";"B";"C";"D";"F"|]
    ReferencesFile.FromLines [|"C";"D";"E"|]
]

[<Test>]
let ``should remove all indirect dependencies from dep file recursively``() =
    let depFile,refFiles  = Simplifier.Analyze(lockFile2, cfg2, refFiles2, false)
    
    depFile.Packages |> List.map (fun p -> p.Name) |> shouldEqual [PackageName"A";PackageName"C"]

    refFiles.Head.NugetPackages |>  shouldEqual [PackageName "A";PackageName "C"]
    refFiles.Tail.Head.NugetPackages |>  shouldEqual [PackageName "C";PackageName "D"]



let strictLockFile = """REFERENCES: STRICT
NUGET
  remote: https://nuget.org/api/v2
  specs:
    A (1.0)
      B (1.0)
    B (1.0)""" 
    
let strictDepFile = """
references strict
source http://nuget.org/api/v2

nuget A 1.0
nuget B 1.0""" 


[<Test>]
let ``should not remove dependency in strict mode``() =
    let lockFile = strictLockFile |> (fun x -> LockFile.Parse("", toLines x))
    let cfg = DependenciesFile.FromCode(strictDepFile)
    let depFile,refFiles  = Simplifier.Analyze(lockFile, cfg, [], false)
    
    depFile.Packages |> List.map (fun p -> p.Name) |> shouldEqual [PackageName"A";PackageName"B"]