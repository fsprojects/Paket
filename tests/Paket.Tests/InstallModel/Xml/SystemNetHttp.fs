module Paket.InstallModel.Xml.SystemNetHttpSpecs

open Paket
open NUnit.Framework
open FsUnit
open Paket.TestHelpers

let expected = """
<Choose xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework'">
    <Choose>
      <When Condition="$(TargetFrameworkVersion) == 'v1.0'">
        <ItemGroup />
      </When>
      <When Condition="$(TargetFrameworkVersion) == 'v1.1'">
        <ItemGroup />
      </When>
      <When Condition="$(TargetFrameworkVersion) == 'v2.0'">
        <ItemGroup />
      </When>
      <When Condition="$(TargetFrameworkVersion) == 'v3.5'">
        <ItemGroup />
      </When>
      <When Condition="$(TargetFrameworkVersion) == 'v4.0' And $(TargetFrameworkProfile) == 'Client'">
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
      <When Condition="$(TargetFrameworkVersion) == 'v4.0'">
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
      <Otherwise>
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
      </Otherwise>
    </Choose>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETPortable'">
    <Choose>
      <When Condition="$(TargetFrameworkProfile) == 'Profile88' And $(TargetPlatformIdentifier) == 'Portable' And $(TargetPlatformVersion) == '7.0'">
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
      <Otherwise>
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
      </Otherwise>
    </Choose>
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
  <When Condition="$(TargetFrameworkIdentifier) == 'Silverlight'">
    <Choose>
      <When Condition="$(SilverlightVersion) == 'v4.0'">
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
      <Otherwise>
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
      </Otherwise>
    </Choose>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == 'Windows'">
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
  <When Condition="$(TargetFrameworkIdentifier) == 'WindowsPhoneApp'">
    <Choose>
      <When Condition="$(TargetPlatformVersion) == '7.1'">
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
      <When Condition="$(TargetPlatformVersion) == 'v8.0'">
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
      <Otherwise>
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
      </Otherwise>
    </Choose>
  </When>
  <Otherwise>
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
  </Otherwise>
</Choose>"""

[<Test>]
let ``should generate Xml for System.Net.Http 2.2.8``() = 
    let model =     
        InstallModel.CreateFromLibs("System.Net.Http", SemVer.Parse "2.2.8",        
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
              Nuspec.All).FilterFallbacks()

    let chooseNode = ProjectFile.Load("./ProjectFile/TestData/Empty.fsprojtest").Value.GenerateXml(model)
    chooseNode.OuterXml
    |> normalizeXml
    |> shouldEqual (normalizeXml expected)
