module Paket.InstallModel.Xml.EmptyLibsSpecs

open Paket
open NUnit.Framework
open FsUnit
open Paket.TestHelpers
open Paket.Domain
open Paket.Requirements

let expected = """
<ItemGroup xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <Reference Include="System.Xaml">
    <Paket>True</Paket>
  </Reference>
</ItemGroup>"""

[<Test>]
let ``should generate Xml for framework references and empty libs``() = 
    ensureDir()
    let model =
        InstallModel.CreateFromLibs(PackageName "TempPkg", SemVer.Parse "0.1", InstallModelKind.Package, FrameworkRestriction.NoRestriction,
            [  ],
              [],
              [],
              Nuspec.Load(__SOURCE_DIRECTORY__ + "/../../Nuspec/EmptyLibs.nuspec"))
    
    let ctx = ProjectFile.TryLoad("./ProjectFile/TestData/Empty.fsprojtest").Value.GenerateXml(model, System.Collections.Generic.HashSet<_>(),Map.empty,None,Some true,None,true,KnownTargetProfiles.AllProfiles,None)
    ctx.ChooseNodes.Head.OuterXml
    |> normalizeXml
    |> shouldEqual (normalizeXml expected)
