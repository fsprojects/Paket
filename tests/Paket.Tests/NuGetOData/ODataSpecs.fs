module Paket.ODataSpecs

open Paket
open NUnit.Framework
open FsUnit
open System.IO
open Paket.NuGetV2

open Paket.NuGetV3
open Paket.Requirements

let fakeUrl = "http://doesntmatter"

let parse fileName =
    parseODataDetails(fakeUrl,"package",SemVer.Parse "0",File.ReadAllText fileName)

[<Test>]
let ``can detect explicit dependencies for Fantomas``() = 
    parse "NuGetOData/Fantomas.xml"
    |> shouldEqual 
        { PackageName = "Fantomas"
          DownloadUrl = "http://www.nuget.org/api/v2/package/Fantomas/1.6.0"
          Dependencies = ["FSharp.Compiler.Service",DependenciesFileParser.parseVersionRequirement(">= 0.0.73"), []]
          Unlisted = false
          SourceUrl = fakeUrl }

[<Test>]
let ``can detect explicit dependencies for Rx-PlaformServices``() = 
    parse "NuGetOData/Rx-PlatformServices.xml"
    |> shouldEqual 
        { PackageName = "Rx-PlatformServices"
          DownloadUrl = "https://www.nuget.org/api/v2/package/Rx-PlatformServices/2.3.0"
          Dependencies = 
                ["Rx-Interfaces",DependenciesFileParser.parseVersionRequirement(">= 2.2"), []
                 "Rx-Core",DependenciesFileParser.parseVersionRequirement(">= 2.2"), []]
          Unlisted = true
          SourceUrl = fakeUrl }

[<Test>]
let ``can detect explicit dependencies for Fleece``() = 
    parse "NuGetOData/Fleece.xml"
    |> shouldEqual 
        { PackageName = "Fleece"
          DownloadUrl = "http://www.nuget.org/api/v2/package/Fleece/0.4.0"
          Unlisted = false
          Dependencies = 
            ["FSharpPlus",DependenciesFileParser.parseVersionRequirement(">= 0.0.4"), []
             "ReadOnlyCollectionInterfaces",DependenciesFileParser.parseVersionRequirement("1.0.0"), []
             "ReadOnlyCollectionExtensions",DependenciesFileParser.parseVersionRequirement(">= 1.2.0"), []
             "System.Json",DependenciesFileParser.parseVersionRequirement(">= 4.0.20126.16343"), []]
          SourceUrl = fakeUrl }

[<Test>]
let ``can detect explicit dependencies for ReadOnlyCollectionExtensions``() = 
    parse "NuGetOData/ReadOnlyCollectionExtensions.xml"
    |> shouldEqual 
        { PackageName = "ReadOnlyCollectionExtensions"
          DownloadUrl = "http://www.nuget.org/api/v2/package/ReadOnlyCollectionExtensions/1.2.0"
          Unlisted = false
          Dependencies = 
            ["LinqBridge",DependenciesFileParser.parseVersionRequirement(">= 1.3.0"), [FrameworkRestriction.Exactly(DotNetFramework(FrameworkVersion.V2))]
             "ReadOnlyCollectionInterfaces",DependenciesFileParser.parseVersionRequirement("1.0.0"), [FrameworkRestriction.Exactly(DotNetFramework(FrameworkVersion.V2))]
             "ReadOnlyCollectionInterfaces",DependenciesFileParser.parseVersionRequirement("1.0.0"), [FrameworkRestriction.Exactly(DotNetFramework(FrameworkVersion.V3_5))]
             "ReadOnlyCollectionInterfaces",DependenciesFileParser.parseVersionRequirement("1.0.0"), [FrameworkRestriction.Exactly(DotNetFramework(FrameworkVersion.V4_Client))]]
          SourceUrl = fakeUrl }

[<Test>]
let ``can calculate v3 path``() = 
    calculateNuGet3Path "https://nuget.org/api/v2" |> shouldEqual (Some "http://preview.nuget.org/ver3-preview/index.json")
    calculateNuGet3Path "http://nuget.org/api/v2" |> shouldEqual (Some "http://preview.nuget.org/ver3-preview/index.json")

[<Test>]
let ``can read all versions from single page with multiple entries``() =
    let getUrlContentsStub _ = async { return File.ReadAllText "NuGetOData/NUnit.xml" }
    
    let versions = getAllVersionsFromNugetOData(getUrlContentsStub, fakeUrl, "NUnit")
                   |> Async.RunSynchronously

    versions |> shouldContain "3.0.0-alpha-2"
    versions |> shouldContain "3.0.0-alpha"
    versions |> shouldContain "2.6.3"
    versions |> shouldContain "2.6.2"
    versions |> shouldContain "2.6.1"
    versions |> shouldContain "2.6.0.12054"
    versions |> shouldContain "2.5.10.11092"
    versions |> shouldContain "2.5.9.10348"
    versions |> shouldContain "2.5.7.10213"
