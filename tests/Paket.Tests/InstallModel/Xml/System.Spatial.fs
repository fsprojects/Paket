module Paket.InstallModel.Xml.SystemSpatialSpecs

open Paket
open NUnit.Framework
open FsUnit
open Paket.TestHelpers
open Paket.Domain
open Paket.Requirements

let expected = """
<Choose xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <When Condition="$(TargetFrameworkIdentifier) == 'Silverlight' And ($(TargetFrameworkVersion) == 'v4.0' Or $(TargetFrameworkVersion) == 'v5.0')">
    <ItemGroup>
      <Reference Include="System.Spatial">
        <HintPath>..\..\..\System.Spatial\lib\sl4\System.Spatial.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="($(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v4.0' Or $(TargetFrameworkVersion) == 'v4.5' Or $(TargetFrameworkVersion) == 'v4.5.1' Or $(TargetFrameworkVersion) == 'v4.5.2' Or $(TargetFrameworkVersion) == 'v4.5.3')) Or ($(TargetFrameworkIdentifier) == 'MonoAndroid') Or ($(TargetFrameworkIdentifier) == 'MonoTouch')">
    <ItemGroup>
      <Reference Include="System.Spatial">
        <HintPath>..\..\..\System.Spatial\lib\net40\System.Spatial.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
</Choose>"""

[<Test>]
let ``should generate Xml for System.Spatial``() = 
    let model =
        InstallModel.CreateFromLibs(PackageName "System.Spatial", SemVer.Parse "5.6.3", [],
            [ @"..\System.Spatial\lib\net40\System.Spatial.dll"
              @"..\System.Spatial\lib\net40\de\System.Spatial.resources.dll"
              @"..\System.Spatial\lib\net40\es\System.Spatial.resources.dll"
              @"..\System.Spatial\lib\net40\zh-Hans\System.Spatial.resources.dll" 

              @"..\System.Spatial\lib\sl4\System.Spatial.dll" 
              @"..\System.Spatial\lib\sl4\de\System.Spatial.resources.dll"
              @"..\System.Spatial\lib\sl4\es\System.Spatial.resources.dll"
              @"..\System.Spatial\lib\sl4\zh-Hans\System.Spatial.resources.dll" 
            ],[],Nuspec.All)
    
    let _,chooseNode,_ = ProjectFile.Load("./ProjectFile/TestData/Empty.fsprojtest").Value.GenerateXml(model,true,true)
    chooseNode.OuterXml
    |> normalizeXml
    |> shouldEqual (normalizeXml expected)