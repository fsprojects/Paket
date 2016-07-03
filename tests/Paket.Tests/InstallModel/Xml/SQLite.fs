module Paket.InstallModel.Xml.SQLiteSpecs

open Paket
open NUnit.Framework
open FsUnit
open Paket.TestHelpers
open Paket.Domain
open Paket.Requirements
open Paket.InstallModel

let expectedReferenceNodes = """
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
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v4.5.1' Or $(TargetFrameworkVersion) == 'v4.5.2' Or $(TargetFrameworkVersion) == 'v4.5.3' Or $(TargetFrameworkVersion) == 'v4.6' Or $(TargetFrameworkVersion) == 'v4.6.1' Or $(TargetFrameworkVersion) == 'v4.6.2' Or $(TargetFrameworkVersion) == 'v4.6.3')">
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
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v4.5.1' Or $(TargetFrameworkVersion) == 'v4.5.2' Or $(TargetFrameworkVersion) == 'v4.5.3' Or $(TargetFrameworkVersion) == 'v4.6' Or $(TargetFrameworkVersion) == 'v4.6.1' Or $(TargetFrameworkVersion) == 'v4.6.2' Or $(TargetFrameworkVersion) == 'v4.6.3')">
    <PropertyGroup>
      <__paket__System_Data_SQLite_Core_targets>net451\System.Data.SQLite.Core</__paket__System_Data_SQLite_Core_targets>
    </PropertyGroup>
  </When>
</Choose>"""

let expectedPropertyNodes = """
<Import Project="..\..\..\System.Data.SQLite.Core\build\$(__paket__System_Data_SQLite_Core_targets).targets" Condition="Exists('..\..\..\System.Data.SQLite.Core\build\$(__paket__System_Data_SQLite_Core_targets).targets')" Label="Paket" xmlns="http://schemas.microsoft.com/developer/msbuild/2003" />"""

[<Test>]
let ``best match for DotNet Standard 1.0``()=
    Paket.PlatformMatching.findBestMatch (["net20"; "net40"; "net45"; "net451"], SinglePlatform(DotNetStandard(DotNetStandardVersion.V1_0)))
    |> shouldEqual (None)


[<Test>]
let ``best match for DotNet Standard 1.1``()=
    Paket.PlatformMatching.findBestMatch (["net20"; "net40"; "net45"; "net451"], SinglePlatform(DotNetStandard(DotNetStandardVersion.V1_1)))
    |> shouldEqual (None)

[<Test>]
let ``best match for DotNet Standard 1.5``()=
    Paket.PlatformMatching.findBestMatch (["net20"; "net40"; "net45"; "net451"], SinglePlatform(DotNetStandard(DotNetStandardVersion.V1_5)))
    |> shouldEqual (None)

[<Test>]
let ``best match for net45``()=
    Paket.PlatformMatching.findBestMatch 
      (["netstandard10"; "netstandard11"; "netstandard12"; "netstandard13"; "netstandard14"; "netstandard15"; "netstandard16"], 
       SinglePlatform(DotNetFramework(FrameworkVersion.V4_5)))
    |> shouldEqual (Some ("netstandard11"))

[<Test>]
let ``best match for net451``()=
    Paket.PlatformMatching.findBestMatch 
      (["netstandard10"; "netstandard11"; "netstandard12"; "netstandard13"; "netstandard14"; "netstandard15"; "netstandard16"], 
       SinglePlatform(DotNetFramework(FrameworkVersion.V4_5_1)))
    |> shouldEqual (Some ("netstandard12"))

[<Test>]
let ``best match for net463``()=
    Paket.PlatformMatching.findBestMatch 
      (["netstandard10"; "netstandard11"; "netstandard12"; "netstandard13"; "netstandard14"; "netstandard15"; "netstandard16"], 
       SinglePlatform(DotNetFramework(FrameworkVersion.V4_6_3)))
    |> shouldEqual (Some ("netstandard16"))

[<Test>]
let ``can get supported target profile``()=
    let profiles = PlatformMatching.getSupportedTargetProfiles  ["net20"; "net40"; "net45"; "net451"; "netstandard14" ]
    let folder = profiles |> Seq.item 2 
    folder.Key |> shouldEqual "net45"
    folder.Value |> shouldEqual [SinglePlatform (DotNetFramework FrameworkVersion.V4_5)]

[<Test>]
let ``should extract lib folders for SQLite``() = 
    let libs =
        [@"..\System.Data.SQLite.Core\lib\net20\System.Data.SQLite.dll"
         @"..\System.Data.SQLite.Core\lib\net40\System.Data.SQLite.dll"
         @"..\System.Data.SQLite.Core\lib\net45\System.Data.SQLite.dll"
         @"..\System.Data.SQLite.Core\lib\net451\System.Data.SQLite.dll"]

    let model =
       libs 
        |> List.choose (extractLibFolder (PackageName "System.Data.SQLite.Core"))
        |> List.distinct 

    model |> shouldEqual ["net20"; "net40"; "net45"; "net451"]

[<Test>]
let ``should calc lib folders for SQLite``() = 
    let libs =
        [@"..\System.Data.SQLite.Core\lib\net20\System.Data.SQLite.dll"
         @"..\System.Data.SQLite.Core\lib\net40\System.Data.SQLite.dll"
         @"..\System.Data.SQLite.Core\lib\net45\System.Data.SQLite.dll"
         @"..\System.Data.SQLite.Core\lib\net451\System.Data.SQLite.dll"]

    let model = calcLibFolders (PackageName "System.Data.SQLite.Core") libs 
    let folder = model |> List.item 2
    folder.Targets |> shouldEqual [SinglePlatform (DotNetFramework FrameworkVersion.V4_5)]


[<Test>]
let ``should init model for SQLite``() = 
    let libs =
        [@"..\System.Data.SQLite.Core\lib\net20\System.Data.SQLite.dll"
         @"..\System.Data.SQLite.Core\lib\net40\System.Data.SQLite.dll"
         @"..\System.Data.SQLite.Core\lib\net45\System.Data.SQLite.dll"
         @"..\System.Data.SQLite.Core\lib\net451\System.Data.SQLite.dll"]

    let model =
        emptyModel (PackageName "System.Data.SQLite.Core") (SemVer.Parse "3.8.2")
        |> addLibReferences libs Nuspec.All.References

    let libFolder = model.LegacyReferenceFileFolders |> List.item 2
    libFolder.Name |> shouldEqual "net45"
    libFolder.Targets |> shouldEqual [SinglePlatform (DotNetFramework FrameworkVersion.V4_5)]


[<Test>]
let ``should generate model for SQLite``() = 
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
            [],
                Nuspec.All)

    let libFolder = model.LegacyReferenceFileFolders |> List.item 2
    libFolder.Name |> shouldEqual "net45"
    libFolder.Targets |> shouldEqual [SinglePlatform (DotNetFramework FrameworkVersion.V4_5)]

[<Test>]
let ``should generate Xml for SQLite``() = 
    ensureDir()
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
            [],
                Nuspec.All)


    let propsNodes,targetsNodes,chooseNode,propertyDefinitionNodes,_ = ProjectFile.TryLoad("./ProjectFile/TestData/Empty.fsprojtest").Value.GenerateXml(model,Map.empty,true,true,None)
    let currentXML = chooseNode.OuterXml |> normalizeXml
    currentXML |> shouldEqual (normalizeXml expectedReferenceNodes)

    let currentPropertyXML = propertyDefinitionNodes.OuterXml |> normalizeXml
    currentPropertyXML
    |> shouldEqual (normalizeXml expectedPropertyDefinitionNodes)

    propsNodes |> Seq.length |> shouldEqual 0
    targetsNodes |> Seq.length |> shouldEqual 1

    let currentTargetsXML = (targetsNodes |> Seq.head).OuterXml |> normalizeXml
    currentTargetsXML
    |> shouldEqual (normalizeXml expectedPropertyNodes)
