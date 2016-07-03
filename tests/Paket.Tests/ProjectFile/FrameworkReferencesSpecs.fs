module Paket.FrameworkReferencesSpecs

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers

[<Test>]
let ``should detect empty framework references in empty project``() =
    ensureDir()
    ProjectFile.TryLoad("./ProjectFile/TestData/Empty.fsprojtest").Value.GetFrameworkAssemblies()
    |> shouldEqual []

[<Test>]
let ``should detect references in project1``() =
    ensureDir()
    ProjectFile.TryLoad("./ProjectFile/TestData/Project1.fsprojtest").Value.GetFrameworkAssemblies()
    |> shouldEqual ["mscorlib"; "System"]