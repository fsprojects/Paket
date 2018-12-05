module Paket.InstallModel.Xml.FuchuSpecs

open Paket
open NUnit.Framework
open FsUnit
open Paket.TestHelpers
open Paket.Domain
open Paket.Requirements

let expected = """
<ItemGroup xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <Reference Include="Fuchu">
    <HintPath>..\..\..\Fuchu\lib\Fuchu.dll</HintPath>
    <Private>True</Private>
    <Paket>True</Paket>
  </Reference>
</ItemGroup>"""

[<Test>]
let ``should generate Xml for Fuchu 0.4``() = 
    ensureDir()
    let model =
        InstallModel.CreateFromLibs(PackageName "Fuchu", SemVer.Parse "0.4.0", InstallModelKind.Package, FrameworkRestriction.NoRestriction,
            [ @"..\Fuchu\lib\Fuchu.dll"
              @"..\Fuchu\lib\Fuchu.XML"
              @"..\Fuchu\lib\Fuchu.pdb" ] |> Paket.InstallModel.ProcessingSpecs.fromLegacyList @"..\Fuchu\",
              [],
              [],
              Nuspec.All)
    
    let ctx = ProjectFile.TryLoad("./ProjectFile/TestData/Empty.fsprojtest").Value.GenerateXml(model, System.Collections.Generic.HashSet<_>(),Map.empty,None,Some true,None,true,KnownTargetProfiles.AllProfiles,None)
    ctx.ChooseNodes.Head.OuterXml
    |> normalizeXml
    |> shouldEqual (normalizeXml expected)
