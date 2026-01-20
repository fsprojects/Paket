module Paket.InstallModel.Xml.RxXaml

open Paket
open NUnit.Framework
open FsUnit
open Paket.TestHelpers
open Paket.Domain
open Paket.Requirements

let expected = System.IO.File.ReadAllText(System.IO.Path.Combine(__SOURCE_DIRECTORY__,__SOURCE_FILE__ + ".expected.xml"))
let actualPath = System.IO.Path.Combine(__SOURCE_DIRECTORY__,__SOURCE_FILE__ + ".actual.xml")

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
    let expected = normalizeXml expected

    if currentXml <> expected then
      // making it easier to troubleshoot this test
      System.IO.File.WriteAllText(actualPath, currentXml)

    currentXml
    |> shouldEqual (normalizeXml expected)
