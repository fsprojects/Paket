module Paket.InstallModel.Xml.XUniRunnerSpecs

open Paket
open NUnit.Framework
open FsUnit
open Paket.TestHelpers
open Paket.Domain
open Paket.Requirements

let expected = """<?xml version="1.0" encoding="utf-16"?>
<Choose xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <When Condition="($(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v2.0' Or $(TargetFrameworkVersion) == 'v3.0' Or $(TargetFrameworkVersion) == 'v3.5' Or $(TargetFrameworkVersion) == 'v4.0' Or $(TargetFrameworkVersion) == 'v4.5' Or $(TargetFrameworkVersion) == 'v4.5.1' Or $(TargetFrameworkVersion) == 'v4.5.2' Or $(TargetFrameworkVersion) == 'v4.5.3')) Or ($(TargetFrameworkIdentifier) == 'MonoAndroid') Or ($(TargetFrameworkIdentifier) == 'MonoTouch')">
    <PropertyGroup>
      <__paket__xunit_runner_visualstudio_props>net20\xunit.runner.visualstudio</__paket__xunit_runner_visualstudio_props>
    </PropertyGroup>
  </When>
  <When Condition="($(TargetFrameworkIdentifier) == 'WindowsPhoneApp') Or ($(TargetFrameworkIdentifier) == '.NETCore') Or ($(TargetFrameworkIdentifier) == 'WindowsPhone' And ($(TargetFrameworkVersion) == 'v8.0' Or $(TargetFrameworkVersion) == 'v8.1')) Or ($(TargetFrameworkProfile) == 'Profile7') Or ($(TargetFrameworkProfile) == 'Profile31') Or ($(TargetFrameworkProfile) == 'Profile32') Or ($(TargetFrameworkProfile) == 'Profile44') Or ($(TargetFrameworkProfile) == 'Profile49') Or ($(TargetFrameworkProfile) == 'Profile78') Or ($(TargetFrameworkProfile) == 'Profile84') Or ($(TargetFrameworkProfile) == 'Profile111') Or ($(TargetFrameworkProfile) == 'Profile151') Or ($(TargetFrameworkProfile) == 'Profile157') Or ($(TargetFrameworkProfile) == 'Profile259')">
    <PropertyGroup>
      <__paket__xunit_runner_visualstudio_props>portable-net45+aspnetcore50+win+wpa81+wp80+monotouch+monoandroid\xunit.runner.visualstudio</__paket__xunit_runner_visualstudio_props>
    </PropertyGroup>
  </When>
</Choose>"""

let expectedPropertyNdoes = """<?xml version="1.0" encoding="utf-16"?>
<Import Project="..\..\..\xunit.runner.visualstudio\build\$(__paket__xunit_runner_visualstudio_props).props" Condition="Exists('..\..\..\xunit.runner.visualstudio\build\$(__paket__xunit_runner_visualstudio_props).props')" xmlns="http://schemas.microsoft.com/developer/msbuild/2003" />"""

[<Test>]
let ``should generate Xml for xunit.runner.visualstudio 2.0.0``() = 
    let model =
        InstallModel.CreateFromLibs(PackageName "xunit.runner.visualstudio", SemVer.Parse "2.50.0", [],[],
            [ @"..\xunit.runner.visualstudio\build\net20\xunit.runner.visualstudio.props" 
              @"..\xunit.runner.visualstudio\build\portable-net45+aspnetcore50+win+wpa81+wp80+monotouch+monoandroid\xunit.runner.visualstudio.props"  ],
              Nuspec.All)
    
    let propertyNodes,chooseNode,additionalNode = ProjectFile.Load("./ProjectFile/TestData/Empty.fsprojtest").Value.GenerateXml(model,CopyLocal.True)
    chooseNode.OuterXml
    |> normalizeXml
    |> shouldEqual (normalizeXml expected)

    additionalNode |> shouldEqual None

    propertyNodes |> Seq.length |> shouldEqual 1

    (propertyNodes |> Seq.head).OuterXml
    |> normalizeXml
    |> shouldEqual (normalizeXml expectedPropertyNdoes)