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
    let model =
        InstallModel.CreateFromLibs(PackageName "Fuchu", SemVer.Parse "0.4.0", [],
            [ @"..\Fuchu\lib\Fuchu.dll" 
              @"..\Fuchu\lib\Fuchu.XML" 
              @"..\Fuchu\lib\Fuchu.pdb" ],
              [],
              Nuspec.All)
    
    let _,chooseNode,_ = ProjectFile.Load("./ProjectFile/TestData/Empty.fsprojtest").Value.GenerateXml(model,true,true)
    chooseNode.OuterXml
    |> normalizeXml
    |> shouldEqual (normalizeXml expected)
