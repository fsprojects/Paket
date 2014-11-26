module Paket.InstallModel.Xml.ManualNodesSpecs

open Paket
open NUnit.Framework
open FsUnit
open Paket.Domain

[<Test>]
let ``should find custom nodes in doc``() = 
    let model =
        InstallModel.CreateFromLibs(PackageName "Fantomas", SemVer.Parse "1.5.0", None,
            [ @"..\Fantomas\lib\FantomasLib.dll" 
              @"..\Fantomas\lib\FSharp.Core.dll" 
              @"..\Fantomas\lib\Fantomas.exe" ],
              Nuspec.Explicit ["FantomasLib.dll"])
    
    ProjectFile.Load("./ProjectFile/TestData/CustomFantomasNode.fsprojtest").Value.GetCustomModelNodes(model).IsEmpty
    |> shouldEqual false


[<Test>]
let ``should not find custom nodes if there are none``() = 
    let model =
        InstallModel.CreateFromLibs(PackageName "Fantomas", SemVer.Parse "1.5.0", None,
            [ @"..\Fantomas\lib\FantomasLib.dll" 
              @"..\Fantomas\lib\FSharp.Core.dll" 
              @"..\Fantomas\lib\Fantomas.exe" ],
              Nuspec.Explicit ["FantomasLib.dll"])

    ProjectFile.Load("./ProjectFile/TestData/NoCustomFantomasNode.fsprojtest").Value.GetCustomModelNodes(model).IsEmpty
    |> shouldEqual true


[<Test>]
let ``should delete custom nodes if there are some``() = 
    let model =
        InstallModel.CreateFromLibs(PackageName "Fantomas", SemVer.Parse "1.5.0", None,
            [ @"..\Fantomas\lib\FantomasLib.dll" 
              @"..\Fantomas\lib\FSharp.Core.dll" 
              @"..\Fantomas\lib\Fantomas.exe" ],
              Nuspec.Explicit ["FantomasLib.dll"])

    let project = ProjectFile.Load("./ProjectFile/TestData/CustomFantomasNode.fsprojtest").Value

    project.GetCustomModelNodes(model).IsEmpty
    |> shouldEqual false

    project.DeleteCustomNodes()

    project.GetCustomModelNodes(model).IsEmpty
    |> shouldEqual true