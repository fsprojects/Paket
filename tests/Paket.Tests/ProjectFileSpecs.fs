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
    references.[1].DLLName |> shouldEqual "FSharp.Core"
    references.[2].DLLName |> shouldEqual "nunit.framework"
    references.[3].DLLName |> shouldEqual "System"