module Paket.InstallModel.Xml.CodeCrackerSpecs

open FsUnit
open NUnit.Framework
open Paket
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
    let model =
        InstallModel.CreateFromLibs(PackageName "codecracker.CSharp", SemVer.Parse "1.0.0-rc2", [],
              [],
              [],
              [
                [".."; "codecracker.CSharp"; "analyzers"; "dotnet"; "cs"; "CodeCracker.CSharp.dll"] |> toPath
                [".."; "codecracker.CSharp"; "analyzers"; "dotnet"; "cs"; "CodeCracker.Common.dll"] |> toPath
              ],
              Nuspec.All)
    
    let project = ProjectFile.TryLoad("./ProjectFile/TestData/EmptyCsharpGuid.csprojtest")
    Assert.IsTrue(project.IsSome)
    let _,_,_,_,analyzerNodes = project.Value.GenerateXml(model,Map.empty,true,true,None)
    analyzerNodes
    |> (fun n -> n.OuterXml)
    |> normalizeXml
    |> shouldEqual (normalizeXml expectedCsharp)

[<Test>]
let ``should generate Xml for codecracker.CSharp in VisualBasic project``() = 
    let model =
        InstallModel.CreateFromLibs(PackageName "codecracker.CSharp", SemVer.Parse "1.0.0-rc2", [],
              [],
              [],
              [
                [".."; "codecracker.CSharp"; "analyzers"; "dotnet"; "cs"; "CodeCracker.CSharp.dll"] |> toPath
                [".."; "codecracker.CSharp"; "analyzers"; "dotnet"; "cs"; "CodeCracker.Common.dll"] |> toPath
              ],
              Nuspec.All)
    
    let project = ProjectFile.TryLoad("./ProjectFile/TestData/EmptyVbGuid.vbprojtest")
    Assert.IsTrue(project.IsSome)
    let _,_,_,_,analyzerNodes = project.Value.GenerateXml(model,Map.empty,true,true,None)
    analyzerNodes
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
    let model =
        InstallModel.CreateFromLibs(PackageName "codecracker.VisualBasic", SemVer.Parse "1.0.0-rc2", [],
              [],
              [],
              [
                [".."; "codecracker.CSharp"; "analyzers"; "dotnet"; "vb"; "CodeCracker.VisualBasic.dll"] |> toPath
                [".."; "codecracker.CSharp"; "analyzers"; "dotnet"; "vb"; "CodeCracker.Common.dll"] |> toPath
              ],
              Nuspec.All)
    
    let project = ProjectFile.TryLoad("./ProjectFile/TestData/EmptyVbGuid.vbprojtest")
    Assert.IsTrue(project.IsSome)
    let _,_,_,_,analyzerNodes = project.Value.GenerateXml(model,Map.empty,true,true,None)
    analyzerNodes
    |> (fun n -> n.OuterXml)
    |> normalizeXml
    |> shouldEqual (normalizeXml expectedVb)
