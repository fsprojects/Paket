module Paket.InstallModel.Xml.ManualNodesSpecs

open Paket
open NUnit.Framework
open FsUnit
open Paket.Nuspec
open System.Xml

let projectWithCustomFantomasNode = """<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="4.0" DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <Import Project="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props" Condition="Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')" />
    <ItemGroup>
      <Reference Include="FantomasLib">
        <HintPath>..\..\packages\Fantomas\lib\FantomasLib.dll</HintPath>
        <Private>True</Private>        
      </Reference>
    </ItemGroup>
</Project>"""

[<Test>]
let ``should find custom nodes in doc``() = 
    let model =
        InstallModel.CreateFromLibs("Fantomas", SemVer.parse "1.5.0",
            [ @"..\Fantomas\lib\FantomasLib.dll" 
              @"..\Fantomas\lib\FSharp.Core.dll" 
              @"..\Fantomas\lib\Fantomas.exe" ],
              References.Explicit ["FantomasLib.dll"])

    let doc = new XmlDocument()
    doc.LoadXml projectWithCustomFantomasNode

    model.HasCustomNodes(doc)
    |> shouldEqual true


let projectWithoutCustomFantomasNode = """<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="4.0" DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <Import Project="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props" Condition="Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')" />
    <ItemGroup>
      <Reference Include="Fanta">
        <HintPath>..\..\packages\Fanta\lib\Fanta.dll</HintPath>
        <Private>True</Private>        
      </Reference>
      <Reference Include="FantomasLib">
        <HintPath>..\..\packages\Fantomas\lib\FantomasLib.dll</HintPath>
        <Private>True</Private>        
        <Paket>True</Paket>  
      </Reference>
    </ItemGroup>
</Project>"""

[<Test>]
let ``should not find custom nodes if there are none``() = 
    let model =
        InstallModel.CreateFromLibs("Fantomas", SemVer.parse "1.5.0",
            [ @"..\Fantomas\lib\FantomasLib.dll" 
              @"..\Fantomas\lib\FSharp.Core.dll" 
              @"..\Fantomas\lib\Fantomas.exe" ],
              References.Explicit ["FantomasLib.dll"])

    let doc = new XmlDocument()
    doc.LoadXml projectWithoutCustomFantomasNode

    model.HasCustomNodes(doc)
    |> shouldEqual false


[<Test>]
let ``should delete custom nodes if there are some``() = 
    let model =
        InstallModel.CreateFromLibs("Fantomas", SemVer.parse "1.5.0",
            [ @"..\Fantomas\lib\FantomasLib.dll" 
              @"..\Fantomas\lib\FSharp.Core.dll" 
              @"..\Fantomas\lib\Fantomas.exe" ],
              References.Explicit ["FantomasLib.dll"])

    let doc = new XmlDocument()
    doc.LoadXml projectWithCustomFantomasNode

    model.HasCustomNodes(doc)
    |> shouldEqual true

    model.DeleteCustomNodes(doc)

    model.HasCustomNodes(doc)
    |> shouldEqual false