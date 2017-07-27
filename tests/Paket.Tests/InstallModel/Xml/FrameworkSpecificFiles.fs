module Paket.InstallModel.Xml.FrameworkSpecificFilesSpecs

open Paket
open NUnit.Framework
open FsUnit
open Paket.TestHelpers
open Paket.Domain
open Paket.Requirements

let modelFromFiles files =
    
    InstallModel.CreateFromLibs(PackageName "FrameworkSpecificFiles", SemVer.Parse "0.21", FrameworkRestriction.NoRestriction,
        []
        |> Paket.InstallModel.ProcessingSpecs.fromLegacyList @"..\FrameworkSpecificFiles\",
        files
        |> Paket.InstallModel.ProcessingSpecs.fromLegacyList @"..\FrameworkSpecificFiles\",
        [],
            Nuspec.All)

[<Test>]
let ``https://github.com/fsprojects/Paket/issues/2392``() =
    
    // we have two folders, but it should only ever take one
    let files = [ @"..\FrameworkSpecificFiles\build\net40\FrameworkSpecificFiles.props"
                  @"..\FrameworkSpecificFiles\build\FrameworkSpecificFiles.props"
    ]

    let expectedPropertyDefinitionNodes = """<?xml version="1.0" encoding="utf-16"?>
<Choose xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <When Condition="1 == 1">
    <PropertyGroup>
      <__paket__FrameworkSpecificFiles_props>FrameworkSpecificFiles</__paket__FrameworkSpecificFiles_props>
    </PropertyGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v4.0' Or $(TargetFrameworkVersion) == 'v4.0.3' Or $(TargetFrameworkVersion) == 'v4.5' Or $(TargetFrameworkVersion) == 'v4.5.1' Or $(TargetFrameworkVersion) == 'v4.5.2' Or $(TargetFrameworkVersion) == 'v4.5.3' Or $(TargetFrameworkVersion) == 'v4.6' Or $(TargetFrameworkVersion) == 'v4.6.1' Or $(TargetFrameworkVersion) == 'v4.6.2' Or $(TargetFrameworkVersion) == 'v4.6.3' Or $(TargetFrameworkVersion) == 'v4.7')">
    <PropertyGroup>
      <__paket__FrameworkSpecificFiles_props>net40\FrameworkSpecificFiles</__paket__FrameworkSpecificFiles_props>
    </PropertyGroup>
  </When>
</Choose>"""

    let expectedPropertyNodes = """<?xml version="1.0" encoding="utf-16"?>
<Import Project="..\..\..\FrameworkSpecificFiles\build\$(__paket__FrameworkSpecificFiles_props).props" Condition="Exists('..\..\..\FrameworkSpecificFiles\build\$(__paket__FrameworkSpecificFiles_props).props')" Label="Paket" xmlns="http://schemas.microsoft.com/developer/msbuild/2003" />"""

    ensureDir()
    let model = modelFromFiles files

    let ctx = ProjectFile.TryLoad("./ProjectFile/TestData/Empty.fsprojtest").Value.GenerateXml(model, System.Collections.Generic.HashSet<_>(),Map.empty,Some true,None,true,KnownTargetProfiles.AllProfiles,None)

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


[<Test>]
let ``https://github.com/fsprojects/Paket/issues/2347``() =
    
    let files = [ @"..\FrameworkSpecificFiles\build\FrameworkSpecificFiles.props" ]

    let expectedPropertyDefinitionNodes = """<?xml version="1.0" encoding="utf-16"?>
<Choose xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <When Condition="'$(DEBUG)' == 'True'">
    <PropertyGroup>
      <__paket__FrameworkSpecificFiles_props>FrameworkSpecificFiles</__paket__FrameworkSpecificFiles_props>
    </PropertyGroup>
  </When>
</Choose>"""

    let expectedPropertyNodes = """<?xml version="1.0" encoding="utf-16"?>
<Import Project="..\..\..\FrameworkSpecificFiles\build\$(__paket__FrameworkSpecificFiles_props).props" Condition="Exists('..\..\..\FrameworkSpecificFiles\build\$(__paket__FrameworkSpecificFiles_props).props')" Label="Paket" xmlns="http://schemas.microsoft.com/developer/msbuild/2003" />"""

    ensureDir()
    let model = modelFromFiles files

    let ctx = ProjectFile.TryLoad("./ProjectFile/TestData/Empty.fsprojtest").Value.GenerateXml(model, System.Collections.Generic.HashSet<_>(),Map.empty,Some true,None,true,KnownTargetProfiles.AllProfiles,Some "DEBUG")

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


[<Test>]
let ``empty build dirs are respected``() =
    
    // for 4.7 and up, it should not include any files.
    // for others, it should fall back to /build/ and include the file.
    let files = [ @"..\FrameworkSpecificFiles\build\FrameworkSpecificFiles.props"
                  @"..\FrameworkSpecificFiles\build\net47\_._" ]

    let expectedPropertyDefinitionNodes = """<?xml version="1.0" encoding="utf-16"?>
<Choose xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <When Condition="1 == 1">
    <PropertyGroup>
      <__paket__FrameworkSpecificFiles_props>FrameworkSpecificFiles</__paket__FrameworkSpecificFiles_props>
    </PropertyGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And $(TargetFrameworkVersion) == 'v4.7'" />
</Choose>"""

    let expectedPropertyNodes = """<?xml version="1.0" encoding="utf-16"?>
<Import Project="..\..\..\FrameworkSpecificFiles\build\$(__paket__FrameworkSpecificFiles_props).props" Condition="Exists('..\..\..\FrameworkSpecificFiles\build\$(__paket__FrameworkSpecificFiles_props).props')" Label="Paket" xmlns="http://schemas.microsoft.com/developer/msbuild/2003" />"""

    ensureDir()
    let model = modelFromFiles files

    let ctx = ProjectFile.TryLoad("./ProjectFile/TestData/Empty.fsprojtest").Value.GenerateXml(model, System.Collections.Generic.HashSet<_>(),Map.empty,Some true,None,true,KnownTargetProfiles.AllProfiles,None)

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
