module Paket.Simplifier.BasicScenarioSpecs

open Paket
open Paket.LockFile
open NUnit.Framework
open FsUnit
open TestHelpers


let lockFile = """NUGET
  remote: http://nuget.org/api/v2
  specs:
    A (3.3.1)
    B (3.3.0)
      A (>= 3.3.0)""" |> toLines |> LockFile.Parse

let depFile = """source http://nuget.org/api/v2

nuget A 3.3.1
nuget B 3.3.0""" |> DependenciesFile.FromCode

let expected = """source http://nuget.org/api/v2

nuget B 3.3.0"""

[<Test>]
let ``should remove indirect dependency from dep file``() = 
    Simplifier.Simplify(lockFile, depFile) |> shouldEqual expected