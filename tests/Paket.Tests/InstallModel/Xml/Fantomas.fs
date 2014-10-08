module Paket.InstallModel.Xml.FantomasSpecs

open Paket
open NUnit.Framework
open FsUnit
open Paket.Nuspec
open System.Xml
open Paket.TestHelpers

let expected = """
<Choose xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And $(TargetFrameworkVersion) == 'v1.0'">
    <ItemGroup>
      <Reference Include="FantomasLib.dll">
        <HintPath>..\Fantomas\lib\FantomasLib.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And $(TargetFrameworkVersion) == 'v1.1'">
    <ItemGroup>
      <Reference Include="FantomasLib.dll">
        <HintPath>..\Fantomas\lib\FantomasLib.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And $(TargetFrameworkVersion) == 'v2.0'">
    <ItemGroup>
      <Reference Include="FantomasLib.dll">
        <HintPath>..\Fantomas\lib\FantomasLib.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And $(TargetFrameworkVersion) == 'v3.5'">
    <ItemGroup>
      <Reference Include="FantomasLib.dll">
        <HintPath>..\Fantomas\lib\FantomasLib.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And $(TargetFrameworkVersion) == 'v4.0' And $(TargetFrameworkProfile) == 'Client'">
    <ItemGroup>
      <Reference Include="FantomasLib.dll">
        <HintPath>..\Fantomas\lib\FantomasLib.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And $(TargetFrameworkVersion) == 'v4.0'">
    <ItemGroup>
      <Reference Include="FantomasLib.dll">
        <HintPath>..\Fantomas\lib\FantomasLib.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And $(TargetFrameworkVersion) == 'v4.5'">
    <ItemGroup>
      <Reference Include="FantomasLib.dll">
        <HintPath>..\Fantomas\lib\FantomasLib.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And $(TargetFrameworkVersion) == 'v4.5.1'">
    <ItemGroup>
      <Reference Include="FantomasLib.dll">
        <HintPath>..\Fantomas\lib\FantomasLib.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
</Choose>"""

[<Test>]
let ``should generate Xml for Fantomas 1.5``() = 
    let model =
        InstallModel.CreateFromLibs("Fantomas", SemVer.parse "1.5.0",        
            [ @"..\Fantomas\lib\FantomasLib.dll" 
              @"..\Fantomas\lib\FSharp.Core.dll" 
              @"..\Fantomas\lib\Fantomas.exe" ],
              References.Explicit ["FantomasLib.dll"])

    let doc = new XmlDocument()

    let manager = new XmlNamespaceManager(doc.NameTable)
    manager.AddNamespace("ns", Constants.ProjectDefaultNameSpace)

    let chooseNode = model.GenerateXml("",doc)
    chooseNode.OuterXml
    |> normalizeXml
    |> shouldEqual (normalizeXml expected)
