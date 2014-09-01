module Paket.ProjectFileSpecs

open Paket
open NUnit.Framework
open FsUnit

[<Test>]
let ``should detect reference nodes``() =
    let references =
        ProjectFile.getProject "./TestData/Project1.fsproj"
        |> ProjectFile.getReferences

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