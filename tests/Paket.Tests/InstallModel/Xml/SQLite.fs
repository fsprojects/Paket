module Paket.InstallModel.Xml.SQLiteSpecs

open Paket
open NUnit.Framework
open FsUnit
open Paket.TestHelpers
open Paket.Domain
open Paket.Requirements

let expectedReferenceNodes = """<?xml version="1.0" encoding="utf-16"?>
<Choose xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And $(TargetFrameworkVersion) == 'v4.5'">
    <ItemGroup>
      <Reference Include="System.Data.SQLite">
        <HintPath>..\..\..\System.Data.SQLite.Core\lib\net45\System.Data.SQLite.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v2.0' Or $(TargetFrameworkVersion) == 'v3.0' Or $(TargetFrameworkVersion) == 'v3.5')">
    <ItemGroup>
      <Reference Include="System.Data.SQLite">
        <HintPath>..\..\..\System.Data.SQLite.Core\lib\net20\System.Data.SQLite.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v4.0')">
    <ItemGroup>
      <Reference Include="System.Data.SQLite">
        <HintPath>..\..\..\System.Data.SQLite.Core\lib\net40\System.Data.SQLite.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="($(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v4.5.1' Or $(TargetFrameworkVersion) == 'v4.5.2' Or $(TargetFrameworkVersion) == 'v4.5.3')) Or ($(TargetFrameworkIdentifier) == 'MonoAndroid') Or ($(TargetFrameworkIdentifier) == 'MonoTouch')">
    <ItemGroup>
      <Reference Include="System.Data.SQLite">
        <HintPath>..\..\..\System.Data.SQLite.Core\lib\net451\System.Data.SQLite.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
</Choose>"""


let expectedPropertyDefinitionNodes = """<?xml version="1.0" encoding="utf-16"?>
<Choose xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And $(TargetFrameworkVersion) == 'v4.5'">
    <PropertyGroup>
      <__paket__System_Data_SQLite_Core_targets>net45\System.Data.SQLite.Core</__paket__System_Data_SQLite_Core_targets>
    </PropertyGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v2.0' Or $(TargetFrameworkVersion) == 'v3.0' Or $(TargetFrameworkVersion) == 'v3.5')">
    <PropertyGroup>
      <__paket__System_Data_SQLite_Core_targets>net20\System.Data.SQLite.Core</__paket__System_Data_SQLite_Core_targets>
    </PropertyGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v4.0')">
    <PropertyGroup>
      <__paket__System_Data_SQLite_Core_targets>net40\System.Data.SQLite.Core</__paket__System_Data_SQLite_Core_targets>
    </PropertyGroup>
  </When>
  <When Condition="($(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v4.5.1' Or $(TargetFrameworkVersion) == 'v4.5.2' Or $(TargetFrameworkVersion) == 'v4.5.3')) Or ($(TargetFrameworkIdentifier) == 'MonoAndroid') Or ($(TargetFrameworkIdentifier) == 'MonoTouch')">
    <PropertyGroup>
      <__paket__System_Data_SQLite_Core_targets>net451\System.Data.SQLite.Core</__paket__System_Data_SQLite_Core_targets>
    </PropertyGroup>
  </When>
</Choose>"""

let expectedPropertyNodes = """<?xml version="1.0" encoding="utf-16"?>
<Import Project="..\..\..\System.Data.SQLite.Core\build\$(__paket__System_Data_SQLite_Core_targets).targets" Condition="Exists('..\..\..\System.Data.SQLite.Core\build\$(__paket__System_Data_SQLite_Core_targets).targets')" Label="Paket" xmlns="http://schemas.microsoft.com/developer/msbuild/2003" />"""

[<Test>]
let ``should generate Xml for SQLite``() = 
    let model =
        InstallModel.CreateFromLibs(PackageName "System.Data.SQLite.Core", SemVer.Parse "3.8.2", [],
            [ @"..\System.Data.SQLite.Core\lib\net20\System.Data.SQLite.dll"
              @"..\System.Data.SQLite.Core\lib\net40\System.Data.SQLite.dll"
              @"..\System.Data.SQLite.Core\lib\net45\System.Data.SQLite.dll"
              @"..\System.Data.SQLite.Core\lib\net451\System.Data.SQLite.dll"],
            [ @"..\System.Data.SQLite.Core\build\net20\System.Data.SQLite.Core.targets"
              @"..\System.Data.SQLite.Core\build\net40\System.Data.SQLite.Core.targets"
              @"..\System.Data.SQLite.Core\build\net45\System.Data.SQLite.Core.targets"
              @"..\System.Data.SQLite.Core\build\net451\System.Data.SQLite.Core.targets" ],
              Nuspec.All)
    
    let propertyNodes,chooseNode,propertyDefinitionNodes = ProjectFile.Load("./ProjectFile/TestData/Empty.fsprojtest").Value.GenerateXml(model,true,true)
    chooseNode.OuterXml
    |> normalizeXml
    |> shouldEqual (normalizeXml expectedReferenceNodes)

    propertyDefinitionNodes.OuterXml
    |> normalizeXml
    |> shouldEqual (normalizeXml expectedPropertyDefinitionNodes)

    propertyNodes |> Seq.length |> shouldEqual 1

    (propertyNodes |> Seq.head).OuterXml
    |> normalizeXml
    |> shouldEqual (normalizeXml expectedPropertyNodes)