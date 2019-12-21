module Paket.InstallModel.Xml.SystemNetHttpWithExistingFramweworkReferencesSpecs

open Paket
open NUnit.Framework
open FsUnit
open Paket.TestHelpers
open Paket.Domain
open Paket.Requirements

let expected = """
<Choose xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v4.0' Or $(TargetFrameworkVersion) == 'v4.0.3')">
    <ItemGroup>
      <Reference Include="System.Net.Http">
        <HintPath>..\..\..\Microsoft.Net.Http\lib\net40\System.Net.Http.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
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
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v4.5' Or $(TargetFrameworkVersion) == 'v4.5.1' Or $(TargetFrameworkVersion) == 'v4.5.2' Or $(TargetFrameworkVersion) == 'v4.5.3' Or $(TargetFrameworkVersion) == 'v4.6' Or $(TargetFrameworkVersion) == 'v4.6.1' Or $(TargetFrameworkVersion) == 'v4.6.2' Or $(TargetFrameworkVersion) == 'v4.6.3' Or $(TargetFrameworkVersion) == 'v4.7' Or $(TargetFrameworkVersion) == 'v4.7.1' Or $(TargetFrameworkVersion) == 'v4.7.2' Or $(TargetFrameworkVersion) == 'v4.8')">
    <ItemGroup>
      <Reference Include="System.Net.Http">
        <Paket>True</Paket>
      </Reference>
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
</Choose>"""

[<Test>]
let ``should generate Xml for System.Net.Http 2.2.8``() = 
    ensureDir()
    let model =
        InstallModel.CreateFromLibs(PackageName "System.Net.Http", SemVer.Parse "2.2.8", InstallModelKind.Package, FrameworkRestriction.NoRestriction,
            [ @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.dll"
              @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.Extensions.dll"
              @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.Primitives.dll"
              @"..\Microsoft.Net.Http\lib\net40\System.Net.Http.WebRequest.dll"

              @"..\Microsoft.Net.Http\lib\net45\System.Net.Http.Extensions.dll"
              @"..\Microsoft.Net.Http\lib\net45\System.Net.Http.Primitives.dll"]
            |> Paket.InstallModel.ProcessingSpecs.fromLegacyList @"..\Microsoft.Net.Http\",
               [],
               [],
               { References = NuspecReferences.All
                 OfficialName = "Microsoft.Net.Http"
                 Version = "2.2.8"
                 Dependencies = lazy []
                 LicenseUrl = ""
                 IsDevelopmentDependency = false
                 FrameworkAssemblyReferences =
                 [{ AssemblyName = "System.Net.Http"; FrameworkRestrictions = makeOrList [FrameworkRestriction.Exactly(DotNetFramework(FrameworkVersion.V4_5))] }
                  { AssemblyName = "System.Net.Http.WebRequest"; FrameworkRestrictions = makeOrList [FrameworkRestriction.Exactly(DotNetFramework(FrameworkVersion.V4_5))] }]})

    let ctx = ProjectFile.TryLoad("./ProjectFile/TestData/FrameworkAssemblies.fsprojtest").Value.GenerateXml(model, System.Collections.Generic.HashSet<_>(),Map.empty,None,Some true,None,true,KnownTargetProfiles.AllProfiles,None)
    let currentXML = ctx.ChooseNodes.Head.OuterXml |> normalizeXml
    currentXML
    |> shouldEqual (normalizeXml expected)
