module Paket.InstallModel.Xml.CodeCrackerSpecs

open FsUnit
open NUnit.Framework
open Paket
open Paket.Requirements
open Paket.Domain
open Paket.TestHelpers

let expectedEmpty = """
<ItemGroup xmlns="http://schemas.microsoft.com/developer/msbuild/2003" />"""

let expectedCsharp = """
<ItemGroup xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <Analyzer Include="..\..\..\codecracker.CSharp\analyzers\dotnet\cs\CodeCracker.CSharp.dll">
    <Paket>True</Paket>
  </Analyzer>
  <Analyzer Include="..\..\..\codecracker.CSharp\analyzers\dotnet\cs\CodeCracker.Common.dll">
    <Paket>True</Paket>
  </Analyzer>
</ItemGroup>"""

[<Test>]
let ``should generate Xml for codecracker.CSharp``() = 
    ensureDir()
    let model =
        InstallModel.CreateFromLibs(PackageName "codecracker.CSharp", SemVer.Parse "1.0.0-rc2", InstallModelKind.Package, FrameworkRestriction.NoRestriction,
              [],
              [],
              [
                [".."; "codecracker.CSharp"; "analyzers"; "dotnet"; "cs"; "CodeCracker.CSharp.dll"] |> toPath
                [".."; "codecracker.CSharp"; "analyzers"; "dotnet"; "cs"; "CodeCracker.Common.dll"] |> toPath
              ]
              |> Paket.InstallModel.ProcessingSpecs.fromLegacyList ([".."; "codecracker.CSharp"; ""] |> toPath),
              Nuspec.All)

    let project = ProjectFile.TryLoad("./ProjectFile/TestData/EmptyCsharpGuid.csprojtest")
    Assert.IsTrue(project.IsSome)
    let ctx = project.Value.GenerateXml(model, System.Collections.Generic.HashSet<_>() ,Map.empty,None,Some true,None,true,KnownTargetProfiles.AllProfiles,None)
    ctx.AnalyzersNode
    |> (fun n -> n.OuterXml)
    |> normalizeXml
    |> shouldEqual (normalizeXml expectedCsharp)

[<Test>]
let ``should generate Xml for codecracker.CSharp in VisualBasic project``() = 
    ensureDir()
    let model =
        InstallModel.CreateFromLibs(PackageName "codecracker.CSharp", SemVer.Parse "1.0.0-rc2", InstallModelKind.Package, FrameworkRestriction.NoRestriction,
              [],
              [],
              [
                [".."; "codecracker.CSharp"; "analyzers"; "dotnet"; "cs"; "CodeCracker.CSharp.dll"] |> toPath
                [".."; "codecracker.CSharp"; "analyzers"; "dotnet"; "cs"; "CodeCracker.Common.dll"] |> toPath
              ] |> Paket.InstallModel.ProcessingSpecs.fromLegacyList ([".."; "codecracker.CSharp"; ""] |> toPath),
              Nuspec.All)
    
    let project = ProjectFile.TryLoad("./ProjectFile/TestData/EmptyVbGuid.vbprojtest")
    Assert.IsTrue(project.IsSome)
    let ctx = project.Value.GenerateXml(model, System.Collections.Generic.HashSet<_>(),Map.empty,None,Some true,None,true,KnownTargetProfiles.AllProfiles,None)
    ctx.AnalyzersNode
    |> (fun n -> n.OuterXml)
    |> normalizeXml
    |> shouldEqual (normalizeXml expectedEmpty)

let expectedVb = """
<ItemGroup xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <Analyzer Include="..\..\..\codecracker.CSharp\analyzers\dotnet\vb\CodeCracker.Common.dll">
    <Paket>True</Paket>
  </Analyzer>
  <Analyzer Include="..\..\..\codecracker.CSharp\analyzers\dotnet\vb\CodeCracker.VisualBasic.dll">
    <Paket>True</Paket>
  </Analyzer>
</ItemGroup>"""

[<Test>]
let ``should generate Xml for codecracker.VisualBasic``() = 
    ensureDir()
    let model =
        InstallModel.CreateFromLibs(PackageName "codecracker.VisualBasic", SemVer.Parse "1.0.0-rc2", InstallModelKind.Package, FrameworkRestriction.NoRestriction,
              [],
              [],
              [
                [".."; "codecracker.CSharp"; "analyzers"; "dotnet"; "vb"; "CodeCracker.VisualBasic.dll"] |> toPath
                [".."; "codecracker.CSharp"; "analyzers"; "dotnet"; "vb"; "CodeCracker.Common.dll"] |> toPath
              ] |> Paket.InstallModel.ProcessingSpecs.fromLegacyList ([".."; "codecracker.CSharp"; ""] |> toPath),
              Nuspec.All)

    let project = ProjectFile.TryLoad("./ProjectFile/TestData/EmptyVbGuid.vbprojtest")
    Assert.IsTrue(project.IsSome)
    let ctx = project.Value.GenerateXml(model, System.Collections.Generic.HashSet<_>(),Map.empty,None,Some true,None,true,KnownTargetProfiles.AllProfiles,None)
    ctx.AnalyzersNode
    |> (fun n -> n.OuterXml)
    |> normalizeXml
    |> shouldEqual (normalizeXml expectedVb)
