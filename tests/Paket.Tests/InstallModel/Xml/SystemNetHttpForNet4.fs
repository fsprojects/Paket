module Paket.InstallModel.Xml.SystemNetHttpForNet2Specs

open Paket
open NUnit.Framework
open FsUnit
open Paket.TestHelpers
open Paket.Domain
open Paket.Requirements

let expected = """
<Choose xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <When Condition="($(TargetFrameworkIdentifier) == '.NETStandard' And $(TargetFrameworkVersion) == 'v1.0') Or ($(TargetFrameworkIdentifier) == '.NETFramework' And $(TargetFrameworkVersion) == 'v4.0')">
    <ItemGroup>
      <Reference Include="System.Net.Http.Extensions">
        <HintPath>..\..\..\Microsoft.Net.Http\lib\net40\System.Net.Http.Extensions.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Net.Http.Primitives">
        <HintPath>..\..\..\Microsoft.Net.Http\lib\net40\System.Net.Http.Primitives.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Net.Http.WebRequest">
        <HintPath>..\..\..\Microsoft.Net.Http\lib\net40\System.Net.Http.WebRequest.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Net.Http">
        <HintPath>..\..\..\Microsoft.Net.Http\lib\net40\System.Net.Http.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
</Choose>"""

[<Test>]
let ``should generate Xml for System.Net.Http 2.2.8``() = 
    let model =     
        InstallModel.CreateFromLibs(PackageName "System.Net.Http", SemVer.Parse "2.2.8", [FrameworkRestriction.Exactly(DotNetFramework(FrameworkVersion.V4))],
            [ @"..\Microsoft.Net.Http\lib\monoandroid\System.Net.Http.Extensions.dll" 
              @"..\Microsoft.Net.Http\lib\monoandroid\System.Net.Http.Primitives.dll" 
              
              @"..\Microsoft.Net.Http\lib\monotouch\System.Net.Http.Extensions.dll" 
              @"..\Microsoft.Net.Http\lib\monotouch\System.Net.Http.Primitives.dll" 

              @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.dll" 
              @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.Extensions.dll" 
              @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.Primitives.dll" 
              @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.WebRequest.dll" 
                     
              @"..\Microsoft.Net.Http\lib\net45\System.Net.Http.Extensions.dll" 
              @"..\Microsoft.Net.Http\lib\net45\System.Net.Http.Primitives.dll" 
              
              @"..\Microsoft.Net.Http\lib\portable-net40+sl4+win8+wp71+wpa81\System.Net.Http.dll" 
              @"..\Microsoft.Net.Http\lib\portable-net40+sl4+win8+wp71+wpa81\System.Net.Http.Extensions.dll" 
              @"..\Microsoft.Net.Http\lib\portable-net40+sl4+win8+wp71+wpa81\System.Net.Http.Primitives.dll"
                            
              @"..\Microsoft.Net.Http\lib\portable-net45+win8\System.Net.Http.Extensions.dll" 
              @"..\Microsoft.Net.Http\lib\portable-net45+win8\System.Net.Http.Primitives.dll"

              @"..\Microsoft.Net.Http\lib\win8\System.Net.Http.Extensions.dll" 
              @"..\Microsoft.Net.Http\lib\win8\System.Net.Http.Primitives.dll"
              
              @"..\Microsoft.Net.Http\lib\wpa81\System.Net.Http.Extensions.dll" 
              @"..\Microsoft.Net.Http\lib\wpa81\System.Net.Http.Primitives.dll" ],
              [],
              [],
              Nuspec.All)

    let _,targetsNodes,chooseNode,_,_ = ProjectFile.TryLoad("./ProjectFile/TestData/Empty.fsprojtest").Value.GenerateXml(model,Map.empty,true,true,None) 
    let currentXML = chooseNode.OuterXml |> normalizeXml
    currentXML
    |> shouldEqual (normalizeXml expected)
