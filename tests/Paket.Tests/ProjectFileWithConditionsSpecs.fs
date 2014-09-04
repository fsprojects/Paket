module Paket.ProjectFileWithConditionsSpecs

open Paket
open NUnit.Framework
open FsUnit

[<Test>]
let ``should detect reference nodes``() =
    let references = ProjectFile.Load("./TestData/ProjectWithConditions.fsprojtest").GetReferences()

    references.Length |> shouldEqual 6

    references.[1].DLLName |> shouldEqual "FSharp.Core"
    references.[1].Condition |> shouldEqual None
    references.[1].HintPath |> shouldEqual (Some @"..\..\lib\FSharp\FSharp.Core.dll")

    references.[2].DLLName |> shouldEqual "FSharp.Core"
    references.[2].Condition |> shouldEqual (Some "$(TargetFrameworkVersion) == 'v3.5'")
    references.[2].HintPath |> shouldEqual (Some @"..\..\lib\FSharp\Net20\FSharp.Core.dll")

    references.[3].DLLName |> shouldEqual "FSharp.Core"
    references.[3].Condition |> shouldEqual (Some "'$(TargetFrameworkVersion)' == 'v4.0'")
    references.[3].HintPath |> shouldEqual (Some @"..\..\lib\FSharp\FSharp.Core.dll")


[<Test>]
let ``should update correct node``() =
    let project = ProjectFile.Load "./TestData/ProjectWithConditions.fsprojtest"

    let node = (project.GetReferences()).[2]
    let newNode = { node with HintPath = Some @"..\..\packages\FSharp.Core\lib\net20\FSharp.Core.dll" }

    project.UpdateReference(newNode)

    let reloaded = project.GetReferences()

    reloaded.[2].DLLName |> shouldEqual "FSharp.Core"
    reloaded.[2].HintPath |> shouldEqual (Some @"..\..\packages\FSharp.Core\lib\net20\FSharp.Core.dll")