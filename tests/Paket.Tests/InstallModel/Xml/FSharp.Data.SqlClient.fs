module Paket.InstallModel.Xml.SqlCLientSpecs

open Paket
open NUnit.Framework
open FsUnit
open Paket.TestHelpers
open Paket.Domain
open Paket.Requirements

let expected = """
<Choose xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <When Condition="($(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v4.0' Or $(TargetFrameworkVersion) == 'v4.5' Or $(TargetFrameworkVersion) == 'v4.5.1' Or $(TargetFrameworkVersion) == 'v4.5.2' Or $(TargetFrameworkVersion) == 'v4.5.3')) Or ($(TargetFrameworkIdentifier) == 'MonoAndroid') Or ($(TargetFrameworkIdentifier) == 'MonoTouch')">
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
</Choose>"""

[<Test>]
let ``should generate Xml for FSharp.Data.SqlClient 1.4.4``() = 
    let model =
        InstallModel.CreateFromLibs(PackageName "FSharp.Data.SqlClient", SemVer.Parse "1.4.4", [],
            [ @"..\FSharp.Data.SqlClient\lib\net40\FSharp.Data.SqlClient.dll" 
              @"..\FSharp.Data.SqlClient\lib\net40\FSharp.Data.SqlClient.pdb" 
              @"..\FSharp.Data.SqlClient\lib\net40\FSharp.Data.SqlClient.XML"
              @"..\FSharp.Data.SqlClient\lib\net40\Microsoft.SqlServer.TransactSql.ScriptDom.dll"
              @"..\FSharp.Data.SqlClient\lib\net40\Microsoft.SqlServer.Types.dll" ],
              [],
              Nuspec.Load("Nuspec/FSharp.Data.SqlClient.nuspec"))
    
    let _,chooseNode,_ = ProjectFile.Load("./ProjectFile/TestData/Empty.fsprojtest").Value.GenerateXml(model,true,true)
    chooseNode.OuterXml
    |> normalizeXml
    |> shouldEqual (normalizeXml expected)
