module Paket.InstallModel.Xml.RxXaml

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
      <Reference Include="WindowsBase">
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Reactive.Windows.Threading">
        <HintPath>..\..\..\Rx-XAML\lib\net40\System.Reactive.Windows.Threading.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v4.5' Or $(TargetFrameworkVersion) == 'v4.5.1' Or $(TargetFrameworkVersion) == 'v4.5.2' Or $(TargetFrameworkVersion) == 'v4.5.3' Or $(TargetFrameworkVersion) == 'v4.6' Or $(TargetFrameworkVersion) == 'v4.6.1' Or $(TargetFrameworkVersion) == 'v4.6.2' Or $(TargetFrameworkVersion) == 'v4.6.3' Or $(TargetFrameworkVersion) == 'v4.7' Or $(TargetFrameworkVersion) == 'v4.7.1' Or $(TargetFrameworkVersion) == 'v4.7.2' Or $(TargetFrameworkVersion) == 'v4.8')">
    <ItemGroup>
      <Reference Include="WindowsBase">
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Reactive.Windows.Threading">
        <HintPath>..\..\..\Rx-XAML\lib\net45\System.Reactive.Windows.Threading.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="($(TargetFrameworkIdentifier) == 'WindowsPhoneApp') Or ($(TargetFrameworkIdentifier) == 'MonoAndroid' And ($(TargetFrameworkVersion) == 'v7.0' Or $(TargetFrameworkVersion) == 'v7.1' Or $(TargetFrameworkVersion) == 'v8.0' Or $(TargetFrameworkVersion) == 'v8.1' Or $(TargetFrameworkVersion) == 'v9.0')) Or ($(TargetFrameworkIdentifier) == 'MonoTouch') Or ($(TargetFrameworkIdentifier) == '.NETCoreApp' And ($(TargetFrameworkVersion) == 'v1.0' Or $(TargetFrameworkVersion) == 'v1.1' Or $(TargetFrameworkVersion) == 'v2.0' Or $(TargetFrameworkVersion) == 'v2.1' Or $(TargetFrameworkVersion) == 'v2.2' Or $(TargetFrameworkVersion) == 'v3.0')) Or ($(TargetFrameworkIdentifier) == '.NETStandard' And ($(TargetFrameworkVersion) == 'v1.2' Or $(TargetFrameworkVersion) == 'v1.3' Or $(TargetFrameworkVersion) == 'v1.4' Or $(TargetFrameworkVersion) == 'v1.5' Or $(TargetFrameworkVersion) == 'v1.6' Or $(TargetFrameworkVersion) == 'v2.0' Or $(TargetFrameworkVersion) == 'v2.1')) Or ($(TargetFrameworkProfile) == 'Profile32') Or ($(TargetFrameworkIdentifier) == 'Xamarin.iOS') Or ($(TargetFrameworkIdentifier) == 'Xamarin.Mac') Or ($(TargetFrameworkIdentifier) == 'Xamarin.tvOS') Or ($(TargetFrameworkIdentifier) == 'Xamarin.watchOS')">
    <ItemGroup>
      <Reference Include="System.Reactive.Windows.Threading">
        <HintPath>..\..\..\Rx-XAML\lib\portable-win81+wpa81\System.Reactive.Windows.Threading.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == 'Silverlight' And $(TargetFrameworkVersion) == 'v5.0'">
    <ItemGroup>
      <Reference Include="System.Windows">
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Reactive.Windows.Threading">
        <HintPath>..\..\..\Rx-XAML\lib\sl5\System.Reactive.Windows.Threading.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETCore'">
    <ItemGroup>
      <Reference Include="System.Reactive.Windows.Threading">
        <HintPath>..\..\..\Rx-XAML\lib\windows8\System.Reactive.Windows.Threading.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == 'WindowsPhone' And ($(TargetFrameworkVersion) == 'v7.1' Or $(TargetFrameworkVersion) == 'v7.5')">
    <ItemGroup>
      <Reference Include="System.Windows">
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Reactive.Windows.Threading">
        <HintPath>..\..\..\Rx-XAML\lib\windowsphone71\System.Reactive.Windows.Threading.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == 'WindowsPhone' And ($(TargetFrameworkVersion) == 'v8.0' Or $(TargetFrameworkVersion) == 'v8.1')">
    <ItemGroup>
      <Reference Include="System.Reactive.Windows.Threading">
        <HintPath>..\..\..\Rx-XAML\lib\windowsphone8\System.Reactive.Windows.Threading.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
</Choose>"""

[<Test>]
let ``should generate Xml for Rx-XAML 2.2.4 with correct framework assembly references``() =
    ensureDir()
    let model =
        InstallModel.CreateFromLibs(PackageName "Rx-XAML", SemVer.Parse "2.2.4", InstallModelKind.Package, FrameworkRestriction.NoRestriction,
            [ @"..\Rx-XAML\lib\net40\System.Reactive.Windows.Threading.dll"
              @"..\Rx-XAML\lib\net45\System.Reactive.Windows.Threading.dll"
              @"..\Rx-XAML\lib\portable-win81+wpa81\System.Reactive.Windows.Threading.dll"
              @"..\Rx-XAML\lib\sl5\System.Reactive.Windows.Threading.dll"
              @"..\Rx-XAML\lib\windows8\System.Reactive.Windows.Threading.dll"
              @"..\Rx-XAML\lib\windowsphone8\System.Reactive.Windows.Threading.dll"
              @"..\Rx-XAML\lib\windowsphone71\System.Reactive.Windows.Threading.dll" ]
            |> Paket.InstallModel.ProcessingSpecs.fromLegacyList @"..\Rx-XAML\",
               [],
               [],
               { References = NuspecReferences.All
                 OfficialName = "Reactive Extensions - XAML Support Library"
                 Version = "2.2.4"
                 Dependencies = lazy []
                 LicenseUrl = ""
                 IsDevelopmentDependency = false
                 FrameworkAssemblyReferences =
                 [{ AssemblyName = "WindowsBase"; FrameworkRestrictions = makeOrList [FrameworkRestriction.Exactly(DotNetFramework FrameworkVersion.V4_5)] }
                  { AssemblyName = "WindowsBase"; FrameworkRestrictions = makeOrList [FrameworkRestriction.Exactly(DotNetFramework FrameworkVersion.V4)] }
                  { AssemblyName = "System.Windows"; FrameworkRestrictions = makeOrList [FrameworkRestriction.Exactly(Silverlight SilverlightVersion.V5)] }
                  { AssemblyName = "System.Windows"; FrameworkRestrictions = makeOrList [FrameworkRestriction.Exactly(WindowsPhone WindowsPhoneVersion.V7_5)] }]})

    let ctx = ProjectFile.TryLoad("./ProjectFile/TestData/Empty.fsprojtest").Value.GenerateXml(model, System.Collections.Generic.HashSet<_>(),Map.empty,None,Some true,None,true,KnownTargetProfiles.AllProfiles,None)
    let currentXml = ctx.ChooseNodes.Head.OuterXml  |> normalizeXml
    currentXml
    |> shouldEqual (normalizeXml expected)
