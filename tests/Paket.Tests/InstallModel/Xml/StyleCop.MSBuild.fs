module Paket.InstallModel.Xml.StyleCopSpecs

open Paket
open NUnit.Framework
open FsUnit
open Paket.TestHelpers
open Paket.Domain
open Paket.Requirements

let expected = """<?xml version="1.0" encoding="utf-16"?>
<PropertyGroup xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <__paket__StyleCop_MSBuild_Targets>StyleCop.MSBuild</__paket__StyleCop_MSBuild_Targets>
</PropertyGroup>"""

let expectedPropertyNdoes = """<?xml version="1.0" encoding="utf-16"?>
<Import Project="..\..\..\StyleCop.MSBuild\build\$(__paket__StyleCop_MSBuild_Targets).targets" Condition="Exists('..\..\..\StyleCop.MSBuild\build\$(__paket__StyleCop_MSBuild_Targets).targets')" xmlns="http://schemas.microsoft.com/developer/msbuild/2003" />"""

[<Test>]
let ``should generate Xml for StyleCop.MSBuild``() = 
    let model =
        InstallModel.CreateFromLibs(PackageName "StyleCop.MSBuild", SemVer.Parse "4.7.49.1", [],[],
            [ @"..\StyleCop.MSBuild\build\StyleCop.MSBuild.Targets" ],
              Nuspec.All)
    
    let propertyNodes,chooseNode,additionalNode = ProjectFile.Load("./ProjectFile/TestData/Empty.fsprojtest").Value.GenerateXml(model,CopyLocal.True)
    match additionalNode with
    | Some node -> 
        node.OuterXml
        |> normalizeXml
        |> shouldEqual (normalizeXml expected)
    | None -> failwith "error"

    
    propertyNodes |> Seq.length |> shouldEqual 1

    (propertyNodes |> Seq.head).OuterXml
    |> normalizeXml
    |> shouldEqual (normalizeXml expectedPropertyNdoes)