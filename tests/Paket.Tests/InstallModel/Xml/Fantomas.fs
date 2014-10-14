module Paket.InstallModel.Xml.FantomasSpecs

open Paket
open NUnit.Framework
open FsUnit
open System.Xml
open Paket.TestHelpers

let expected = """
<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
<Choose>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And $(TargetFrameworkVersion) == 'v1.0'">
    <ItemGroup>
      <Reference Include="FantomasLib">
        <HintPath>$(SolutionDir)/packages/Fantomas/lib/FantomasLib.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And $(TargetFrameworkVersion) == 'v1.1'">
    <ItemGroup>
      <Reference Include="FantomasLib">
        <HintPath>$(SolutionDir)/packages/Fantomas/lib/FantomasLib.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And $(TargetFrameworkVersion) == 'v2.0'">
    <ItemGroup>
      <Reference Include="FantomasLib">
        <HintPath>$(SolutionDir)/packages/Fantomas/lib/FantomasLib.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And $(TargetFrameworkVersion) == 'v3.5'">
    <ItemGroup>
      <Reference Include="FantomasLib">
        <HintPath>$(SolutionDir)/packages/Fantomas/lib/FantomasLib.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And $(TargetFrameworkVersion) == 'v4.0' And $(TargetFrameworkProfile) == 'Client'">
    <ItemGroup>
      <Reference Include="FantomasLib">
        <HintPath>$(SolutionDir)/packages/Fantomas/lib/FantomasLib.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And $(TargetFrameworkVersion) == 'v4.0'">
    <ItemGroup>
      <Reference Include="FantomasLib">
        <HintPath>$(SolutionDir)/packages/Fantomas/lib/FantomasLib.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And $(TargetFrameworkVersion) == 'v4.5'">
    <ItemGroup>
      <Reference Include="FantomasLib">
        <HintPath>$(SolutionDir)/packages/Fantomas/lib/FantomasLib.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And $(TargetFrameworkVersion) == 'v4.5.1'">
    <ItemGroup>
      <Reference Include="FantomasLib">
        <HintPath>$(SolutionDir)/packages/Fantomas/lib/FantomasLib.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
</Choose>
</Project>"""

[<Test>]
let ``should generate Xml for Fantomas 1.5``() = 
    let model =
        InstallModel.CreateFromLibs("Fantomas", SemVer.Parse "1.5.0",        
            [ @"../packages/Fantomas/lib/FantomasLib.dll" 
              @"../packages/Fantomas/lib/FSharp.Core.dll" 
              @"../packages/Fantomas/lib/Fantomas.exe" ],
              { References = NuspecReferences.Explicit ["FantomasLib.dll"]; FrameworkAssemblyReferences = []})
    
    let chooseNode = ProjectFile.GenerateTarget(model)
    chooseNode.OuterXml
    |> normalizeXml
    |> shouldEqual (normalizeXml expected)
