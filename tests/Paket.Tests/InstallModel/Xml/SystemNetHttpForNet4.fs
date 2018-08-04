module Paket.InstallModel.Xml.SystemNetHttpForNet2Specs

open Paket
open NUnit.Framework
open FsUnit
open Paket.TestHelpers
open Paket.Domain
open Paket.Requirements

let expected = """
<Choose xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And $(TargetFrameworkVersion) == 'v4.0'">
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
      <Reference Include="System.Net.Http.WebRequest">
        <HintPath>..\..\..\Microsoft.Net.Http\lib\net40\System.Net.Http.WebRequest.dll</HintPath>
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
        InstallModel.CreateFromLibs(PackageName "System.Net.Http", SemVer.Parse "2.2.8", InstallModelKind.Package, FrameworkRestriction.Exactly(DotNetFramework(FrameworkVersion.V4)),
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
              @"..\Microsoft.Net.Http\lib\wpa81\System.Net.Http.Primitives.dll" ] |> Paket.InstallModel.ProcessingSpecs.fromLegacyList @"..\Microsoft.Net.Http\",
              [],
              [],
              Nuspec.All)

    let ctx = ProjectFile.TryLoad("./ProjectFile/TestData/Empty.fsprojtest").Value.GenerateXml(model, System.Collections.Generic.HashSet<_>(),Map.empty,None,Some true,None,true,KnownTargetProfiles.AllProfiles,None) 
    let currentXML = ctx.ChooseNodes.Head.OuterXml |> normalizeXml
    currentXML
    |> shouldEqual (normalizeXml expected)
