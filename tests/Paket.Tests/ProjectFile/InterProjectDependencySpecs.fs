module Paket.ProjectFile.InterProjectDependencySpecs

open Paket
open NUnit.Framework
open FsUnit

[<Test>]
let ``should detect no dependencies in empty proj file``() =
    ProjectFile.Load("./ProjectFile/TestData/Empty.fsprojtest").Value.GetInterProjectDependencies()
    |> shouldBeEmpty

[<Test>]
let ``should detect Paket dependency in Project1 proj file``() =
    ProjectFile.Load("./ProjectFile/TestData/Project1.fsprojtest").Value.GetInterProjectDependencies()
    |> shouldEqual ["Paket"]

[<Test>]
let ``should detect Paket and Paket.Core dependency in Project2 proj file``() =
    ProjectFile.Load("./ProjectFile/TestData/Project2.fsprojtest").Value.GetInterProjectDependencies()
    |> shouldEqual ["Paket"; "Paket.Core"]