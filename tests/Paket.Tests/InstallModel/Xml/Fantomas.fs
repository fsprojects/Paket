module Paket.InstallModel.Xml.FantomasSpecs

open Paket
open NUnit.Framework
open FsUnit
open Paket.TestHelpers
open Paket.Domain
open System.IO

let expected = """
<ItemGroup xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <Reference Include="FantomasLib">
    <HintPath>..\..\..\Fantomas\lib\FantomasLib.dll</HintPath>
    <Private>True</Private>
    <Paket>True</Paket>
  </Reference>
</ItemGroup>"""

[<Test>]
let ``should generate Xml for Fantomas 1.5``() = 
    let model =
        InstallModel.CreateFromLibs(PackageName "Fantomas", SemVer.Parse "1.5.0", None,
            [ @"..\Fantomas\lib\FantomasLib.dll" 
              @"..\Fantomas\lib\FSharp.Core.dll" 
              @"..\Fantomas\lib\Fantomas.exe" ],
              Nuspec.Explicit ["FantomasLib.dll"])
    
    let chooseNode = ProjectFile.Load("./ProjectFile/TestData/Empty.fsprojtest").Value.GenerateXml(model)
    chooseNode.OuterXml
    |> normalizeXml
    |> shouldEqual (normalizeXml expected)


let emptyDoc = """<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="4.0" DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <Import Project="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props" Condition="Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')" />
</Project>"""

let fullDoc = """<?xml version="1.0" encoding="utf-16"?>
<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <ItemGroup>
    <Reference Include="FantomasLib">
      <HintPath>..\..\..\Fantomas\lib\FantomasLib.dll</HintPath>
      <Private>True</Private>
      <Paket>True</Paket>
    </Reference>
  </ItemGroup>
</Project>"""

let docWithTargets = """<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="4.0" DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <Import Project="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props" Condition="Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')" />
  <Import Project="..\..\packages\Fantomas\paket.targets" Condition="Exists('..\..\packages\Fantomas\paket.targets')" />
</Project>"""

[<Test>]
let ``should generate full Xml for Fantomas 1.5``() = 
    let model =
        InstallModel.CreateFromLibs(PackageName "Fantomas", SemVer.Parse "1.5.0", None,
            [ @"..\Fantomas\lib\FantomasLib.dll" 
              @"..\Fantomas\lib\FSharp.Core.dll" 
              @"..\Fantomas\lib\Fantomas.exe" ],
              Nuspec.Explicit ["FantomasLib.dll"])
    
    let project = ProjectFile.Load("./ProjectFile/TestData/Empty.fsprojtest").Value
    let completeModel = [NormalizedPackageName (PackageName "Fantomas"),model] |> Map.ofSeq
    let used = [NormalizedPackageName (PackageName "fantoMas")] |> Set.ofSeq
    let targetFiles = project.GenerateReferences(".",completeModel,used,false)
    
    project.Document.OuterXml
    |> normalizeXml
    |> shouldEqual (normalizeXml docWithTargets)

    let targetFileName,targetDoc = targetFiles |> List.head

    targetFileName.EndsWith (Path.Combine("packages","Fantomas","paket.targets")) |> shouldEqual true
    targetDoc.OuterXml
    |> normalizeXml 
    |> shouldEqual (normalizeXml fullDoc)


[<Test>]
let ``should not generate full Xml for Fantomas 1.5 if not referenced``() = 
    let model =
        InstallModel.CreateFromLibs(PackageName "Fantomas", SemVer.Parse "1.5.0", None,
            [ @"..\Fantomas\lib\FantomasLib.dll" 
              @"..\Fantomas\lib\FSharp.Core.dll" 
              @"..\Fantomas\lib\Fantomas.exe" ],
              Nuspec.Explicit ["FantomasLib.dll"])
    
    let project = ProjectFile.Load("./ProjectFile/TestData/Empty.fsprojtest").Value
    let completeModel = [NormalizedPackageName (PackageName "Fantomas"),model] |> Map.ofSeq
    let used = [NormalizedPackageName (PackageName "blub")] |> Set.ofSeq
    let targetFiles = project.GenerateReferences(".",completeModel,used,false)
    
    project.Document.OuterXml
    |> normalizeXml
    |> shouldEqual (normalizeXml emptyDoc)