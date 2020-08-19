module Paket.InstallModel.Xml.SystemSecurityCryptographyAlgorithms

open FsUnit
open NUnit.Framework
open Paket
open Paket.Requirements
open Paket.Domain
open Paket.TestHelpers

let expected = """
<Choose xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And $(TargetFrameworkVersion) == 'v4.6'">
    <ItemGroup>
      <Reference Include="System.Security.Cryptography.Algorithms">
        <HintPath>..\..\..\System.Security.Cryptography.Algorithms\lib\net46\System.Security.Cryptography.Algorithms.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v4.6.1' Or $(TargetFrameworkVersion) == 'v4.6.2')">
    <ItemGroup>
      <Reference Include="System.Security.Cryptography.Algorithms">
        <HintPath>..\..\..\System.Security.Cryptography.Algorithms\lib\net461\System.Security.Cryptography.Algorithms.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v4.6.3' Or $(TargetFrameworkVersion) == 'v4.7' Or $(TargetFrameworkVersion) == 'v4.7.1' Or $(TargetFrameworkVersion) == 'v4.7.2' Or $(TargetFrameworkVersion) == 'v4.8')">
    <ItemGroup>
      <Reference Include="System.Security.Cryptography.Algorithms">
        <HintPath>..\..\..\System.Security.Cryptography.Algorithms\lib\net463\System.Security.Cryptography.Algorithms.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETStandard' And $(TargetFrameworkVersion) == 'v1.3'">
    <ItemGroup>
      <Reference Include="System.Security.Cryptography.Algorithms">
        <HintPath>..\..\..\System.Security.Cryptography.Algorithms\ref\netstandard1.3\System.Security.Cryptography.Algorithms.dll</HintPath>
        <Private>False</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETStandard' And ($(TargetFrameworkVersion) == 'v1.4' Or $(TargetFrameworkVersion) == 'v1.5')">
    <ItemGroup>
      <Reference Include="System.Security.Cryptography.Algorithms">
        <HintPath>..\..\..\System.Security.Cryptography.Algorithms\ref\netstandard1.4\System.Security.Cryptography.Algorithms.dll</HintPath>
        <Private>False</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="($(TargetFrameworkIdentifier) == '.NETCoreApp' And ($(TargetFrameworkVersion) == 'v1.0' Or $(TargetFrameworkVersion) == 'v1.1' Or $(TargetFrameworkVersion) == 'v2.0' Or $(TargetFrameworkVersion) == 'v2.1' Or $(TargetFrameworkVersion) == 'v2.2' Or $(TargetFrameworkVersion) == 'v3.0' Or $(TargetFrameworkVersion) == 'v3.1')) Or ($(TargetFrameworkIdentifier) == '.NETStandard' And ($(TargetFrameworkVersion) == 'v1.6' Or $(TargetFrameworkVersion) == 'v2.0' Or $(TargetFrameworkVersion) == 'v2.1'))">
    <ItemGroup>
      <Reference Include="System.Security.Cryptography.Algorithms">
        <HintPath>..\..\..\System.Security.Cryptography.Algorithms\ref\netstandard1.6\System.Security.Cryptography.Algorithms.dll</HintPath>
        <Private>False</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
</Choose>"""

[<Test>]
let ``should generate Xml for System.Security.Cryptography.Algorithms in CSharp project``() =
    ensureDir()
    let model =
        InstallModel.CreateFromLibs(PackageName "System.Security.Cryptography.Algorithms", SemVer.Parse "1.2.0", InstallModelKind.Package, FrameworkRestriction.NoRestriction,
            [ @"..\System.Security.Cryptography.Algorithms\lib\net46\System.Security.Cryptography.Algorithms.dll"
              @"..\System.Security.Cryptography.Algorithms\lib\net461\System.Security.Cryptography.Algorithms.dll"
              @"..\System.Security.Cryptography.Algorithms\lib\net463\System.Security.Cryptography.Algorithms.dll"
              @"..\System.Security.Cryptography.Algorithms\ref\net46\System.Security.Cryptography.Algorithms.dll"
              @"..\System.Security.Cryptography.Algorithms\ref\net461\System.Security.Cryptography.Algorithms.dll"
              @"..\System.Security.Cryptography.Algorithms\ref\net463\System.Security.Cryptography.Algorithms.dll"
              @"..\System.Security.Cryptography.Algorithms\ref\netstandard1.3\System.Security.Cryptography.Algorithms.dll"
              @"..\System.Security.Cryptography.Algorithms\ref\netstandard1.4\System.Security.Cryptography.Algorithms.dll"
              @"..\System.Security.Cryptography.Algorithms\ref\netstandard1.6\System.Security.Cryptography.Algorithms.dll"

              @"..\System.Security.Cryptography.Algorithms\runtimes\unix\lib\netstandard1.6\System.Security.Cryptography.Algorithms.dll"
              @"..\System.Security.Cryptography.Algorithms\runtimes\win\lib\net46\System.Security.Cryptography.Algorithms.dll"
              @"..\System.Security.Cryptography.Algorithms\runtimes\win\lib\net461\System.Security.Cryptography.Algorithms.dll"
              @"..\System.Security.Cryptography.Algorithms\runtimes\win\lib\net463\System.Security.Cryptography.Algorithms.dll"
              @"..\System.Security.Cryptography.Algorithms\runtimes\win\lib\netcore50\System.Security.Cryptography.Algorithms.dll"
              @"..\System.Security.Cryptography.Algorithms\runtimes\win\lib\netstandard1.6\System.Security.Cryptography.Algorithms.dll" ]
            |> Paket.InstallModel.ProcessingSpecs.fromLegacyList @"..\System.Security.Cryptography.Algorithms\",
            [],
            [],
            Nuspec.All)

    let project = ProjectFile.TryLoad("./ProjectFile/TestData/EmptyCsharpGuid.csprojtest")
    Assert.IsTrue(project.IsSome)
    let ctx = project.Value.GenerateXml(model, System.Collections.Generic.HashSet<_>(),Map.empty,None,None,None,true,KnownTargetProfiles.AllProfiles,None)
    let result =
      ctx.ChooseNodes
      |> (fun n -> n.Head.OuterXml)
      |> normalizeXml
    let expectedXml = normalizeXml expected
    result |> shouldEqual expectedXml
