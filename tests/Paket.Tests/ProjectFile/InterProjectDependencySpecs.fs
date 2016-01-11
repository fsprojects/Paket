module Paket.ProjectFile.InterProjectDependencySpecs

open Paket
open NUnit.Framework
open FsUnit
open System

[<Test>]
let ``should detect no dependencies in empty proj file``() =
    ProjectFile.TryLoad("./ProjectFile/TestData/Empty.fsprojtest").Value.GetInterProjectDependencies()
    |> shouldBeEmpty

[<Test>]
let ``should detect Paket dependency in Project1 proj file``() =
    ProjectFile.TryLoad("./ProjectFile/TestData/Project1.fsprojtest").Value.GetInterProjectDependencies()
    |> List.map (fun p -> p.Name)
    |> shouldEqual ["Paket"]

[<Test>]
let ``should detect Paket and Paket.Core dependency in Project2 proj file``() =
    ProjectFile.TryLoad("./ProjectFile/TestData/Project2.fsprojtest").Value.GetInterProjectDependencies()
    |> List.map (fun p -> p.Name)
    |> shouldEqual ["Paket"; "Paket.Core"]

[<Test>]
let ``should detect path for dependencies in Project2 proj file``() =
    let paths =
        ProjectFile.TryLoad("./ProjectFile/TestData/Project2.fsprojtest").Value.GetInterProjectDependencies()
        |> List.map (fun p -> p.Path)

    paths.[0].EndsWith(normalizePath "src/Paket/Paket.fsproj") |> shouldEqual true
    paths.[1].EndsWith(normalizePath "src/Paket.Core/Paket.Core.fsproj") |> shouldEqual true

[<Test>]
let ``should detect relative path for dependencies in Project2 proj file``() =
    let paths =
        ProjectFile.TryLoad("./ProjectFile/TestData/Project2.fsprojtest").Value.GetInterProjectDependencies()
        |> List.map (fun p -> p.RelativePath)

    paths.[0] |> shouldEqual "..\\..\\..\\..\\src\\Paket\\Paket.fsproj"
    paths.[1] |> shouldEqual "..\\..\\..\\..\\src\\Paket.Core\\Paket.Core.fsproj"

[<Test>]
let ``should detect Guids for dependencies in Project2 proj file``() =
    let p = ProjectFile.TryLoad("./ProjectFile/TestData/Project2.fsprojtest").Value
    p.GetProjectGuid() |> shouldEqual (Guid.Parse "e789c72a-5cfd-436b-8ef1-61aa2852a89f")
    p.GetInterProjectDependencies()
    |> List.map (fun p -> p.GUID.ToString())
    |> shouldEqual ["09b32f18-0c20-4489-8c83-5106d5c04c93"; "7bab0ae2-089f-4761-b138-a717aa2f86c5"]

[<Test>]
let ``should not add dependencies in referenced projects for GetCompileItems false``() =
    let p = ProjectFile.TryLoad("../../ProjectFile/TestData/Project2.fsprojtest").Value
    p.GetCompileItems(false)
    |> Seq.filter (fun ci -> ci.Include.Contains("Program.fs"))
    |> Seq.length
    |> shouldEqual 0

[<Test>]
let ``should add dependencies in referenced projects for GetCompileItems true``() =
    let p = ProjectFile.TryLoad("../../ProjectFile/TestData/Project2.fsprojtest").Value
    p.GetCompileItems(true)
    |> Seq.filter (fun ci -> ci.Include.Contains("Program.fs"))
    |> Seq.length
    |> shouldBeGreaterThan 0
