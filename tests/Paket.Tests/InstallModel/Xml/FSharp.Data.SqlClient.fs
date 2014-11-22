module Paket.InstallModel.Xml.SqlCLientSpecs

open Paket
open NUnit.Framework
open FsUnit
open Paket.TestHelpers
open Paket.Domain

let expected = """
<Choose xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework'">
    <Choose>
      <When Condition="$(TargetFrameworkVersion) == 'v1.0'">
        <ItemGroup>
          <Reference Include="System.Data">
            <Paket>True</Paket>
          </Reference>
          <Reference Include="System.Xml">
            <Paket>True</Paket>
          </Reference>
        </ItemGroup>
      </When>
      <When Condition="$(TargetFrameworkVersion) == 'v1.1'">
        <ItemGroup>
          <Reference Include="System.Data">
            <Paket>True</Paket>
          </Reference>
          <Reference Include="System.Xml">
            <Paket>True</Paket>
          </Reference>
        </ItemGroup>
      </When>
      <When Condition="$(TargetFrameworkVersion) == 'v2.0'">
        <ItemGroup>
          <Reference Include="System.Data">
            <Paket>True</Paket>
          </Reference>
          <Reference Include="System.Xml">
            <Paket>True</Paket>
          </Reference>
        </ItemGroup>
      </When>
      <When Condition="$(TargetFrameworkVersion) == 'v3.5'">
        <ItemGroup>
          <Reference Include="System.Data">
            <Paket>True</Paket>
          </Reference>
          <Reference Include="System.Xml">
            <Paket>True</Paket>
          </Reference>
        </ItemGroup>
      </When>
      <Otherwise>
        <ItemGroup>
          <Reference Include="FSharp.Data.SqlClient">
            <HintPath>..\..\..\FSharp.Data.SqlClient\lib\net40\FSharp.Data.SqlClient.dll</HintPath>
            <Private>True</Private>
            <Paket>True</Paket>
          </Reference>
          <Reference Include="System.Data">
            <Paket>True</Paket>
          </Reference>
          <Reference Include="System.Xml">
            <Paket>True</Paket>
          </Reference>
        </ItemGroup>
      </Otherwise>
    </Choose>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == 'MonoAndroid'">
    <ItemGroup>
      <Reference Include="FSharp.Data.SqlClient">
        <HintPath>..\..\..\FSharp.Data.SqlClient\lib\net40\FSharp.Data.SqlClient.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Data">
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Xml">
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == 'MonoTouch'">
    <ItemGroup>
      <Reference Include="FSharp.Data.SqlClient">
        <HintPath>..\..\..\FSharp.Data.SqlClient\lib\net40\FSharp.Data.SqlClient.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Data">
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Xml">
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == 'Silverlight'">
    <ItemGroup>
      <Reference Include="FSharp.Data.SqlClient">
        <HintPath>..\..\..\FSharp.Data.SqlClient\lib\net40\FSharp.Data.SqlClient.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Data">
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Xml">
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == 'Windows'">
    <ItemGroup>
      <Reference Include="FSharp.Data.SqlClient">
        <HintPath>..\..\..\FSharp.Data.SqlClient\lib\net40\FSharp.Data.SqlClient.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Data">
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Xml">
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == 'WindowsPhoneApp'">
    <ItemGroup>
      <Reference Include="FSharp.Data.SqlClient">
        <HintPath>..\..\..\FSharp.Data.SqlClient\lib\net40\FSharp.Data.SqlClient.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Data">
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Xml">
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <Otherwise>
    <ItemGroup>
      <Reference Include="FSharp.Data.SqlClient">
        <HintPath>..\..\..\FSharp.Data.SqlClient\lib\net40\FSharp.Data.SqlClient.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Data">
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Xml">
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </Otherwise>
</Choose>"""

[<Test>]
let ``should generate Xml for FSharp.Data.SqlClient 1.4.4``() = 
    let model =
        InstallModel.CreateFromLibs(PackageName "FSharp.Data.SqlClient", SemVer.Parse "1.4.4", None,
            [ @"..\FSharp.Data.SqlClient\lib\net40\FSharp.Data.SqlClient.dll" 
              @"..\FSharp.Data.SqlClient\lib\net40\FSharp.Data.SqlClient.pdb" 
              @"..\FSharp.Data.SqlClient\lib\net40\FSharp.Data.SqlClient.XML"
              @"..\FSharp.Data.SqlClient\lib\net40\Microsoft.SqlServer.TransactSql.ScriptDom.dll"
              @"..\FSharp.Data.SqlClient\lib\net40\Microsoft.SqlServer.Types.dll" ],
              Nuspec.Load("Nuspec/FSharp.Data.SqlClient.nuspec"))
    
    let chooseNode = ProjectFile.Load("./ProjectFile/TestData/Empty.fsprojtest").Value.GenerateXml(model)
    chooseNode.OuterXml
    |> normalizeXml
    |> shouldEqual (normalizeXml expected)
