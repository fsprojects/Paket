module Paket.InstallModel.Xml.GitInfoPlanterSpecs

open Paket
open NUnit.Framework
open FsUnit
open Paket.TestHelpers
open Paket.Domain
open Paket.Requirements

let emptyReferences = """<?xml version="1.0" encoding="utf-16"?>
<Choose xmlns="http://schemas.microsoft.com/developer/msbuild/2003" />"""

let emptyPropertyDefinitionNodes = """<?xml version="1.0" encoding="utf-16"?>
<Choose xmlns="http://schemas.microsoft.com/developer/msbuild/2003" />"""

let expectedPropertyNodes = """<?xml version="1.0" encoding="utf-16"?>
<Import Project="..\..\..\GitInfoPlanter\build\GitInfoPlanter.targets" Condition="Exists('..\..\..\GitInfoPlanter\build\GitInfoPlanter.targets')" Label="Paket" xmlns="http://schemas.microsoft.com/developer/msbuild/2003" />"""

[<Test>]
#if TESTSUITE_RUNS_ON_DOTNETCORE
[<Flaky>]
#endif
let ``should generate Xml for GitInfoPlanter2.0.0``() = 
    ensureDir()
    let model =
        InstallModel.CreateFromLibs(PackageName "GitInfoPlanter", SemVer.Parse "0.21", InstallModelKind.Package, FrameworkRestriction.NoRestriction,
            [ ],
            [ @"..\GitInfoPlanter\build\GitInfoPlanter.targets" ]
            |> Paket.InstallModel.ProcessingSpecs.fromLegacyList @"..\GitInfoPlanter\",
            [],
              Nuspec.All)

    let ctx = ProjectFile.TryLoad("./ProjectFile/TestData/Empty.fsprojtest").Value.GenerateXml(model, System.Collections.Generic.HashSet<_>(),Map.empty,None,Some true,None,true,KnownTargetProfiles.AllProfiles,None)
    ctx.ChooseNodes.Head.OuterXml
    |> normalizeXml
    |> shouldEqual (normalizeXml emptyReferences)

    ctx.FrameworkSpecificPropertyChooseNode.OuterXml
    |> normalizeXml
    |> shouldEqual (normalizeXml emptyPropertyDefinitionNodes)

    ctx.FrameworkSpecificPropsNodes |> Seq.length |> shouldEqual 0
    ctx.GlobalPropsNodes |> Seq.length |> shouldEqual 0
    ctx.FrameworkSpecificTargetsNodes |> Seq.length |> shouldEqual 0
    ctx.GlobalTargetsNodes |> Seq.length |> shouldEqual 1

    (ctx.GlobalTargetsNodes |> Seq.head).OuterXml
    |> normalizeXml
    |> shouldEqual (normalizeXml expectedPropertyNodes)