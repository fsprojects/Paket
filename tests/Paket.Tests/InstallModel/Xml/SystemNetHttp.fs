module Paket.InstallModel.Xml.SystemNetHttpSpecs

open Paket
open NUnit.Framework
open FsUnit
open Paket.TestHelpers

let expected = """
<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
<Choose>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And $(TargetFrameworkVersion) == 'v1.0'">
    <ItemGroup />
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And $(TargetFrameworkVersion) == 'v1.1'">
    <ItemGroup />
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And $(TargetFrameworkVersion) == 'v2.0'">
    <ItemGroup />
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And $(TargetFrameworkVersion) == 'v3.5'">
    <ItemGroup />
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And $(TargetFrameworkVersion) == 'v4.0' And $(TargetFrameworkProfile) == 'Client'">
    <ItemGroup />
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And $(TargetFrameworkVersion) == 'v4.0'">
    <ItemGroup>
      <Reference Include="System.Net.Http.Extensions">
        <HintPath>$(SolutionDir)\packages\Microsoft.Net.Http\lib\net40\System.Net.Http.Extensions.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Net.Http.Primitives">
        <HintPath>$(SolutionDir)\packages\Microsoft.Net.Http\lib\net40\System.Net.Http.Primitives.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Net.Http.WebRequest">
        <HintPath>$(SolutionDir)\packages\Microsoft.Net.Http\lib\net40\System.Net.Http.WebRequest.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Net.Http">
        <HintPath>$(SolutionDir)\packages\Microsoft.Net.Http\lib\net40\System.Net.Http.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And $(TargetFrameworkVersion) == 'v4.5'">
    <ItemGroup>
      <Reference Include="System.Net.Http.Extensions">
        <HintPath>$(SolutionDir)\packages\Microsoft.Net.Http\lib\net45\System.Net.Http.Extensions.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Net.Http.Primitives">
        <HintPath>$(SolutionDir)\packages\Microsoft.Net.Http\lib\net45\System.Net.Http.Primitives.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And $(TargetFrameworkVersion) == 'v4.5.1'">
    <ItemGroup>
      <Reference Include="System.Net.Http.Extensions">
        <HintPath>$(SolutionDir)\packages\Microsoft.Net.Http\lib\net45\System.Net.Http.Extensions.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Net.Http.Primitives">
        <HintPath>$(SolutionDir)\packages\Microsoft.Net.Http\lib\net45\System.Net.Http.Primitives.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETPortable' And $(TargetFrameworkProfile) == 'Profile88' And $(TargetPlatformIdentifier) == 'Portable' And $(TargetPlatformVersion) == '7.0'">
    <ItemGroup>
      <Reference Include="System.Net.Http.Extensions">
        <HintPath>$(SolutionDir)\packages\Microsoft.Net.Http\lib\portable-net40+sl4+win8+wp71+wpa81\System.Net.Http.Extensions.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Net.Http.Primitives">
        <HintPath>$(SolutionDir)\packages\Microsoft.Net.Http\lib\portable-net40+sl4+win8+wp71+wpa81\System.Net.Http.Primitives.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Net.Http">
        <HintPath>$(SolutionDir)\packages\Microsoft.Net.Http\lib\portable-net40+sl4+win8+wp71+wpa81\System.Net.Http.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETPortable' And $(TargetFrameworkProfile) == 'net45+win8' And $(TargetPlatformIdentifier) == 'Portable' And $(TargetPlatformVersion) == '7.0'">
    <ItemGroup>
      <Reference Include="System.Net.Http.Extensions">
        <HintPath>$(SolutionDir)\packages\Microsoft.Net.Http\lib\portable-net45+win8\System.Net.Http.Extensions.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Net.Http.Primitives">
        <HintPath>$(SolutionDir)\packages\Microsoft.Net.Http\lib\portable-net45+win8\System.Net.Http.Primitives.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == 'MonoAndroid'">
    <ItemGroup>
      <Reference Include="System.Net.Http.Extensions">
        <HintPath>$(SolutionDir)\packages\Microsoft.Net.Http\lib\monoandroid\System.Net.Http.Extensions.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Net.Http.Primitives">
        <HintPath>$(SolutionDir)\packages\Microsoft.Net.Http\lib\monoandroid\System.Net.Http.Primitives.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == 'MonoTouch'">
    <ItemGroup>
      <Reference Include="System.Net.Http.Extensions">
        <HintPath>$(SolutionDir)\packages\Microsoft.Net.Http\lib\monotouch\System.Net.Http.Extensions.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Net.Http.Primitives">
        <HintPath>$(SolutionDir)\packages\Microsoft.Net.Http\lib\monotouch\System.Net.Http.Primitives.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == 'Windows' And $(TargetPlatformVersion) == 'v8.0'">
    <ItemGroup>
      <Reference Include="System.Net.Http.Extensions">
        <HintPath>$(SolutionDir)\packages\Microsoft.Net.Http\lib\win8\System.Net.Http.Extensions.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Net.Http.Primitives">
        <HintPath>$(SolutionDir)\packages\Microsoft.Net.Http\lib\win8\System.Net.Http.Primitives.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == 'WindowsPhoneApp' And $(TargetPlatformVersion) == 'v8.1'">
    <ItemGroup>
      <Reference Include="System.Net.Http.Extensions">
        <HintPath>$(SolutionDir)\packages\Microsoft.Net.Http\lib\wpa81\System.Net.Http.Extensions.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Net.Http.Primitives">
        <HintPath>$(SolutionDir)\packages\Microsoft.Net.Http\lib\wpa81\System.Net.Http.Primitives.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == 'Silverlight' And $(SilverlightVersion) == 'v4.0'">
    <ItemGroup>
      <Reference Include="System.Net.Http.Extensions">
        <HintPath>$(SolutionDir)\packages\Microsoft.Net.Http\lib\portable-net40+sl4+win8+wp71+wpa81\System.Net.Http.Extensions.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Net.Http.Primitives">
        <HintPath>$(SolutionDir)\packages\Microsoft.Net.Http\lib\portable-net40+sl4+win8+wp71+wpa81\System.Net.Http.Primitives.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Net.Http">
        <HintPath>$(SolutionDir)\packages\Microsoft.Net.Http\lib\portable-net40+sl4+win8+wp71+wpa81\System.Net.Http.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
</Choose>
</Project>"""

[<Test>]
let ``should generate Xml for System.Net.Http 2.2.8``() = 
    let model =     
        InstallModel.CreateFromLibs("System.Net.Http", SemVer.Parse "2.2.8",        
            [ @"..\packages\Microsoft.Net.Http\lib\monoandroid\System.Net.Http.Extensions.dll" 
              @"..\packages\Microsoft.Net.Http\lib\monoandroid\System.Net.Http.Primitives.dll" 
              
              @"..\packages\Microsoft.Net.Http\lib\monotouch\System.Net.Http.Extensions.dll" 
              @"..\packages\Microsoft.Net.Http\lib\monotouch\System.Net.Http.Primitives.dll" 

              @"..\packages\Microsoft.Net.Http\lib\net40\System.Net.Http.dll" 
              @"..\packages\Microsoft.Net.Http\lib\net40\System.Net.Http.Extensions.dll" 
              @"..\packages\Microsoft.Net.Http\lib\net40\System.Net.Http.Primitives.dll" 
              @"..\packages\Microsoft.Net.Http\lib\net40\System.Net.Http.WebRequest.dll" 
                     
              @"..\packages\Microsoft.Net.Http\lib\net45\System.Net.Http.Extensions.dll" 
              @"..\packages\Microsoft.Net.Http\lib\net45\System.Net.Http.Primitives.dll" 
              
              @"..\packages\Microsoft.Net.Http\lib\portable-net40+sl4+win8+wp71+wpa81\System.Net.Http.dll" 
              @"..\packages\Microsoft.Net.Http\lib\portable-net40+sl4+win8+wp71+wpa81\System.Net.Http.Extensions.dll" 
              @"..\packages\Microsoft.Net.Http\lib\portable-net40+sl4+win8+wp71+wpa81\System.Net.Http.Primitives.dll"
                            
              @"..\packages\Microsoft.Net.Http\lib\portable-net45+win8\System.Net.Http.Extensions.dll" 
              @"..\packages\Microsoft.Net.Http\lib\portable-net45+win8\System.Net.Http.Primitives.dll"

              @"..\packages\Microsoft.Net.Http\lib\win8\System.Net.Http.Extensions.dll" 
              @"..\packages\Microsoft.Net.Http\lib\win8\System.Net.Http.Primitives.dll"
              
              @"..\packages\Microsoft.Net.Http\lib\wpa81\System.Net.Http.Extensions.dll" 
              @"..\packages\Microsoft.Net.Http\lib\wpa81\System.Net.Http.Primitives.dll" ],
              Nuspec.All)

    let chooseNode = ProjectFile.GenerateTarget(model)
    chooseNode.OuterXml
    |> normalizeXml
    |> shouldEqual (normalizeXml expected)
