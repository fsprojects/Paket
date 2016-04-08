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
    let model =
        InstallModel.CreateFromLibs(PackageName "TempPkg", SemVer.Parse "0.1", [],
            [  ],
              [],
              [],
              Nuspec.Load("Nuspec/EmptyLibs.nuspec"))
    
    let _,targetsNodes,chooseNode,_,_ = ProjectFile.TryLoad("./ProjectFile/TestData/Empty.fsprojtest").Value.GenerateXml(model,Map.empty,true,true,None)
    chooseNode.OuterXml
    |> normalizeXml
    |> shouldEqual (normalizeXml expected)
