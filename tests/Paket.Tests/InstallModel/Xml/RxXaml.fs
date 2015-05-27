module Paket.InstallModel.Xml.RxXaml

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
      <Reference Include="System.Reactive.Windows.Threading">
        <HintPath>..\..\..\Rx-XAML\lib\windows8\System.Reactive.Windows.Threading.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v4.0')">
    <ItemGroup>
      <Reference Include="System.Reactive.Windows.Threading">
        <HintPath>..\..\..\Rx-XAML\lib\net40\System.Reactive.Windows.Threading.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="WindowsBase">
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == 'Silverlight' And $(TargetFrameworkVersion) == 'v5.0'">
    <ItemGroup>
      <Reference Include="System.Reactive.Windows.Threading">
        <HintPath>..\..\..\Rx-XAML\lib\sl5\System.Reactive.Windows.Threading.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Windows">
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="$(TargetFrameworkIdentifier) == 'WindowsPhone' And $(TargetFrameworkVersion) == 'v7.1'">
    <ItemGroup>
      <Reference Include="System.Reactive.Windows.Threading">
        <HintPath>..\..\..\Rx-XAML\lib\windowsphone71\System.Reactive.Windows.Threading.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="System.Windows">
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
  <When Condition="($(TargetFrameworkIdentifier) == '.NETFramework' And ($(TargetFrameworkVersion) == 'v4.5' Or $(TargetFrameworkVersion) == 'v4.5.1' Or $(TargetFrameworkVersion) == 'v4.5.2' Or $(TargetFrameworkVersion) == 'v4.5.3')) Or ($(TargetFrameworkIdentifier) == 'MonoAndroid') Or ($(TargetFrameworkIdentifier) == 'MonoTouch')">
    <ItemGroup>
      <Reference Include="System.Reactive.Windows.Threading">
        <HintPath>..\..\..\Rx-XAML\lib\net45\System.Reactive.Windows.Threading.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
      <Reference Include="WindowsBase">
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
  <When Condition="($(TargetFrameworkIdentifier) == 'WindowsPhoneApp') Or ($(TargetFrameworkProfile) == 'Profile32')">
    <ItemGroup>
      <Reference Include="System.Reactive.Windows.Threading">
        <HintPath>..\..\..\Rx-XAML\lib\portable-win81+wpa81\System.Reactive.Windows.Threading.dll</HintPath>
        <Private>True</Private>
        <Paket>True</Paket>
      </Reference>
    </ItemGroup>
  </When>
</Choose>"""

[<Test>]
let ``should generate Xml for Rx-XAML 2.2.4 with correct framework assembly references``() = 
    let model =     
        InstallModel.CreateFromLibs(PackageName "Rx-XAML", SemVer.Parse "2.2.4", [],
            [ @"..\Rx-XAML\lib\net40\System.Reactive.Windows.Threading.dll" 
              @"..\Rx-XAML\lib\net45\System.Reactive.Windows.Threading.dll" 
              @"..\Rx-XAML\lib\portable-win81+wpa81\System.Reactive.Windows.Threading.dll" 
              @"..\Rx-XAML\lib\sl5\System.Reactive.Windows.Threading.dll" 
              @"..\Rx-XAML\lib\windows8\System.Reactive.Windows.Threading.dll" 
              @"..\Rx-XAML\lib\windowsphone8\System.Reactive.Windows.Threading.dll" 
              @"..\Rx-XAML\lib\windowsphone71\System.Reactive.Windows.Threading.dll" ],
               [],
               { References = NuspecReferences.All
                 OfficialName = "Reactive Extensions - XAML Support Library"
                 Dependencies = []
                 FrameworkAssemblyReferences =
                 [{ AssemblyName = "WindowsBase"; FrameworkRestrictions = [FrameworkRestriction.Exactly(DotNetFramework FrameworkVersion.V4_5)] }
                  { AssemblyName = "WindowsBase"; FrameworkRestrictions = [FrameworkRestriction.Exactly(DotNetFramework FrameworkVersion.V4)] }
                  { AssemblyName = "System.Windows"; FrameworkRestrictions = [FrameworkRestriction.Exactly(Silverlight "v5.0")] }
                  { AssemblyName = "System.Windows"; FrameworkRestrictions = [FrameworkRestriction.Exactly(WindowsPhoneSilverlight "v7.1")] }]})

    let _,chooseNode,_ = ProjectFile.Load("./ProjectFile/TestData/Empty.fsprojtest").Value.GenerateXml(model,true,true)
    chooseNode.OuterXml
    |> normalizeXml
    |> shouldEqual (normalizeXml expected)
