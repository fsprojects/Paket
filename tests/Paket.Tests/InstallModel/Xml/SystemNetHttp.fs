module Paket.InstallModel.Xml.SystemNetHttpSpecs

open Paket
open NUnit.Framework
open FsUnit
open Paket.TestHelpers
open Paket.Domain
open Paket.Requirements

let expected = """
<Choose xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <When Condition="$(TargetFrameworkIdentifier) == '.NETCore'">
    <ItemGroup>
      <Reference Include="System.Net.Http.Extensions">
        <HintPath>..\..\..\Microsoft.Net.Http\lib\win8\System.Net.Http.Extensions.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Net.Http.Primitives">
        <HintPath>..\..\..\Microsoft.Net.Http\lib\win8\System.Net.Http.Primitives.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v4.0')">
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
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v4.5' Or $(TargetFrameworkVersion) == 'v4.5.1' Or $(TargetFrameworkVersion) == 'v4.5.2' Or $(TargetFrameworkVersion) == 'v4.5.3')">
    <ItemGroup>
      <Reference Include="System.Net.Http.Extensions">
        <HintPath>..\..\..\Microsoft.Net.Http\lib\net45\System.Net.Http.Extensions.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Net.Http.Primitives">
        <HintPath>..\..\..\Microsoft.Net.Http\lib\net45\System.Net.Http.Primitives.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == 'MonoAndroid'">
    <ItemGroup>
      <Reference Include="System.Net.Http.Extensions">
        <HintPath>..\..\..\Microsoft.Net.Http\lib\monoandroid\System.Net.Http.Extensions.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Net.Http.Primitives">
        <HintPath>..\..\..\Microsoft.Net.Http\lib\monoandroid\System.Net.Http.Primitives.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == 'MonoTouch'">
    <ItemGroup>
      <Reference Include="System.Net.Http.Extensions">
        <HintPath>..\..\..\Microsoft.Net.Http\lib\monotouch\System.Net.Http.Extensions.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Net.Http.Primitives">
        <HintPath>..\..\..\Microsoft.Net.Http\lib\monotouch\System.Net.Http.Primitives.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == 'WindowsPhoneApp'">
    <ItemGroup>
      <Reference Include="System.Net.Http.Extensions">
        <HintPath>..\..\..\Microsoft.Net.Http\lib\wpa81\System.Net.Http.Extensions.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Net.Http.Primitives">
        <HintPath>..\..\..\Microsoft.Net.Http\lib\wpa81\System.Net.Http.Primitives.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="($(TargetFrameworkIdentifier) == 'Silverlight' And ($(TargetFrameworkVersion) == 'v4.0' Or $(TargetFrameworkVersion) == 'v5.0')) Or ($(TargetFrameworkIdentifier) == 'WindowsPhone' And ($(TargetFrameworkVersion) == 'v7.1' Or $(TargetFrameworkVersion) == 'v8.0' Or $(TargetFrameworkVersion) == 'v8.1')) Or ($(TargetFrameworkProfile) == 'Profile3') Or ($(TargetFrameworkProfile) == 'Profile5') Or ($(TargetFrameworkProfile) == 'Profile6') Or ($(TargetFrameworkProfile) == 'Profile14') Or ($(TargetFrameworkProfile) == 'Profile18') Or ($(TargetFrameworkProfile) == 'Profile19') Or ($(TargetFrameworkProfile) == 'Profile23') Or ($(TargetFrameworkProfile) == 'Profile24') Or ($(TargetFrameworkProfile) == 'Profile31') Or ($(TargetFrameworkProfile) == 'Profile32') Or ($(TargetFrameworkProfile) == 'Profile36') Or ($(TargetFrameworkProfile) == 'Profile37') Or ($(TargetFrameworkProfile) == 'Profile41') Or ($(TargetFrameworkProfile) == 'Profile42') Or ($(TargetFrameworkProfile) == 'Profile46') Or ($(TargetFrameworkProfile) == 'Profile47') Or ($(TargetFrameworkProfile) == 'Profile49') Or ($(TargetFrameworkProfile) == 'Profile78') Or ($(TargetFrameworkProfile) == 'Profile84') Or ($(TargetFrameworkProfile) == 'Profile88') Or ($(TargetFrameworkProfile) == 'Profile92') Or ($(TargetFrameworkProfile) == 'Profile96') Or ($(TargetFrameworkProfile) == 'Profile102') Or ($(TargetFrameworkProfile) == 'Profile104') Or ($(TargetFrameworkProfile) == 'Profile111') Or ($(TargetFrameworkProfile) == 'Profile136') Or ($(TargetFrameworkProfile) == 'Profile143') Or ($(TargetFrameworkProfile) == 'Profile147') Or ($(TargetFrameworkProfile) == 'Profile151') Or ($(TargetFrameworkProfile) == 'Profile154') Or ($(TargetFrameworkProfile) == 'Profile157') Or ($(TargetFrameworkProfile) == 'Profile158') Or ($(TargetFrameworkProfile) == 'Profile225') Or ($(TargetFrameworkProfile) == 'Profile240') Or ($(TargetFrameworkProfile) == 'Profile255') Or ($(TargetFrameworkProfile) == 'Profile259') Or ($(TargetFrameworkProfile) == 'Profile328') Or ($(TargetFrameworkProfile) == 'Profile336') Or ($(TargetFrameworkProfile) == 'Profile344')">
    <ItemGroup>
      <Reference Include="System.Net.Http.Extensions">
        <HintPath>..\..\..\Microsoft.Net.Http\lib\portable-net40+sl4+win8+wp71+wpa81\System.Net.Http.Extensions.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Net.Http.Primitives">
        <HintPath>..\..\..\Microsoft.Net.Http\lib\portable-net40+sl4+win8+wp71+wpa81\System.Net.Http.Primitives.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Net.Http">
        <HintPath>..\..\..\Microsoft.Net.Http\lib\portable-net40+sl4+win8+wp71+wpa81\System.Net.Http.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="($(TargetFrameworkProfile) == 'Profile7') Or ($(TargetFrameworkProfile) == 'Profile44')">
    <ItemGroup>
      <Reference Include="System.Net.Http.Extensions">
        <HintPath>..\..\..\Microsoft.Net.Http\lib\portable-net45+win8\System.Net.Http.Extensions.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Net.Http.Primitives">
        <HintPath>..\..\..\Microsoft.Net.Http\lib\portable-net45+win8\System.Net.Http.Primitives.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
</Choose>"""

[<Test>]
let ``should generate Xml for System.Net.Http 2.2.8``() = 
    let model =     
        InstallModel.CreateFromLibs(PackageName "System.Net.Http", SemVer.Parse "2.2.8", [],
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
              Nuspec.All)

    let _,chooseNode,_ = ProjectFile.Load("./ProjectFile/TestData/Empty.fsprojtest").Value.GenerateXml(model,true,true)
    chooseNode.OuterXml
    |> normalizeXml
    |> shouldEqual (normalizeXml expected)
