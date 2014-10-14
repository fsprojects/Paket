module Paket.InstallModel.Xml.SystemNetHttpWithFramweworkReferencesSpecs

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
        <HintPath>$(SolutionDir)/packages/Microsoft.Net.Http/lib/net40/System.Net.Http.Extensions.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Net.Http.Primitives">
        <HintPath>$(SolutionDir)/packages/Microsoft.Net.Http/lib/net40/System.Net.Http.Primitives.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Net.Http.WebRequest">
        <HintPath>$(SolutionDir)/packages/Microsoft.Net.Http/lib/net40/System.Net.Http.WebRequest.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Net.Http">
        <HintPath>$(SolutionDir)/packages/Microsoft.Net.Http/lib/net40/System.Net.Http.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And $(TargetFrameworkVersion) == 'v4.5'">
    <ItemGroup>
      <Reference Include="System.Net.Http.Extensions">
        <HintPath>$(SolutionDir)/packages/Microsoft.Net.Http/lib/net45/System.Net.Http.Extensions.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Net.Http.Primitives">
        <HintPath>$(SolutionDir)/packages/Microsoft.Net.Http/lib/net45/System.Net.Http.Primitives.dll</HintPath>
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
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And $(TargetFrameworkVersion) == 'v4.5.1'">
    <ItemGroup>
      <Reference Include="System.Net.Http.Extensions">
        <HintPath>$(SolutionDir)/packages/Microsoft.Net.Http/lib/net45/System.Net.Http.Extensions.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Net.Http.Primitives">
        <HintPath>$(SolutionDir)/packages/Microsoft.Net.Http/lib/net45/System.Net.Http.Primitives.dll</HintPath>
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
</Choose>
</Project>"""

[<Test>]
let ``should generate Xml for System.Net.Http 2.2.8``() = 
    let model =     
        InstallModel.CreateFromLibs("System.Net.Http", SemVer.Parse "2.2.8",
            [ @"../packages/Microsoft.Net.Http/lib/net40/System.Net.Http.dll" 
              @"../packages/Microsoft.Net.Http/lib/net40/System.Net.Http.Extensions.dll" 
              @"../packages/Microsoft.Net.Http/lib/net40/System.Net.Http.Primitives.dll" 
              @"../packages/Microsoft.Net.Http/lib/net40/System.Net.Http.WebRequest.dll" 
                     
              @"../packages/Microsoft.Net.Http/lib/net45/System.Net.Http.Extensions.dll" 
              @"../packages/Microsoft.Net.Http/lib/net45/System.Net.Http.Primitives.dll"],
               { References = NuspecReferences.All
                 FrameworkAssemblyReferences =
                 [{ AssemblyName = "System.Net.Http"; TargetFramework = DotNetFramework(FrameworkVersion.Framework(FrameworkVersionNo.V4_5),Full) }
                  { AssemblyName = "System.Net.Http.WebRequest"; TargetFramework = DotNetFramework(FrameworkVersion.Framework(FrameworkVersionNo.V4_5),Full) }] })

    let chooseNode = ProjectFile.GenerateTarget(model)
    chooseNode.OuterXml
    |> normalizeXml
    |> shouldEqual (normalizeXml expected)
