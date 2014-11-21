module Paket.FrameworkReferencesSpecs

open Paket
open NUnit.Framework
open FsUnit

[<Test>]
let ``should detect empty framework references in empty project``() =
    ProjectFile.Load("./ProjectFile/TestData/Empty.fsprojtest").Value.GetFrameworkAssemblies()
    |> shouldEqual []

[<Test>]
let ``should detect references in project1``() =
    ProjectFile.Load("./ProjectFile/TestData/Project1.fsprojtest").Value.GetFrameworkAssemblies()
    |> shouldEqual ["mscorlib"; "System"]