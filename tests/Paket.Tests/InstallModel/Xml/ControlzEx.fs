module Paket.InstallModel.Xml.ControlzEx

open FsUnit
open NUnit.Framework
open Paket
open Paket.Requirements
open Paket.Domain
open Paket.TestHelpers

let expectedWithoutSpecificVersion = """
<Choose xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v4.0' Or $(TargetFrameworkVersion) == 'v4.0.3')">
    <ItemGroup>
      <Reference Include="ControlzEx">
        <HintPath>..\..\..\ControlzEx\lib\net40\ControlzEx.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Windows.Interactivity">
        <HintPath>..\..\..\ControlzEx\lib\net40\System.Windows.Interactivity.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v4.5' Or $(TargetFrameworkVersion) == 'v4.5.1' Or $(TargetFrameworkVersion) == 'v4.5.2' Or $(TargetFrameworkVersion) == 'v4.5.3' Or $(TargetFrameworkVersion) == 'v4.6' Or $(TargetFrameworkVersion) == 'v4.6.1')">
    <ItemGroup>
      <Reference Include="ControlzEx">
        <HintPath>..\..\..\ControlzEx\lib\net45\ControlzEx.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Windows.Interactivity">
        <HintPath>..\..\..\ControlzEx\lib\net45\System.Windows.Interactivity.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v4.6.2' Or $(TargetFrameworkVersion) == 'v4.6.3' Or $(TargetFrameworkVersion) == 'v4.7' Or $(TargetFrameworkVersion) == 'v4.7.1' Or $(TargetFrameworkVersion) == 'v4.7.2' Or $(TargetFrameworkVersion) == 'v4.8')">
    <ItemGroup>
      <Reference Include="ControlzEx">
        <HintPath>..\..\..\ControlzEx\lib\net462\ControlzEx.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Windows.Interactivity">
        <HintPath>..\..\..\ControlzEx\lib\net462\System.Windows.Interactivity.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
</Choose>"""

let expectedWithSpecificVersionSetToTrue = """
<Choose xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v4.0' Or $(TargetFrameworkVersion) == 'v4.0.3')">
    <ItemGroup>
      <Reference Include="ControlzEx">
        <HintPath>..\..\..\ControlzEx\lib\net40\ControlzEx.dll</HintPath>
        <Private>True</Private>
        <SpecificVersion>True</SpecificVersion>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Windows.Interactivity">
        <HintPath>..\..\..\ControlzEx\lib\net40\System.Windows.Interactivity.dll</HintPath>
        <Private>True</Private>
        <SpecificVersion>True</SpecificVersion>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v4.5' Or $(TargetFrameworkVersion) == 'v4.5.1' Or $(TargetFrameworkVersion) == 'v4.5.2' Or $(TargetFrameworkVersion) == 'v4.5.3' Or $(TargetFrameworkVersion) == 'v4.6' Or $(TargetFrameworkVersion) == 'v4.6.1')">
    <ItemGroup>
      <Reference Include="ControlzEx">
        <HintPath>..\..\..\ControlzEx\lib\net45\ControlzEx.dll</HintPath>
        <Private>True</Private>
        <SpecificVersion>True</SpecificVersion>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Windows.Interactivity">
        <HintPath>..\..\..\ControlzEx\lib\net45\System.Windows.Interactivity.dll</HintPath>
        <Private>True</Private>
        <SpecificVersion>True</SpecificVersion>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v4.6.2' Or $(TargetFrameworkVersion) == 'v4.6.3' Or $(TargetFrameworkVersion) == 'v4.7' Or $(TargetFrameworkVersion) == 'v4.7.1' Or $(TargetFrameworkVersion) == 'v4.7.2' Or $(TargetFrameworkVersion) == 'v4.8')">
    <ItemGroup>
      <Reference Include="ControlzEx">
        <HintPath>..\..\..\ControlzEx\lib\net462\ControlzEx.dll</HintPath>
        <Private>True</Private>
        <SpecificVersion>True</SpecificVersion>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Windows.Interactivity">
        <HintPath>..\..\..\ControlzEx\lib\net462\System.Windows.Interactivity.dll</HintPath>
        <Private>True</Private>
        <SpecificVersion>True</SpecificVersion>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
</Choose>"""

let expectedWithSpecificVersionSetToFalse = """
<Choose xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v4.0' Or $(TargetFrameworkVersion) == 'v4.0.3')">
    <ItemGroup>
      <Reference Include="ControlzEx">
        <HintPath>..\..\..\ControlzEx\lib\net40\ControlzEx.dll</HintPath>
        <Private>True</Private>
        <SpecificVersion>False</SpecificVersion>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Windows.Interactivity">
        <HintPath>..\..\..\ControlzEx\lib\net40\System.Windows.Interactivity.dll</HintPath>
        <Private>True</Private>
        <SpecificVersion>False</SpecificVersion>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v4.5' Or $(TargetFrameworkVersion) == 'v4.5.1' Or $(TargetFrameworkVersion) == 'v4.5.2' Or $(TargetFrameworkVersion) == 'v4.5.3' Or $(TargetFrameworkVersion) == 'v4.6' Or $(TargetFrameworkVersion) == 'v4.6.1')">
    <ItemGroup>
      <Reference Include="ControlzEx">
        <HintPath>..\..\..\ControlzEx\lib\net45\ControlzEx.dll</HintPath>
        <Private>True</Private>
        <SpecificVersion>False</SpecificVersion>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Windows.Interactivity">
        <HintPath>..\..\..\ControlzEx\lib\net45\System.Windows.Interactivity.dll</HintPath>
        <Private>True</Private>
        <SpecificVersion>False</SpecificVersion>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v4.6.2' Or $(TargetFrameworkVersion) == 'v4.6.3' Or $(TargetFrameworkVersion) == 'v4.7' Or $(TargetFrameworkVersion) == 'v4.7.1' Or $(TargetFrameworkVersion) == 'v4.7.2' Or $(TargetFrameworkVersion) == 'v4.8')">
    <ItemGroup>
      <Reference Include="ControlzEx">
        <HintPath>..\..\..\ControlzEx\lib\net462\ControlzEx.dll</HintPath>
        <Private>True</Private>
        <SpecificVersion>False</SpecificVersion>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Windows.Interactivity">
        <HintPath>..\..\..\ControlzEx\lib\net462\System.Windows.Interactivity.dll</HintPath>
        <Private>True</Private>
        <SpecificVersion>False</SpecificVersion>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
</Choose>"""

[<Test>]
let ``should generate Xml without specific version for ControlzEx in CSharp project``() = 
    ensureDir()
    let model =
        InstallModel.CreateFromLibs(PackageName "ControlzEx", SemVer.Parse "3.0.1", InstallModelKind.Package, FrameworkRestriction.NoRestriction,
            [ @"..\ControlzEx\lib\net40\ControlzEx.dll"
              @"..\ControlzEx\lib\net40\System.Windows.Interactivity.dll"
              @"..\ControlzEx\lib\net45\ControlzEx.dll"
              @"..\ControlzEx\lib\net45\System.Windows.Interactivity.dll"
              @"..\ControlzEx\lib\net462\ControlzEx.dll"
              @"..\ControlzEx\lib\net462\System.Windows.Interactivity.dll"]
            |> Paket.InstallModel.ProcessingSpecs.fromLegacyList @"..\ControlzEx\",
            [],
            [],
            Nuspec.All)

    let project = ProjectFile.TryLoad("./ProjectFile/TestData/EmptyCsharpGuid.csprojtest")
    Assert.IsTrue(project.IsSome)
    let ctx = project.Value.GenerateXml(model, System.Collections.Generic.HashSet<_>(),Map.empty,None,None,None,true,KnownTargetProfiles.AllProfiles,None)
    let result =
      ctx.ChooseNodes
      |> (fun n -> n.Head.OuterXml)
      |> normalizeXml
    let expectedXml = normalizeXml expectedWithoutSpecificVersion
    result |> shouldEqual expectedXml

[<Test>]
let ``should generate Xml with specific version set to true for ControlzEx in CSharp project``() = 
    ensureDir()
    let model =
        InstallModel.CreateFromLibs(PackageName "ControlzEx", SemVer.Parse "3.0.1", InstallModelKind.Package, FrameworkRestriction.NoRestriction,
            [ @"..\ControlzEx\lib\net40\ControlzEx.dll"
              @"..\ControlzEx\lib\net40\System.Windows.Interactivity.dll"
              @"..\ControlzEx\lib\net45\ControlzEx.dll"
              @"..\ControlzEx\lib\net45\System.Windows.Interactivity.dll"
              @"..\ControlzEx\lib\net462\ControlzEx.dll"
              @"..\ControlzEx\lib\net462\System.Windows.Interactivity.dll"]
            |> Paket.InstallModel.ProcessingSpecs.fromLegacyList @"..\ControlzEx\",
            [],
            [],
            Nuspec.All)

    let project = ProjectFile.TryLoad("./ProjectFile/TestData/EmptyCsharpGuid.csprojtest")
    Assert.IsTrue(project.IsSome)
    let ctx = project.Value.GenerateXml(model, System.Collections.Generic.HashSet<_>(),Map.empty,None,None,Some true,true,KnownTargetProfiles.AllProfiles,None)
    let result =
      ctx.ChooseNodes
      |> (fun n -> n.Head.OuterXml)
      |> normalizeXml
    let expectedXml = normalizeXml expectedWithSpecificVersionSetToTrue
    result |> shouldEqual expectedXml

[<Test>]
let ``should generate Xml with specific version set to false for ControlzEx in CSharp project``() = 
    ensureDir()
    let model =
        InstallModel.CreateFromLibs(PackageName "ControlzEx", SemVer.Parse "3.0.1", InstallModelKind.Package, FrameworkRestriction.NoRestriction,
            [ @"..\ControlzEx\lib\net40\ControlzEx.dll"
              @"..\ControlzEx\lib\net40\System.Windows.Interactivity.dll"
              @"..\ControlzEx\lib\net45\ControlzEx.dll"
              @"..\ControlzEx\lib\net45\System.Windows.Interactivity.dll"
              @"..\ControlzEx\lib\net462\ControlzEx.dll"
              @"..\ControlzEx\lib\net462\System.Windows.Interactivity.dll"]
            |> Paket.InstallModel.ProcessingSpecs.fromLegacyList @"..\ControlzEx\",
            [],
            [],
            Nuspec.All)

    let project = ProjectFile.TryLoad("./ProjectFile/TestData/EmptyCsharpGuid.csprojtest")
    Assert.IsTrue(project.IsSome)
    let ctx = project.Value.GenerateXml(model, System.Collections.Generic.HashSet<_>(),Map.empty,None,None,Some false,true,KnownTargetProfiles.AllProfiles,None)
    let result =
      ctx.ChooseNodes
      |> (fun n -> n.Head.OuterXml)
      |> normalizeXml
    let expectedXml = normalizeXml expectedWithSpecificVersionSetToFalse
    result |> shouldEqual expectedXml