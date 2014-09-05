module Paket.ProjectFile.ParserSpecs

open Paket
open NUnit.Framework
open FsUnit

[<Test>]
let ``should detect reference nodes``() =
    let references = ProjectFile.Load("./ProjectFile/TestData/Project1.fsprojtest").GetReferences()

    references.Length |> shouldEqual 4
    references.[0].DLLName |> shouldEqual "mscorlib"
    references.[0].Private |> shouldEqual false
    references.[0].HintPath |> shouldEqual None

    references.[1].DLLName |> shouldEqual "FSharp.Core"
    references.[1].Private |> shouldEqual true
    references.[1].HintPath |> shouldEqual (Some @"..\..\lib\FSharp\FSharp.Core.dll")

    references.[2].DLLName |> shouldEqual "nunit.framework"
    references.[2].Private |> shouldEqual true
    references.[2].HintPath |> shouldEqual (Some @"..\..\packages\NUnit.2.6.3\lib\nunit.framework.dll")

    references.[3].DLLName |> shouldEqual "System"
    references.[3].Private |> shouldEqual false
    references.[3].HintPath |> shouldEqual None

[<Test>]
let ``should update single nodes``() =
    let project = ProjectFile.Load "./ProjectFile/TestData/Project1.fsprojtest"

    let node = (project.GetReferences()).[2]
    let newNode = { node with HintPath = Some @"..\..\packages\NUnit.2.7.5\lib\nunit.framework.dll" }

    project.AddReference(newNode)

    let reloaded = project.GetReferences()

    reloaded.[4].DLLName |> shouldEqual "nunit.framework"
    reloaded.[4].Private |> shouldEqual true
    reloaded.[4].HintPath |> shouldEqual (Some @"..\..\packages\NUnit.2.7.5\lib\nunit.framework.dll")

[<Test>]
let ``should add single node``() =
    let project = ProjectFile.Load "./ProjectFile/TestData/Project1.fsprojtest"

    let hintPath = @"..\..\packagesFAKE\lib\Fake.Core.dll"
    let newNode = { DLLName = "FAKE"; HintPath = Some hintPath; Private = false; Node = None; Condition = None }

    project.AddReference(newNode)

    let reloaded = project.GetReferences()

    reloaded.[4].DLLName |> shouldEqual "FAKE"
    reloaded.[4].Private |> shouldEqual false
    reloaded.[4].HintPath |> shouldEqual (Some hintPath)