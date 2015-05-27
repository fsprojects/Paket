module Paket.InstallModel.Xml.StyleCopSpecs

open Paket
open NUnit.Framework
open FsUnit
open Paket.TestHelpers
open Paket.Domain
open Paket.Requirements

let emptyPropertyNameNodes = """<?xml version="1.0" encoding="utf-16"?>
<Choose xmlns="http://schemas.microsoft.com/developer/msbuild/2003" />"""

let expectedPropertyNodes = """<?xml version="1.0" encoding="utf-16"?>
<Import Project="..\..\..\StyleCop.MSBuild\build\StyleCop.MSBuild.Targets" Condition="Exists('..\..\..\StyleCop.MSBuild\build\StyleCop.MSBuild.Targets')" Label="Paket" xmlns="http://schemas.microsoft.com/developer/msbuild/2003" />"""

[<Test>]
let ``should generate Xml for StyleCop.MSBuild``() = 
    let model =
        InstallModel.CreateFromLibs(PackageName "StyleCop.MSBuild", SemVer.Parse "4.7.49.1", [],[],
            [ @"..\StyleCop.MSBuild\build\StyleCop.MSBuild.Targets" ],
              Nuspec.All)

    model.GetTargetsFiles(SinglePlatform (DotNetFramework FrameworkVersion.V2)) |> shouldContain @"..\StyleCop.MSBuild\build\StyleCop.MSBuild.Targets" 
    
    let propertyNodes,chooseNode,propertyChooseNode = ProjectFile.Load("./ProjectFile/TestData/Empty.fsprojtest").Value.GenerateXml(model,true,true)
    
    propertyChooseNode.OuterXml
    |> normalizeXml
    |> shouldEqual (normalizeXml emptyPropertyNameNodes)
        
    propertyNodes |> Seq.length |> shouldEqual 1

    (propertyNodes |> Seq.head).OuterXml
    |> normalizeXml
    |> shouldEqual (normalizeXml expectedPropertyNodes)