module Paket.InstallModel.Xml.FantomasSpecs

open Paket
open NUnit.Framework
open FsUnit
open Paket.TestHelpers

let expected = """
<Choose xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework'">
    <Choose>
      <When Condition="$(TargetFrameworkVersion) == 'v1.0'">
        <ItemGroup>
          <Reference Include="FantomasLib">
            <HintPath>..\..\..\Fantomas\lib\FantomasLib.dll</HintPath>
            <Private>True</Private>
            <Paket>True</Paket>
          </Reference>
        </ItemGroup>
      </When>
      <When Condition="$(TargetFrameworkVersion) == 'v1.1'">
        <ItemGroup>
          <Reference Include="FantomasLib">
            <HintPath>..\..\..\Fantomas\lib\FantomasLib.dll</HintPath>
            <Private>True</Private>
            <Paket>True</Paket>
          </Reference>
        </ItemGroup>
      </When>
      <When Condition="$(TargetFrameworkVersion) == 'v2.0'">
        <ItemGroup>
          <Reference Include="FantomasLib">
            <HintPath>..\..\..\Fantomas\lib\FantomasLib.dll</HintPath>
            <Private>True</Private>
            <Paket>True</Paket>
          </Reference>
        </ItemGroup>
      </When>
      <When Condition="$(TargetFrameworkVersion) == 'v3.5'">
        <ItemGroup>
          <Reference Include="FantomasLib">
            <HintPath>..\..\..\Fantomas\lib\FantomasLib.dll</HintPath>
            <Private>True</Private>
            <Paket>True</Paket>
          </Reference>
        </ItemGroup>
      </When>
      <When Condition="$(TargetFrameworkVersion) == 'v4.0' And $(TargetFrameworkProfile) == 'Client'">
        <ItemGroup>
          <Reference Include="FantomasLib">
            <HintPath>..\..\..\Fantomas\lib\FantomasLib.dll</HintPath>
            <Private>True</Private>
            <Paket>True</Paket>
          </Reference>
        </ItemGroup>
      </When>
      <When Condition="$(TargetFrameworkVersion) == 'v4.0'">
        <ItemGroup>
          <Reference Include="FantomasLib">
            <HintPath>..\..\..\Fantomas\lib\FantomasLib.dll</HintPath>
            <Private>True</Private>
            <Paket>True</Paket>
          </Reference>
        </ItemGroup>
      </When>
      <When Condition="$(TargetFrameworkVersion) == 'v4.5'">
        <ItemGroup>
          <Reference Include="FantomasLib">
            <HintPath>..\..\..\Fantomas\lib\FantomasLib.dll</HintPath>
            <Private>True</Private>
            <Paket>True</Paket>
          </Reference>
        </ItemGroup>
      </When>
      <When Condition="$(TargetFrameworkVersion) == 'v4.5.1'">
        <ItemGroup>
          <Reference Include="FantomasLib">
            <HintPath>..\..\..\Fantomas\lib\FantomasLib.dll</HintPath>
            <Private>True</Private>
            <Paket>True</Paket>
          </Reference>
        </ItemGroup>
      </When>
      <When Condition="$(TargetFrameworkVersion) == 'v4.5.2'">
        <ItemGroup>
          <Reference Include="FantomasLib">
            <HintPath>..\..\..\Fantomas\lib\FantomasLib.dll</HintPath>
            <Private>True</Private>
            <Paket>True</Paket>
          </Reference>
        </ItemGroup>
      </When>
      <When Condition="$(TargetFrameworkVersion) == 'v4.5.3'"></When>
      <Otherwise>
        <ItemGroup>
          <Reference Include="FantomasLib">
            <HintPath>..\..\..\Fantomas\lib\FantomasLib.dll</HintPath>
            <Private>True</Private>
            <Paket>True</Paket>
          </Reference>
        </ItemGroup>
      </Otherwise>
    </Choose>
  </When>
</Choose>"""

[<Test>]
let ``should generate Xml for Fantomas 1.5``() = 
    let model =
        InstallModel.CreateFromLibs("Fantomas", SemVer.Parse "1.5.0",        
            [ @"..\Fantomas\lib\FantomasLib.dll" 
              @"..\Fantomas\lib\FSharp.Core.dll" 
              @"..\Fantomas\lib\Fantomas.exe" ],
              { References = NuspecReferences.Explicit ["FantomasLib.dll"]; FrameworkAssemblyReferences = []})
    
    let chooseNode = ProjectFile.Load("./ProjectFile/TestData/Empty.fsprojtest").Value.GenerateXml(model)
    chooseNode.OuterXml
    |> normalizeXml
    |> shouldEqual (normalizeXml expected)
