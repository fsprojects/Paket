module Paket.ProjectFile.OutputSpecs

open Paket
open NUnit.Framework
open FsUnit

[<Test>]
let ``should detect lib output type for Project1 proj file``() =
    ProjectFile.Load("./ProjectFile/TestData/Project1.fsprojtest").Value.OutputType
    |> shouldEqual ProjectOutputType.Library

[<Test>]
let ``should detect exe output type for Project2 proj file``() =
    ProjectFile.Load("./ProjectFile/TestData/Project2.fsprojtest").Value.OutputType
    |> shouldEqual ProjectOutputType.Exe

[<Test>]
let ``should detect exe output type for Project3 proj file``() =
    ProjectFile.Load("./ProjectFile/TestData/Project3.fsprojtest").Value.OutputType
    |> shouldEqual ProjectOutputType.Exe

[<Test>]
let ``should detect target framework for Project1 proj file``() =
    ProjectFile.Load("./ProjectFile/TestData/Project1.fsprojtest").Value.GetTargetFramework()
    |> shouldEqual (DotNetFramework(FrameworkVersion.V4_5))

[<Test>]
let ``should detect target framework for Project2 proj file``() =
    ProjectFile.Load("./ProjectFile/TestData/Project2.fsprojtest").Value.GetTargetFramework()
    |> shouldEqual (DotNetFramework(FrameworkVersion.V4_Client))

[<Test>]
let ``should detect output path for proj file``
        ([<Values("Project1", "Project2")>] project)
        ([<Values("Debug", "Release")>] configuration) =
    ProjectFile.Load(sprintf "./ProjectFile/TestData/%s.fsprojtest" project).Value.GetOutputDirectory configuration
    |> shouldEqual (System.IO.Path.Combine(@"bin", configuration) |> normalizePath)

[<Test>]
let ``should detect assembly name for Project1 proj file`` () =
    ProjectFile.Load("./ProjectFile/TestData/Project1.fsprojtest").Value.GetAssemblyName()
    |> shouldEqual ("Paket.Tests.dll")

[<Test>]
let ``should detect assembly name for Project2 proj file`` () =
    ProjectFile.Load("./ProjectFile/TestData/Project2.fsprojtest").Value.GetAssemblyName()
    |> shouldEqual ("Paket.Tests.exe")

[<Test>]
let ``should detect assembly name for Project3 proj file`` () =
    ProjectFile.Load("./ProjectFile/TestData/Project3.fsprojtest").Value.GetAssemblyName()
    |> shouldEqual ("Paket.Tests.Win.exe")

