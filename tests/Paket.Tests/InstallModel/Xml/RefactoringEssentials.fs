module Paket.InstallModel.Xml.RefactoringEssentialsSpec

open FsUnit
open NUnit.Framework
open Paket
open Paket.Requirements
open Paket.Domain
open Paket.TestHelpers

let expected = """
<ItemGroup xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <Analyzer Include="..\..\..\RefactoringEssentials\analyzers\dotnet\RefactoringEssentials.dll">
    <Paket>True</Paket>
  </Analyzer>
</ItemGroup>"""

[<Test>]
let ``should generate Xml for RefactoringEssentials in CSharp project``() = 
    ensureDir()
    let model =
        InstallModel.CreateFromLibs(PackageName "RefactoringEssentials", SemVer.Parse "1.2.0", InstallModelKind.Package, FrameworkRestriction.NoRestriction,
              [],
              [],
              [
                [".."; "RefactoringEssentials"; "analyzers"; "dotnet"; "RefactoringEssentials.dll"] |> toPath
              ] |> Paket.InstallModel.ProcessingSpecs.fromLegacyList ([".."; "RefactoringEssentials"; ""] |> toPath),
              Nuspec.All)

    let project = ProjectFile.TryLoad("./ProjectFile/TestData/EmptyCsharpGuid.csprojtest")
    Assert.IsTrue(project.IsSome)
    let ctx = project.Value.GenerateXml(model, System.Collections.Generic.HashSet<_>(),Map.empty,None,Some true,None,true,KnownTargetProfiles.AllProfiles,None)
    ctx.AnalyzersNode
    |> (fun n -> n.OuterXml)
    |> normalizeXml
    |> shouldEqual (normalizeXml expected)

[<Test>]
let ``should generate Xml for RefactoringEssentials in VisualBasic project``() = 
    ensureDir()
    let model =
        InstallModel.CreateFromLibs(PackageName "RefactoringEssentials", SemVer.Parse "1.2.0", InstallModelKind.Package, FrameworkRestriction.NoRestriction,
              [],
              [],
              [
                [".."; "RefactoringEssentials"; "analyzers"; "dotnet"; "RefactoringEssentials.dll"] |> toPath
              ] |> Paket.InstallModel.ProcessingSpecs.fromLegacyList ([".."; "RefactoringEssentials"; ""] |> toPath),
              Nuspec.All)

    let project = ProjectFile.TryLoad("./ProjectFile/TestData/EmptyVbGuid.vbprojtest")
    Assert.IsTrue(project.IsSome)
    let ctx = project.Value.GenerateXml(model, System.Collections.Generic.HashSet<_>(),Map.empty,None,Some true,None,true,KnownTargetProfiles.AllProfiles,None)
    ctx.AnalyzersNode
    |> (fun n -> n.OuterXml)
    |> normalizeXml
    |> shouldEqual (normalizeXml expected)
