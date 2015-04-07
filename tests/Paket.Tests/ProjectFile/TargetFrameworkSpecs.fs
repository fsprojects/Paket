module Paket.ProjectFile.TargetFrameworkSpecs

open Paket
open NUnit.Framework
open FsUnit

[<Test>]
let ``should detect TargetFramework in Project2 proj file``() =
    ProjectFile.Load("./ProjectFile/TestData/Project2.fsprojtest").Value.GetTargetFramework().ToString()
    |> shouldEqual "net40"

[<Test>]
let ``should detect net40 in empty proj file``() =
    ProjectFile.Load("./ProjectFile/TestData/Empty.fsprojtest").Value.GetTargetFramework().ToString()
    |> shouldEqual "net40"

[<Test>]
let ``should detect silverlight framework in new silverlight project2``() =
    ProjectFile.Load("./ProjectFile/TestData/NewSilverlightClassLibrary.csprojtest").Value.GetTargetFramework()
    |> shouldEqual (Silverlight("v5.0"))
