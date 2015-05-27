module Paket.InstallModel.Xml.LibGit2SharpSpecs

open Paket
open NUnit.Framework
open FsUnit
open Paket.TestHelpers
open Paket.Domain
open Paket.Requirements

let expectedReferenceNodes = """<?xml version="1.0" encoding="utf-16"?>
<Choose xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <When Condition="($(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v4.0' Or $(TargetFrameworkVersion) == 'v4.5' Or $(TargetFrameworkVersion) == 'v4.5.1' Or $(TargetFrameworkVersion) == 'v4.5.2' Or $(TargetFrameworkVersion) == 'v4.5.3')) Or ($(TargetFrameworkIdentifier) == 'MonoAndroid') Or ($(TargetFrameworkIdentifier) == 'MonoTouch')">
    <ItemGroup>
      <Reference Include="LibGit2Sharp">
        <HintPath>..\..\..\LibGit2Sharp\lib\net40\LibGit2Sharp.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
</Choose>"""

let expectedPropertyDefinitionNodes = """<?xml version="1.0" encoding="utf-16"?>
<Choose xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <When Condition="($(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v4.0' Or $(TargetFrameworkVersion) == 'v4.5' Or $(TargetFrameworkVersion) == 'v4.5.1' Or $(TargetFrameworkVersion) == 'v4.5.2' Or $(TargetFrameworkVersion) == 'v4.5.3')) Or ($(TargetFrameworkIdentifier) == 'MonoAndroid') Or ($(TargetFrameworkIdentifier) == 'MonoTouch')">
    <PropertyGroup>
      <__paket__LibGit2Sharp_props>net40\LibGit2Sharp</__paket__LibGit2Sharp_props>
    </PropertyGroup>
  </When>
</Choose>"""

let expectedPropertyNodes = """<?xml version="1.0" encoding="utf-16"?>
<Import Project="..\..\..\LibGit2Sharp\build\$(__paket__LibGit2Sharp_props).props" Condition="Exists('..\..\..\LibGit2Sharp\build\$(__paket__LibGit2Sharp_props).props')" Label="Paket" xmlns="http://schemas.microsoft.com/developer/msbuild/2003" />"""

[<Test>]
let ``should generate Xml for LibGit2Sharp 2.0.0``() = 
    let model =
        InstallModel.CreateFromLibs(PackageName "LibGit2Sharp", SemVer.Parse "0.21", [],
            [ @"..\LibGit2Sharp\lib\net40\LibGit2Sharp.dll" ],
            [ @"..\LibGit2Sharp\build\net40\LibGit2Sharp.props" ],
              Nuspec.All)
    
    model.GetLibReferences(SinglePlatform (DotNetFramework FrameworkVersion.V4_Client)) |> shouldContain @"..\LibGit2Sharp\lib\net40\LibGit2Sharp.dll"

    let propertyNodes,chooseNode,propertyChooseNode = ProjectFile.Load("./ProjectFile/TestData/Empty.fsprojtest").Value.GenerateXml(model,true,true)
    chooseNode.OuterXml
    |> normalizeXml
    |> shouldEqual (normalizeXml expectedReferenceNodes)

    propertyChooseNode.OuterXml
    |> normalizeXml
    |> shouldEqual (normalizeXml expectedPropertyDefinitionNodes)

    propertyNodes |> Seq.length |> shouldEqual 1

    (propertyNodes |> Seq.head).OuterXml
    |> normalizeXml
    |> shouldEqual (normalizeXml expectedPropertyNodes)