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
#if TESTSUITE_RUNS_ON_DOTNETCORE
[<Flaky>]
#endif
let ``should generate Xml for StyleCop.MSBuild``() = 
    ensureDir()
    let model =
        InstallModel.CreateFromLibs(PackageName "StyleCop.MSBuild", SemVer.Parse "4.7.49.1", InstallModelKind.Package, FrameworkRestriction.NoRestriction,[],
            [ @"..\StyleCop.MSBuild\build\StyleCop.MSBuild.Targets" ] |> Paket.InstallModel.ProcessingSpecs.fromLegacyList @"..\StyleCop.MSBuild\",
            [],
              Nuspec.All)

    model.GetTargetsFiles(TargetProfile.SinglePlatform (DotNetFramework FrameworkVersion.V2))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\StyleCop.MSBuild\build\StyleCop.MSBuild.Targets" 
    
    let ctx = ProjectFile.TryLoad("./ProjectFile/TestData/Empty.fsprojtest").Value.GenerateXml(model, System.Collections.Generic.HashSet<_>(),Map.empty,None,Some true,None,true,KnownTargetProfiles.AllProfiles,None)
    
    ctx.FrameworkSpecificPropertyChooseNode.OuterXml
    |> normalizeXml
    |> shouldEqual (normalizeXml emptyPropertyNameNodes)
        

    ctx.FrameworkSpecificPropsNodes |> Seq.length |> shouldEqual 0
    ctx.FrameworkSpecificTargetsNodes |> Seq.length |> shouldEqual 0
    ctx.GlobalPropsNodes |> Seq.length |> shouldEqual 0
    ctx.GlobalTargetsNodes |> Seq.length |> shouldEqual 1

    (ctx.GlobalTargetsNodes |> Seq.head).OuterXml
    |> normalizeXml
    |> shouldEqual (normalizeXml expectedPropertyNodes)