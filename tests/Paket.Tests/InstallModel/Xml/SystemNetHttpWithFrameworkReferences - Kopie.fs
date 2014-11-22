module Paket.InstallModel.Xml.SystemNetHttpWithFramweworkReferencesSpecs

open Paket
open NUnit.Framework
open FsUnit
open Paket.TestHelpers
open Paket.Domain

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
      <When Condition="$(TargetFrameworkVersion) == 'v4.5'">
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
          <Reference Include="System.Net.Http">
            <Paket>True</Paket>
          </Reference>
          <Reference Include="System.Net.Http.WebRequest">
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
  <When Condition="$(TargetFrameworkIdentifier) == 'MonoAndroid'">
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
  <When Condition="$(TargetFrameworkIdentifier) == 'MonoTouch'">
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
  <When Condition="$(TargetFrameworkIdentifier) == 'Silverlight'">
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
  <When Condition="$(TargetFrameworkIdentifier) == 'Windows'">
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
  <When Condition="$(TargetFrameworkIdentifier) == 'WindowsPhoneApp'">
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
        InstallModel.CreateFromLibs(PackageName "System.Net.Http", SemVer.Parse "2.2.8", None,
            [ @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.dll" 
              @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.Extensions.dll" 
              @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.Primitives.dll" 
              @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.WebRequest.dll" 
                     
              @"..\Microsoft.Net.Http\lib\net45\System.Net.Http.Extensions.dll" 
              @"..\Microsoft.Net.Http\lib\net45\System.Net.Http.Primitives.dll"],
               { References = NuspecReferences.All
                 OfficialName = "Microsoft.Net.Http"
                 Dependencies = []
                 FrameworkAssemblyReferences =
                 [{ AssemblyName = "System.Net.Http"; TargetFramework = Some(DotNetFramework(FrameworkVersion.V4_5)) }
                  { AssemblyName = "System.Net.Http.WebRequest"; TargetFramework = Some(DotNetFramework(FrameworkVersion.V4_5)) }]})
            .FilterFallbacks()

    let chooseNode = ProjectFile.Load("./ProjectFile/TestData/Empty.fsprojtest").Value.GenerateXml(model)
    chooseNode.OuterXml
    |> normalizeXml
    |> shouldEqual (normalizeXml expected)
