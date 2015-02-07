module Paket.InstallModel.Xml.GitInfoPlanterSpecs

open Paket
open NUnit.Framework
open FsUnit
open Paket.TestHelpers
open Paket.Domain
open Paket.Requirements

let emptyReferences = """<?xml version="1.0" encoding="utf-16"?>
<Choose xmlns="http://schemas.microsoft.com/developer/msbuild/2003" />"""

let expectedPropertyDefinitionNodes = """<?xml version="1.0" encoding="utf-16"?>
<PropertyGroup xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <__paket__GitInfoPlanter_targets>GitInfoPlanter</__paket__GitInfoPlanter_targets>
</PropertyGroup>"""

let expectedPropertyNodes = """<?xml version="1.0" encoding="utf-16"?>
<Import Project="..\..\..\GitInfoPlanter\build\$(__paket__GitInfoPlanter_targets).targets" Condition="Exists('..\..\..\GitInfoPlanter\build\$(__paket__GitInfoPlanter_targets).targets')" xmlns="http://schemas.microsoft.com/developer/msbuild/2003" />"""

[<Test>]
let ``should generate Xml for GitInfoPlanter2.0.0``() = 
    let model =
        InstallModel.CreateFromLibs(PackageName "GitInfoPlanter", SemVer.Parse "0.21", [],
            [ ],
            [ @"..\GitInfoPlanter\build\GitInfoPlanter.targets" ],
              Nuspec.All)

    let propertyNodes,chooseNode,propertyChooseNode = ProjectFile.Load("./ProjectFile/TestData/Empty.fsprojtest").Value.GenerateXml(model,CopyLocal.True)
    chooseNode.OuterXml
    |> normalizeXml
    |> shouldEqual (normalizeXml emptyReferences)

    propertyChooseNode.OuterXml
    |> normalizeXml
    |> shouldEqual (normalizeXml expectedPropertyDefinitionNodes)

    propertyNodes |> Seq.length |> shouldEqual 1

    (propertyNodes |> Seq.head).OuterXml
    |> normalizeXml
    |> shouldEqual (normalizeXml expectedPropertyNodes)