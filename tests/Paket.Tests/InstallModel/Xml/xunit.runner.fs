module Paket.InstallModel.Xml.XUnitRunnerSpecs

open Paket
open NUnit.Framework
open FsUnit
open Paket.TestHelpers
open Paket.Domain
open Paket.Requirements

let emptyReferenceNodes = """<?xml version="1.0" encoding="utf-16"?>
<Choose xmlns="http://schemas.microsoft.com/developer/msbuild/2003" />"""

let expectedPropertyNodes = """<?xml version="1.0" encoding="utf-16"?>
<Import Project="..\..\..\xunit.runner.visualstudio\build\$(__paket__xunit_runner_visualstudio_props).props" Condition="Exists('..\..\..\xunit.runner.visualstudio\build\$(__paket__xunit_runner_visualstudio_props).props')" Label="Paket" xmlns="http://schemas.microsoft.com/developer/msbuild/2003" />"""

let expectedPropertyDefinitionNodes = """<?xml version="1.0" encoding="utf-16"?>
<Choose xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v2.0' Or $(TargetFrameworkVersion) == 'v3.0' Or $(TargetFrameworkVersion) == 'v3.5' Or $(TargetFrameworkVersion) == 'v4.0' Or $(TargetFrameworkVersion) == 'v4.0.3' Or $(TargetFrameworkVersion) == 'v4.5' Or $(TargetFrameworkVersion) == 'v4.5.1' Or $(TargetFrameworkVersion) == 'v4.5.2' Or $(TargetFrameworkVersion) == 'v4.5.3' Or $(TargetFrameworkVersion) == 'v4.6' Or $(TargetFrameworkVersion) == 'v4.6.1' Or $(TargetFrameworkVersion) == 'v4.6.2' Or $(TargetFrameworkVersion) == 'v4.6.3' Or $(TargetFrameworkVersion) == 'v4.7' Or $(TargetFrameworkVersion) == 'v4.7.1' Or $(TargetFrameworkVersion) == 'v4.7.2' Or $(TargetFrameworkVersion) == 'v4.8')">
    <PropertyGroup>
      <__paket__xunit_runner_visualstudio_props>net20\xunit.runner.visualstudio</__paket__xunit_runner_visualstudio_props>
    </PropertyGroup>
  </When>
  <When Condition="($(TargetFrameworkIdentifier) == 'WindowsPhoneApp') Or ($(TargetFrameworkIdentifier) == '.NETCore') Or ($(TargetFrameworkIdentifier) == 'MonoAndroid' And ($(TargetFrameworkVersion) == 'v1.0' Or $(TargetFrameworkVersion) == 'v2.2' Or $(TargetFrameworkVersion) == 'v2.3' Or $(TargetFrameworkVersion) == 'v4.0.3' Or $(TargetFrameworkVersion) == 'v4.1' Or $(TargetFrameworkVersion) == 'v4.2' Or $(TargetFrameworkVersion) == 'v4.3' Or $(TargetFrameworkVersion) == 'v4.4' Or $(TargetFrameworkVersion) == 'v5.0' Or $(TargetFrameworkVersion) == 'v5.1' Or $(TargetFrameworkVersion) == 'v6.0' Or $(TargetFrameworkVersion) == 'v7.0' Or $(TargetFrameworkVersion) == 'v7.1' Or $(TargetFrameworkVersion) == 'v8.0' Or $(TargetFrameworkVersion) == 'v8.1' Or $(TargetFrameworkVersion) == 'v9.0')) Or ($(TargetFrameworkIdentifier) == 'MonoTouch') Or ($(TargetFrameworkIdentifier) == '.NETCoreApp' And ($(TargetFrameworkVersion) == 'v1.0' Or $(TargetFrameworkVersion) == 'v1.1' Or $(TargetFrameworkVersion) == 'v2.0' Or $(TargetFrameworkVersion) == 'v2.1' Or $(TargetFrameworkVersion) == 'v2.2' Or $(TargetFrameworkVersion) == 'v3.0')) Or ($(TargetFrameworkIdentifier) == '.NETStandard' And ($(TargetFrameworkVersion) == 'v1.0' Or $(TargetFrameworkVersion) == 'v1.1' Or $(TargetFrameworkVersion) == 'v1.2' Or $(TargetFrameworkVersion) == 'v1.3' Or $(TargetFrameworkVersion) == 'v1.4' Or $(TargetFrameworkVersion) == 'v1.5' Or $(TargetFrameworkVersion) == 'v1.6' Or $(TargetFrameworkVersion) == 'v2.0' Or $(TargetFrameworkVersion) == 'v2.1')) Or ($(TargetFrameworkProfile) == 'Profile7') Or ($(TargetFrameworkProfile) == 'Profile78') Or ($(TargetFrameworkProfile) == 'Profile259') Or ($(TargetFrameworkProfile) == 'Profile111') Or ($(TargetFrameworkProfile) == 'Profile49') Or ($(TargetFrameworkProfile) == 'Profile44') Or ($(TargetFrameworkProfile) == 'Profile151') Or ($(TargetFrameworkProfile) == 'Profile31') Or ($(TargetFrameworkProfile) == 'Profile157') Or ($(TargetFrameworkProfile) == 'Profile32') Or ($(TargetFrameworkProfile) == 'Profile84') Or ($(TargetFrameworkIdentifier) == 'WindowsPhone' And ($(TargetFrameworkVersion) == 'v8.0' Or $(TargetFrameworkVersion) == 'v8.1')) Or ($(TargetFrameworkIdentifier) == 'Xamarin.iOS') Or ($(TargetFrameworkIdentifier) == 'Xamarin.Mac') Or ($(TargetFrameworkIdentifier) == 'Xamarin.tvOS') Or ($(TargetFrameworkIdentifier) == 'Xamarin.watchOS')">
    <PropertyGroup>
      <__paket__xunit_runner_visualstudio_props>portable-net45+aspnetcore50+win+wpa81+wp80+monotouch+monoandroid\xunit.runner.visualstudio</__paket__xunit_runner_visualstudio_props>
    </PropertyGroup>
  </When>
</Choose>"""

[<Test>]
let ``should generate Xml for xunit.runner.visualstudio 2.0.0``() =
    ensureDir()
    let model =
        InstallModel.CreateFromLibs(PackageName "xunit.runner.visualstudio", SemVer.Parse "2.50.0", InstallModelKind.Package, FrameworkRestriction.NoRestriction,[],
            [ @"..\xunit.runner.visualstudio\build\net20\xunit.runner.visualstudio.props"
              @"..\xunit.runner.visualstudio\build\portable-net45+aspnetcore50+win+wpa81+wp80+monotouch+monoandroid\xunit.runner.visualstudio.props"  ]
            |> Paket.InstallModel.ProcessingSpecs.fromLegacyList @"..\xunit.runner.visualstudio\",
            [],
              Nuspec.All)

    let ctx = ProjectFile.TryLoad("./ProjectFile/TestData/Empty.fsprojtest").Value.GenerateXml(model, System.Collections.Generic.HashSet<_>(),Map.empty,None,Some true,None,true,KnownTargetProfiles.AllProfiles,None)
    ctx.ChooseNodes.Head.OuterXml
    |> normalizeXml
    |> shouldEqual (normalizeXml emptyReferenceNodes)

    let currentXML = ctx.FrameworkSpecificPropertyChooseNode.OuterXml |> normalizeXml
    currentXML
    |> shouldEqual (normalizeXml expectedPropertyDefinitionNodes)

    ctx.FrameworkSpecificPropsNodes |> Seq.length |> shouldEqual 1
    ctx.FrameworkSpecificTargetsNodes |> Seq.length |> shouldEqual 0
    ctx.GlobalPropsNodes |> Seq.length |> shouldEqual 0
    ctx.GlobalTargetsNodes |> Seq.length |> shouldEqual 0

    (ctx.FrameworkSpecificPropsNodes |> Seq.head).OuterXml
    |> normalizeXml
    |> shouldEqual (normalizeXml expectedPropertyNodes)

let disabledChooseNode = """<?xml version="1.0" encoding="utf-16"?>
<Choose xmlns="http://schemas.microsoft.com/developer/msbuild/2003" />"""

[<Test>]
let ``should not generate Xml for xunit.runner.visualstudio 2.0.0 if import is disabled``() =
    ensureDir()
    let model =
        InstallModel.CreateFromLibs(PackageName "xunit.runner.visualstudio", SemVer.Parse "2.50.0", InstallModelKind.Package, FrameworkRestriction.NoRestriction,[],
            [ @"..\xunit.runner.visualstudio\build\net20\xunit.runner.visualstudio.props"
              @"..\xunit.runner.visualstudio\build\portable-net45+aspnetcore50+win+wpa81+wp80+monotouch+monoandroid\xunit.runner.visualstudio.props" ]
            |> Paket.InstallModel.ProcessingSpecs.fromLegacyList @"..\xunit.runner.visualstudio\",
              [],
              Nuspec.All)

    let ctx = ProjectFile.TryLoad("./ProjectFile/TestData/Empty.fsprojtest").Value.GenerateXml(model, System.Collections.Generic.HashSet<_>(),Map.empty,None,Some true,None,false,KnownTargetProfiles.AllProfiles,None)
    ctx.ChooseNodes.Head.OuterXml
    |> normalizeXml
    |> shouldEqual (normalizeXml emptyReferenceNodes)

    ctx.FrameworkSpecificPropertyChooseNode.OuterXml
    |> normalizeXml
    |> shouldEqual (normalizeXml disabledChooseNode)

    ctx.FrameworkSpecificPropsNodes |> Seq.length |> shouldEqual 0
