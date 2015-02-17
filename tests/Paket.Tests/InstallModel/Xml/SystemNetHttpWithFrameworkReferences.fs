module Paket.InstallModel.Xml.SystemNetHttpWithFramweworkReferencesSpecs

open Paket
open NUnit.Framework
open FsUnit
open Paket.TestHelpers
open Paket.Domain
open Paket.Requirements

let expected = """
<Choose xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
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
  <When Condition="($(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v4.5' Or $(TargetFrameworkVersion) == 'v4.5.1' Or $(TargetFrameworkVersion) == 'v4.5.2' Or $(TargetFrameworkVersion) == 'v4.5.3')) Or ($(TargetFrameworkIdentifier) == 'MonoAndroid') Or ($(TargetFrameworkIdentifier) == 'MonoTouch')">
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
</Choose>"""

[<Test>]
let ``should generate Xml for System.Net.Http 2.2.8``() = 
    let model =     
        InstallModel.CreateFromLibs(PackageName "System.Net.Http", SemVer.Parse "2.2.8", [],
            [ @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.dll" 
              @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.Extensions.dll" 
              @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.Primitives.dll" 
              @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.WebRequest.dll" 
                     
              @"..\Microsoft.Net.Http\lib\net45\System.Net.Http.Extensions.dll" 
              @"..\Microsoft.Net.Http\lib\net45\System.Net.Http.Primitives.dll"],
               [],
               { References = NuspecReferences.All
                 OfficialName = "Microsoft.Net.Http"
                 Dependencies = []
                 FrameworkAssemblyReferences =
                 [{ AssemblyName = "System.Net.Http"; FrameworkRestrictions = [FrameworkRestriction.AtLeast(DotNetFramework(FrameworkVersion.V4_5))] }
                  { AssemblyName = "System.Net.Http.WebRequest"; FrameworkRestrictions = [FrameworkRestriction.Exactly(DotNetFramework(FrameworkVersion.V4_5))] }]})

    let _,chooseNode,_ = ProjectFile.Load("./ProjectFile/TestData/Empty.fsprojtest").Value.GenerateXml(model,true,true)
    chooseNode.OuterXml
    |> normalizeXml
    |> shouldEqual (normalizeXml expected)
