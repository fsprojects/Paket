module Paket.ODataSpecs

open Paket
open NUnit.Framework
open FsUnit
open System.IO
open Paket.NuGetV2

open Paket.NuGetV3

let fakeUrl = "http://doesntmatter"

let parse fileName =
    parseODataDetails(fakeUrl,"package",SemVer.Parse "0",File.ReadAllText fileName)

[<Test>]
let ``can detect explicit dependencies for Fantomas``() = 
    parse "NuGetOData/Fantomas.xml"
    |> shouldEqual 
        { Name = "Fantomas"
          DownloadUrl = "http://www.nuget.org/api/v2/package/Fantomas/1.6.0"
          Dependencies = ["FSharp.Compiler.Service",DependenciesFileParser.parseVersionRequirement(">= 0.0.73"), None]
          Unlisted = false
          SourceUrl = fakeUrl }

[<Test>]
let ``can detect explicit dependencies for Rx-PlaformServices``() = 
    parse "NuGetOData/Rx-PlatformServices.xml"
    |> shouldEqual 
        { Name = "Rx-PlatformServices"
          DownloadUrl = "https://www.nuget.org/api/v2/package/Rx-PlatformServices/2.3.0"
          Dependencies = ["Rx-Interfaces",DependenciesFileParser.parseVersionRequirement(">= 2.2"), None
                          "Rx-Core",DependenciesFileParser.parseVersionRequirement(">= 2.2"), None]
          Unlisted = true
          SourceUrl = fakeUrl }

[<Test>]
let ``can detect explicit dependencies for Fleece``() = 
    parse "NuGetOData/Fleece.xml"
    |> shouldEqual 
        { Name = "Fleece"
          DownloadUrl = "http://www.nuget.org/api/v2/package/Fleece/0.4.0"
          Unlisted = false
          Dependencies = 
            ["FSharpPlus",DependenciesFileParser.parseVersionRequirement(">= 0.0.4"), None
             "ReadOnlyCollectionInterfaces",DependenciesFileParser.parseVersionRequirement("1.0.0"), None
             "ReadOnlyCollectionExtensions",DependenciesFileParser.parseVersionRequirement(">= 1.2.0"), None
             "System.Json",DependenciesFileParser.parseVersionRequirement(">= 4.0.20126.16343"), None]
          SourceUrl = fakeUrl }

[<Test>]
let ``can detect explicit dependencies for ReadOnlyCollectionExtensions``() = 
    parse "NuGetOData/ReadOnlyCollectionExtensions.xml"
    |> shouldEqual 
        { Name = "ReadOnlyCollectionExtensions"
          DownloadUrl = "http://www.nuget.org/api/v2/package/ReadOnlyCollectionExtensions/1.2.0"
          Unlisted = false
          Dependencies = 
            ["LinqBridge",DependenciesFileParser.parseVersionRequirement(">= 1.3.0"), Some(DotNetFramework(FrameworkVersion.V2))
             "ReadOnlyCollectionInterfaces",DependenciesFileParser.parseVersionRequirement("1.0.0"), Some(DotNetFramework(FrameworkVersion.V2))
             "ReadOnlyCollectionInterfaces",DependenciesFileParser.parseVersionRequirement("1.0.0"), Some(DotNetFramework(FrameworkVersion.V3_5))
             "ReadOnlyCollectionInterfaces",DependenciesFileParser.parseVersionRequirement("1.0.0"), Some(DotNetFramework(FrameworkVersion.V4_Client))]
          SourceUrl = fakeUrl }

[<Test>]
let ``can calculate v3 path``() = 
    calculateNuGet3Path "https://nuget.org/api/v2" |> shouldEqual (Some "http://preview.nuget.org/ver3-preview/index.json")
    calculateNuGet3Path "http://nuget.org/api/v2" |> shouldEqual (Some "http://preview.nuget.org/ver3-preview/index.json")