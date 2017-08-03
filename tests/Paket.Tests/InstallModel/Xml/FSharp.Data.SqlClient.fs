module Paket.InstallModel.Xml.SqlCLientSpecs

open Paket
open NUnit.Framework
open FsUnit
open Paket.TestHelpers
open Paket.Domain
open Paket.Requirements
open System.IO
open Pri.LongPath

let expected = """
<Choose xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v4.0' Or $(TargetFrameworkVersion) == 'v4.0.3' Or $(TargetFrameworkVersion) == 'v4.5' Or $(TargetFrameworkVersion) == 'v4.5.1' Or $(TargetFrameworkVersion) == 'v4.5.2' Or $(TargetFrameworkVersion) == 'v4.5.3' Or $(TargetFrameworkVersion) == 'v4.6' Or $(TargetFrameworkVersion) == 'v4.6.1' Or $(TargetFrameworkVersion) == 'v4.6.2' Or $(TargetFrameworkVersion) == 'v4.6.3' Or $(TargetFrameworkVersion) == 'v4.7')">
    <ItemGroup>
      <Reference Include="System.Data">
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Xml">
        <Paket>True</Paket>
      </Reference>
      <Reference Include="FSharp.Data.SqlClient">
        <HintPath>..\..\..\FSharp.Data.SqlClient\lib\net40\FSharp.Data.SqlClient.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
</Choose>"""

[<Test>]
let ``should generate Xml for FSharp.Data.SqlClient 1.4.4``() = 
    if not isMonoRuntime then // TODO - figure out why nuspec content is different on Mono
        ensureDir()
        let model =
            InstallModel.CreateFromLibs(PackageName "FSharp.Data.SqlClient", SemVer.Parse "1.4.4", FrameworkRestriction.NoRestriction,
                [ @"..\FSharp.Data.SqlClient\lib\net40\FSharp.Data.SqlClient.dll"
                  @"..\FSharp.Data.SqlClient\lib\net40\FSharp.Data.SqlClient.pdb"
                  @"..\FSharp.Data.SqlClient\lib\net40\FSharp.Data.SqlClient.XML"
                  @"..\FSharp.Data.SqlClient\lib\net40\Microsoft.SqlServer.TransactSql.ScriptDom.dll"
                  @"..\FSharp.Data.SqlClient\lib\net40\Microsoft.SqlServer.Types.dll" ]
                |> Paket.InstallModel.ProcessingSpecs.fromLegacyList @"..\FSharp.Data.SqlClient\",
                  [],
                  [],
                  Nuspec.Load(__SOURCE_DIRECTORY__ + @"\..\..\Nuspec\FSharp.Data.SqlClient.nuspec"))

        let ctx = ProjectFile.TryLoad("./ProjectFile/TestData/Empty.fsprojtest").Value.GenerateXml(model, System.Collections.Generic.HashSet<_>(),Map.empty,Some true,None,true,KnownTargetProfiles.AllProfiles,None)
        let currentXML = ctx.ChooseNodes.Head.OuterXml |> normalizeXml
        currentXML
        |> shouldEqual (normalizeXml expected)