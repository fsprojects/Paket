module Paket.InstallModel.Xml.RefactoringEssentialsSpec

open FsUnit
open NUnit.Framework
open Paket
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
    let model =
        InstallModel.CreateFromLibs(PackageName "RefactoringEssentials", SemVer.Parse "1.2.0", [],
              [],
              [],
              [
                [".."; "RefactoringEssentials"; "analyzers"; "dotnet"; "RefactoringEssentials.dll"] |> toPath
              ],
              Nuspec.All)
    
    let project = ProjectFile.TryLoad("./ProjectFile/TestData/EmptyCsharpGuid.csprojtest")
    Assert.IsTrue(project.IsSome)
    let _,_,_,analyzerNodes = project.Value.GenerateXml(model,true,true,None)
    analyzerNodes
    |> (fun n -> n.OuterXml)
    |> normalizeXml
    |> shouldEqual (normalizeXml expected)

[<Test>]
let ``should generate Xml for RefactoringEssentials in VisualBasic project``() = 
    let model =
        InstallModel.CreateFromLibs(PackageName "RefactoringEssentials", SemVer.Parse "1.2.0", [],
              [],
              [],
              [
                [".."; "RefactoringEssentials"; "analyzers"; "dotnet"; "RefactoringEssentials.dll"] |> toPath
              ],
              Nuspec.All)
    
    let project = ProjectFile.TryLoad("./ProjectFile/TestData/EmptyVbGuid.vbprojtest")
    Assert.IsTrue(project.IsSome)
    let _,_,_,analyzerNodes = project.Value.GenerateXml(model,true,true,None)
    analyzerNodes
    |> (fun n -> n.OuterXml)
    |> normalizeXml
    |> shouldEqual (normalizeXml expected)