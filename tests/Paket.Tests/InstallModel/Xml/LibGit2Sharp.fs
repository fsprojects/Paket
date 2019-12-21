module Paket.InstallModel.Xml.LibGit2SharpSpecs

open Paket
open NUnit.Framework
open FsUnit
open Paket.TestHelpers
open Paket.Domain
open Paket.Requirements

let expectedReferenceNodes = """<?xml version="1.0" encoding="utf-16"?>
<Choose xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v4.0' Or $(TargetFrameworkVersion) == 'v4.0.3' Or $(TargetFrameworkVersion) == 'v4.5' Or $(TargetFrameworkVersion) == 'v4.5.1' Or $(TargetFrameworkVersion) == 'v4.5.2' Or $(TargetFrameworkVersion) == 'v4.5.3' Or $(TargetFrameworkVersion) == 'v4.6' Or $(TargetFrameworkVersion) == 'v4.6.1' Or $(TargetFrameworkVersion) == 'v4.6.2' Or $(TargetFrameworkVersion) == 'v4.6.3' Or $(TargetFrameworkVersion) == 'v4.7' Or $(TargetFrameworkVersion) == 'v4.7.1' Or $(TargetFrameworkVersion) == 'v4.7.2' Or $(TargetFrameworkVersion) == 'v4.8')">
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
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v4.0' Or $(TargetFrameworkVersion) == 'v4.0.3' Or $(TargetFrameworkVersion) == 'v4.5' Or $(TargetFrameworkVersion) == 'v4.5.1' Or $(TargetFrameworkVersion) == 'v4.5.2' Or $(TargetFrameworkVersion) == 'v4.5.3' Or $(TargetFrameworkVersion) == 'v4.6' Or $(TargetFrameworkVersion) == 'v4.6.1' Or $(TargetFrameworkVersion) == 'v4.6.2' Or $(TargetFrameworkVersion) == 'v4.6.3' Or $(TargetFrameworkVersion) == 'v4.7' Or $(TargetFrameworkVersion) == 'v4.7.1' Or $(TargetFrameworkVersion) == 'v4.7.2' Or $(TargetFrameworkVersion) == 'v4.8')">
    <PropertyGroup>
      <__paket__LibGit2Sharp_props>net40\LibGit2Sharp</__paket__LibGit2Sharp_props>
    </PropertyGroup>
  </When>
</Choose>"""

let expectedPropertyNodes = """<?xml version="1.0" encoding="utf-16"?>
<Import Project="..\..\..\LibGit2Sharp\build\$(__paket__LibGit2Sharp_props).props" Condition="Exists('..\..\..\LibGit2Sharp\build\$(__paket__LibGit2Sharp_props).props')" Label="Paket" xmlns="http://schemas.microsoft.com/developer/msbuild/2003" />"""

[<Test>]
let ``should generate Xml for LibGit2Sharp 2.0.0``() =
    ensureDir()
    let model =
        InstallModel.CreateFromLibs(PackageName "LibGit2Sharp", SemVer.Parse "0.21", InstallModelKind.Package, FrameworkRestriction.NoRestriction,
            [ @"..\LibGit2Sharp\lib\net40\LibGit2Sharp.dll" ]
            |> Paket.InstallModel.ProcessingSpecs.fromLegacyList @"..\LibGit2Sharp\",
            [ @"..\LibGit2Sharp\build\net40\LibGit2Sharp.props" ]
            |> Paket.InstallModel.ProcessingSpecs.fromLegacyList @"..\LibGit2Sharp\",
            [],
              Nuspec.All)

    model.GetLegacyReferences(TargetProfile.SinglePlatform (DotNetFramework FrameworkVersion.V4))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\LibGit2Sharp\lib\net40\LibGit2Sharp.dll"

    let ctx = ProjectFile.TryLoad("./ProjectFile/TestData/Empty.fsprojtest").Value.GenerateXml(model, System.Collections.Generic.HashSet<_>(),Map.empty,None,Some true,None,true,KnownTargetProfiles.AllProfiles,None)
    ctx.ChooseNodes.Head.OuterXml
    |> normalizeXml
    |> shouldEqual (normalizeXml expectedReferenceNodes)

    ctx.FrameworkSpecificPropertyChooseNode.OuterXml
    |> normalizeXml
    |> shouldEqual (normalizeXml expectedPropertyDefinitionNodes)

    ctx.FrameworkSpecificPropsNodes |> Seq.length |> shouldEqual 1
    ctx.FrameworkSpecificTargetsNodes |> Seq.length |> shouldEqual 0
    ctx.GlobalPropsNodes |> Seq.length |> shouldEqual 0
    ctx.GlobalTargetsNodes |> Seq.length |> shouldEqual 0

    (ctx.FrameworkSpecificPropsNodes |> Seq.head).OuterXml
    |> normalizeXml
    |> shouldEqual (normalizeXml expectedPropertyNodes)