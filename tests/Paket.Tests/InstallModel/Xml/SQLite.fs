module Paket.InstallModel.Xml.SQLiteSpecs

open Paket
open NUnit.Framework
open FsUnit
open Paket.TestHelpers
open Paket.Domain
open Paket.Requirements
open Paket.InstallModel
open Paket.PlatformMatching

let fromLegacyList = Paket.InstallModel.ProcessingSpecs.fromLegacyList

let expectedReferenceNodes = """
<Choose xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v2.0' Or $(TargetFrameworkVersion) == 'v3.0' Or $(TargetFrameworkVersion) == 'v3.5')">
    <ItemGroup>
      <Reference Include="System.Data.SQLite">
        <HintPath>..\..\..\System.Data.SQLite.Core\lib\net20\System.Data.SQLite.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v4.0' Or $(TargetFrameworkVersion) == 'v4.0.3')">
    <ItemGroup>
      <Reference Include="System.Data.SQLite">
        <HintPath>..\..\..\System.Data.SQLite.Core\lib\net40\System.Data.SQLite.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And $(TargetFrameworkVersion) == 'v4.5'">
    <ItemGroup>
      <Reference Include="System.Data.SQLite">
        <HintPath>..\..\..\System.Data.SQLite.Core\lib\net45\System.Data.SQLite.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v4.5.1' Or $(TargetFrameworkVersion) == 'v4.5.2' Or $(TargetFrameworkVersion) == 'v4.5.3' Or $(TargetFrameworkVersion) == 'v4.6' Or $(TargetFrameworkVersion) == 'v4.6.1' Or $(TargetFrameworkVersion) == 'v4.6.2' Or $(TargetFrameworkVersion) == 'v4.6.3' Or $(TargetFrameworkVersion) == 'v4.7' Or $(TargetFrameworkVersion) == 'v4.7.1' Or $(TargetFrameworkVersion) == 'v4.7.2' Or $(TargetFrameworkVersion) == 'v4.8')">
    <ItemGroup>
      <Reference Include="System.Data.SQLite">
        <HintPath>..\..\..\System.Data.SQLite.Core\lib\net451\System.Data.SQLite.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
</Choose>"""


let expectedPropertyDefinitionNodes = """
<Choose xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v2.0' Or $(TargetFrameworkVersion) == 'v3.0' Or $(TargetFrameworkVersion) == 'v3.5')">
    <PropertyGroup>
      <__paket__System_Data_SQLite_Core_targets>net20\System.Data.SQLite.Core</__paket__System_Data_SQLite_Core_targets>
    </PropertyGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v4.0' Or $(TargetFrameworkVersion) == 'v4.0.3')">
    <PropertyGroup>
      <__paket__System_Data_SQLite_Core_targets>net40\System.Data.SQLite.Core</__paket__System_Data_SQLite_Core_targets>
    </PropertyGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And $(TargetFrameworkVersion) == 'v4.5'">
    <PropertyGroup>
      <__paket__System_Data_SQLite_Core_targets>net45\System.Data.SQLite.Core</__paket__System_Data_SQLite_Core_targets>
    </PropertyGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v4.5.1' Or $(TargetFrameworkVersion) == 'v4.5.2' Or $(TargetFrameworkVersion) == 'v4.5.3' Or $(TargetFrameworkVersion) == 'v4.6' Or $(TargetFrameworkVersion) == 'v4.6.1' Or $(TargetFrameworkVersion) == 'v4.6.2' Or $(TargetFrameworkVersion) == 'v4.6.3' Or $(TargetFrameworkVersion) == 'v4.7' Or $(TargetFrameworkVersion) == 'v4.7.1' Or $(TargetFrameworkVersion) == 'v4.7.2' Or $(TargetFrameworkVersion) == 'v4.8')">
    <PropertyGroup>
      <__paket__System_Data_SQLite_Core_targets>net451\System.Data.SQLite.Core</__paket__System_Data_SQLite_Core_targets>
    </PropertyGroup>
  </When>
</Choose>"""

let expectedPropertyNodes = """
<Import Project="..\..\..\System.Data.SQLite.Core\build\$(__paket__System_Data_SQLite_Core_targets).targets" Condition="Exists('..\..\..\System.Data.SQLite.Core\build\$(__paket__System_Data_SQLite_Core_targets).targets')" Label="Paket" xmlns="http://schemas.microsoft.com/developer/msbuild/2003" />"""

[<Test>]
let ``can get supported target profile``()=
    let profiles =
        ["net20"; "net40"; "net45"; "net451"; "netstandard14" ]
        |> List.map forceExtractPlatforms
        |> PlatformMatching.getSupportedTargetProfiles
    let folder = profiles |> Seq.item 2
    folder.Key |> shouldEqual (forceExtractPlatforms "net45")
    folder.Value |> Set.toList |> shouldEqual [TargetProfile.SinglePlatform (DotNetFramework FrameworkVersion.V4_5)]

[<Test>]
let ``should extract lib folders for SQLite``() =
    let libs =
        [@"..\System.Data.SQLite.Core\lib\net20\System.Data.SQLite.dll"
         @"..\System.Data.SQLite.Core\lib\net40\System.Data.SQLite.dll"
         @"..\System.Data.SQLite.Core\lib\net45\System.Data.SQLite.dll"
         @"..\System.Data.SQLite.Core\lib\net451\System.Data.SQLite.dll"]
        |> fromLegacyList @"..\System.Data.SQLite.Core\"

    let model =
       libs
        |> List.choose getCompileLibAssembly
        |> List.distinct

    model
    |> List.map (fun m -> m.Path.Name)
    |> shouldEqual ["net20"; "net40"; "net45"; "net451"]

[<Test>]
let ``should calc lib folders for SQLite``() =
    let libs =
        [@"..\System.Data.SQLite.Core\lib\net20\System.Data.SQLite.dll"
         @"..\System.Data.SQLite.Core\lib\net40\System.Data.SQLite.dll"
         @"..\System.Data.SQLite.Core\lib\net45\System.Data.SQLite.dll"
         @"..\System.Data.SQLite.Core\lib\net451\System.Data.SQLite.dll"]
        |> fromLegacyList @"..\System.Data.SQLite.Core\"

    let model = calcLegacyReferenceLibFolders libs
    let folder = model |> List.item 2
    folder.Targets |> Set.toList |> shouldEqual [TargetProfile.SinglePlatform (DotNetFramework FrameworkVersion.V4_5)]


[<Test>]
let ``should init model for SQLite``() =
    let libs =
        [@"..\System.Data.SQLite.Core\lib\net20\System.Data.SQLite.dll"
         @"..\System.Data.SQLite.Core\lib\net40\System.Data.SQLite.dll"
         @"..\System.Data.SQLite.Core\lib\net45\System.Data.SQLite.dll"
         @"..\System.Data.SQLite.Core\lib\net451\System.Data.SQLite.dll"]
        |> fromLegacyList @"..\System.Data.SQLite.Core\"

    let model =
        emptyModel (PackageName "System.Data.SQLite.Core") (SemVer.Parse "3.8.2") InstallModelKind.Package
        |> addLibReferences libs Nuspec.All.References

    let libFolder = model.CompileLibFolders |> List.item 2
    libFolder.Path.Name |> shouldEqual "net45"
    libFolder.Targets |> Set.toList |> shouldEqual [TargetProfile.SinglePlatform (DotNetFramework FrameworkVersion.V4_5)]


[<Test>]
let ``should generate model for SQLite``() =
    let model =
        InstallModel.CreateFromLibs(PackageName "System.Data.SQLite.Core", SemVer.Parse "3.8.2", InstallModelKind.Package, FrameworkRestriction.NoRestriction,
            [ @"..\System.Data.SQLite.Core\lib\net20\System.Data.SQLite.dll"
              @"..\System.Data.SQLite.Core\lib\net40\System.Data.SQLite.dll"
              @"..\System.Data.SQLite.Core\lib\net45\System.Data.SQLite.dll"
              @"..\System.Data.SQLite.Core\lib\net451\System.Data.SQLite.dll"]
            |> fromLegacyList @"..\System.Data.SQLite.Core\",
            [ @"..\System.Data.SQLite.Core\build\net20\System.Data.SQLite.Core.targets"
              @"..\System.Data.SQLite.Core\build\net40\System.Data.SQLite.Core.targets"
              @"..\System.Data.SQLite.Core\build\net45\System.Data.SQLite.Core.targets"
              @"..\System.Data.SQLite.Core\build\net451\System.Data.SQLite.Core.targets" ]
            |> fromLegacyList @"..\System.Data.SQLite.Core\",
            [],
                Nuspec.All)

    let libFolder = model.CompileLibFolders |> List.item 2
    libFolder.Path.Name |> shouldEqual "net45"
    libFolder.Targets |> Set.toList |> shouldEqual [TargetProfile.SinglePlatform (DotNetFramework FrameworkVersion.V4_5)]

[<Test>]
let ``should generate Xml for SQLite``() =
    ensureDir()
    let model =
        InstallModel.CreateFromLibs(PackageName "System.Data.SQLite.Core", SemVer.Parse "3.8.2", InstallModelKind.Package, FrameworkRestriction.NoRestriction,
            [ @"..\System.Data.SQLite.Core\lib\net20\System.Data.SQLite.dll"
              @"..\System.Data.SQLite.Core\lib\net40\System.Data.SQLite.dll"
              @"..\System.Data.SQLite.Core\lib\net45\System.Data.SQLite.dll"
              @"..\System.Data.SQLite.Core\lib\net451\System.Data.SQLite.dll"]
            |> fromLegacyList @"..\System.Data.SQLite.Core\",
            [ @"..\System.Data.SQLite.Core\build\net20\System.Data.SQLite.Core.targets"
              @"..\System.Data.SQLite.Core\build\net40\System.Data.SQLite.Core.targets"
              @"..\System.Data.SQLite.Core\build\net45\System.Data.SQLite.Core.targets"
              @"..\System.Data.SQLite.Core\build\net451\System.Data.SQLite.Core.targets" ]
            |> fromLegacyList @"..\System.Data.SQLite.Core\",
            [],
                Nuspec.All)


    let ctx = ProjectFile.TryLoad("./ProjectFile/TestData/Empty.fsprojtest").Value.GenerateXml(model, System.Collections.Generic.HashSet<_>(),Map.empty,None,Some true,None,true,KnownTargetProfiles.AllProfiles,None)
    let currentXML = ctx.ChooseNodes.Head.OuterXml |> normalizeXml
    currentXML |> shouldEqual (normalizeXml expectedReferenceNodes)

    let currentPropertyXML = ctx.FrameworkSpecificPropertyChooseNode.OuterXml |> normalizeXml
    currentPropertyXML
    |> shouldEqual (normalizeXml expectedPropertyDefinitionNodes)

    ctx.FrameworkSpecificPropsNodes |> Seq.length |> shouldEqual 0
    ctx.FrameworkSpecificTargetsNodes |> Seq.length |> shouldEqual 1
    ctx.GlobalPropsNodes |> Seq.length |> shouldEqual 0
    ctx.GlobalTargetsNodes |> Seq.length |> shouldEqual 0

    let currentTargetsXML = (ctx.FrameworkSpecificTargetsNodes |> Seq.head).OuterXml |> normalizeXml
    currentTargetsXML
    |> shouldEqual (normalizeXml expectedPropertyNodes)
