module Paket.InstallModel.Xml.MicrosoftCodeAnalysisAnalyzersSpecs

open FsUnit
open NUnit.Framework
open Paket
open Paket.Constants
open Paket.Domain
open Paket.TestHelpers
open System.Text
open System.IO

let model =
    InstallModel.CreateFromLibs(PackageName "Microsoft.CodeAnalysis.Analyzers", SemVer.Parse "1.0.0", [],
            [],
            [],
            [
                [".."; "Microsoft.CodeAnalysis.Analyzers"; "analyzers"; "dotnet"; "cs"; "Microsoft.CodeAnalysis.Analyzers.dll"] |> toPath
                [".."; "Microsoft.CodeAnalysis.Analyzers"; "analyzers"; "dotnet"; "cs"; "Microsoft.CodeAnalysis.CSharp.Analyzers.dll"] |> toPath
                [".."; "Microsoft.CodeAnalysis.Analyzers"; "analyzers"; "dotnet"; "vb"; "Microsoft.CodeAnalysis.Analyzers.dll"] |> toPath
                [".."; "Microsoft.CodeAnalysis.Analyzers"; "analyzers"; "dotnet"; "vb"; "Microsoft.CodeAnalysis.VisualBasic.Analyzers.dll"] |> toPath
            ],
            Nuspec.All)

let expectedCs = """
<ItemGroup xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <Analyzer Include="..\..\..\Microsoft.CodeAnalysis.Analyzers\analyzers\dotnet\cs\Microsoft.CodeAnalysis.Analyzers.dll">
    <Paket>True</Paket>
  </Analyzer>
  <Analyzer Include="..\..\..\Microsoft.CodeAnalysis.Analyzers\analyzers\dotnet\cs\Microsoft.CodeAnalysis.CSharp.Analyzers.dll">
    <Paket>True</Paket>
  </Analyzer>
</ItemGroup>"""

[<Test>]
let ``should generate Xml for Microsoft.CodeAnalysis.Analyzers in CSharp project``() = 
    ensureDir()
    let project = ProjectFile.TryLoad("./ProjectFile/TestData/EmptyCsharpGuid.csprojtest")
    Assert.IsTrue(project.IsSome)
    let _,_,_,_,analyzerNodes = project.Value.GenerateXml(model, System.Collections.Generic.HashSet<_>(),Map.empty,Some true,true,None)
    analyzerNodes
    |> (fun n -> n.OuterXml)
    |> normalizeXml
    |> shouldEqual (normalizeXml expectedCs)

let expectedVb = """
<ItemGroup xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <Analyzer Include="..\..\..\Microsoft.CodeAnalysis.Analyzers\analyzers\dotnet\vb\Microsoft.CodeAnalysis.Analyzers.dll">
    <Paket>True</Paket>
  </Analyzer>
  <Analyzer Include="..\..\..\Microsoft.CodeAnalysis.Analyzers\analyzers\dotnet\vb\Microsoft.CodeAnalysis.VisualBasic.Analyzers.dll">
    <Paket>True</Paket>
  </Analyzer>
</ItemGroup>"""

[<Test>]
let ``should generate Xml for RefactoringEssentials in VisualBasic project``() = 
    ensureDir()
    let project = ProjectFile.TryLoad("./ProjectFile/TestData/EmptyVbGuid.vbprojtest")
    Assert.IsTrue(project.IsSome)
    let _,_,_,_,analyzerNodes = project.Value.GenerateXml(model, System.Collections.Generic.HashSet<_>(),Map.empty,Some true,true,None)
    analyzerNodes
    |> (fun n -> n.OuterXml)
    |> normalizeXml
    |> shouldEqual (normalizeXml expectedVb)

let oldModel =
    InstallModel.CreateFromLibs(PackageName "Microsoft.CodeAnalysis.Analyzers", SemVer.Parse "1.0.0-rc2", [],
            [],
            [],
            [], // Analyzers weren't in the correct folder and won't be found for this version
            Nuspec.All)

let expectedEmpty = """<ItemGroup xmlns="http://schemas.microsoft.com/developer/msbuild/2003" />"""

[<Test>]
let ``should generate Xml for Microsoft.CodeAnalysis.Analyzers 1.0.0-rc2``() = 
    ensureDir()
    let project = ProjectFile.TryLoad("./ProjectFile/TestData/EmptyCsharpGuid.csprojtest")
    Assert.IsTrue(project.IsSome)
    let _,_,_,_,analyzerNodes = project.Value.GenerateXml(oldModel, System.Collections.Generic.HashSet<_>(),Map.empty,Some true,true,None)
    analyzerNodes
    |> (fun n -> n.OuterXml)
    |> normalizeXml
    |> shouldEqual (normalizeXml expectedEmpty)

let projectAfter100Installed = """
<Project ToolsVersion="4.0" DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <Import Project="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props" Condition="Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')" />
  <ItemGroup xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
    <Analyzer Include="..\..\..\Microsoft.CodeAnalysis.Analyzers\analyzers\dotnet\cs\Microsoft.CodeAnalysis.Analyzers.dll">
      <Paket>True</Paket>
    </Analyzer>
    <Analyzer Include="..\..\..\Microsoft.CodeAnalysis.Analyzers\analyzers\dotnet\cs\Microsoft.CodeAnalysis.CSharp.Analyzers.dll">
      <Paket>True</Paket>
    </Analyzer>
  </ItemGroup>
</Project>"""

[<Test>]
let ``can remove analyzer paket nodes``() = 
    ensureDir()
    use stream = new MemoryStream(projectAfter100Installed |> Encoding.ASCII.GetBytes)
    let project = ProjectFile.LoadFromStream("Test.csproj", stream)
    
    project.RemovePaketNodes()

    let analyzerCount = project.FindPaketNodes "Analyzer" |> List.length

    analyzerCount |> shouldEqual 0

    