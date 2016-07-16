module Paket.ProjectFile.InterProjectDependencySpecs

open Paket
open NUnit.Framework
open FsUnit
open System
open TestHelpers

[<Test>]
let ``should detect no dependencies in empty proj file``() =
    ensureDir()
    ProjectFile.TryLoad("./ProjectFile/TestData/Empty.fsprojtest").Value.GetInterProjectDependencies()
    |> shouldBeEmpty

[<Test>]
let ``should detect Paket dependency in Project1 proj file``() =
    ensureDir()
    ProjectFile.TryLoad("./ProjectFile/TestData/Project1.fsprojtest").Value.GetInterProjectDependencies()
    |> List.map (fun p -> p.Name.Value)
    |> shouldEqual ["Paket"]

[<Test>]
let ``should ignore empty ProjectReference tag``() =
    ensureDir()
    ProjectFile.TryLoad("./ProjectFile/TestData/Project1.vcxprojtest").Value.GetInterProjectDependencies()
    |> shouldBeEmpty

[<Test>]
let ``should detect Paket and Paket.Core dependency in Project2 proj file``() =
    ensureDir()
    ProjectFile.TryLoad("./ProjectFile/TestData/Project2.fsprojtest").Value.GetInterProjectDependencies()
    |> List.map (fun p -> p.Name.Value)
    |> shouldEqual ["Paket"; "Paket.Core"]

[<Test>]
let ``should detect path for dependencies in Project2 proj file``() =
    ensureDir()
    let paths =
        ProjectFile.TryLoad("./ProjectFile/TestData/Project2.fsprojtest").Value.GetInterProjectDependencies()
        |> List.map (fun p -> p.Path)

    paths.[0].EndsWith(normalizePath "src/Paket/Paket.fsproj") |> shouldEqual true
    paths.[1].EndsWith(normalizePath "Paket.Core/Paket.Core.fsproj") |> shouldEqual true

[<Test>]
let ``should detect relative path for dependencies in Project2 proj file``() =
    ensureDir()
    let paths =
        ProjectFile.TryLoad("./ProjectFile/TestData/Project2.fsprojtest").Value.GetInterProjectDependencies()
        |> List.map (fun p -> p.RelativePath)

    paths.[0] |> shouldEqual "..\\..\\src\\Paket\\Paket.fsproj"
    paths.[1] |> shouldEqual "..\\Paket.Core\\Paket.Core.fsproj"

[<Test>]
let ``should detect Guids for dependencies in Project2 proj file``() =
    ensureDir()
    let p = ProjectFile.TryLoad("./ProjectFile/TestData/Project2.fsprojtest").Value
    p.GetProjectGuid() |> shouldEqual (Guid.Parse "e789c72a-5cfd-436b-8ef1-61aa2852a89f")
    p.GetInterProjectDependencies()
    |> List.map (fun p -> p.GUID.Value.ToString())
    |> shouldEqual ["09b32f18-0c20-4489-8c83-5106d5c04c93"; "7bab0ae2-089f-4761-b138-a717aa2f86c5"]


[<Test>]
let ``should detect solution path for dependencies in Project4 proj file``() =
    ensureDir()
    let paths =
        ProjectFile.TryLoad("./ProjectFile/TestData/Project4.fsprojtest").Value.GetInterProjectDependencies()
        |> List.map (fun p -> p.RelativePath)

    paths.[0] |> shouldEqual "..\\..\\src\\Paket\\Paket.fsproj"
    paths.[1] |> shouldEqual "..\\Paket.Core\\Paket.Core.fsproj"

[<Test>]
let ``should return None Guids for dependencies in Project5 proj file``() =
    ensureDir()
    let proj = ProjectFile.TryLoad("./ProjectFile/TestData/Project5.fsprojtest")
    proj.Value.GetInterProjectDependencies()
    |> List.map (fun p -> p.GUID)
    |> shouldEqual [None; None]

[<Test>]
let ``should return Name based on Include if not set for dependencies in Project5 proj file``() =
    ensureDir()
    let proj = ProjectFile.TryLoad("./ProjectFile/TestData/Project5.fsprojtest")
    proj.Value.GetInterProjectDependencies()
    |> List.map (fun p -> p.Name)
    |> shouldEqual [Some "Paket"; Some "Paket.Core"]
