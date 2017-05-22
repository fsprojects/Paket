module Paket.ODataSpecs

open Paket
open NUnit.Framework
open FsUnit
open System.IO
open Paket.NuGetV2

open Paket.NuGetV3
open Paket.Requirements
open Paket.TestHelpers
open Domain

let fakeUrl = "http://doesntmatter"

let parse fileName =
    System.Environment.CurrentDirectory <- Path.GetDirectoryName __SOURCE_DIRECTORY__
    parseODataDetails("tenp",fakeUrl,PackageName "package",SemVer.Parse "0",File.ReadAllText fileName)

[<Test>]
let ``can detect explicit dependencies for Fantomas``() = 
    parse "NuGetOData/Fantomas.xml"
    |> shouldEqual 
        { PackageName = "Fantomas"
          DownloadUrl = "http://www.nuget.org/api/v2/package/Fantomas/1.6.0"
          Dependencies = [PackageName "FSharp.Compiler.Service",DependenciesFileParser.parseVersionRequirement(">= 0.0.73"), makeOrList []]
          Unlisted = false
          LicenseUrl = "http://github.com/dungpa/fantomas/blob/master/LICENSE.md"
          CacheVersion = NuGet.NuGetPackageCache.CurrentCacheVersion
          Version = "1.6.0"
          SourceUrl = fakeUrl }

[<Test>]
let ``can detect explicit dependencies for Rx-PlaformServices``() = 
    parse "NuGetOData/Rx-PlatformServices.xml"
    |> shouldEqual 
        { PackageName = "Rx-PlatformServices"
          DownloadUrl = "https://www.nuget.org/api/v2/package/Rx-PlatformServices/2.3.0"
          Dependencies = 
                [PackageName "Rx-Interfaces",DependenciesFileParser.parseVersionRequirement(">= 2.2"), makeOrList []
                 PackageName "Rx-Core",DependenciesFileParser.parseVersionRequirement(">= 2.2"), makeOrList []]
          Unlisted = true
          LicenseUrl = "http://go.microsoft.com/fwlink/?LinkID=261272"
          Version = "2.3.0"
          CacheVersion = NuGet.NuGetPackageCache.CurrentCacheVersion
          SourceUrl = fakeUrl }

[<Test>]
let ``can detect explicit dependencies for EasyNetQ``() = 
    parse "NuGetOData/EasyNetQ.xml"
    |> shouldEqual 
        { PackageName = "EasyNetQ"
          DownloadUrl = "https://www.nuget.org/api/v2/package/EasyNetQ/0.40.3.352"
          Dependencies = 
                [PackageName "RabbitMQ.Client",DependenciesFileParser.parseVersionRequirement(">= 3.4.3"),makeOrList []]
          Unlisted = false
          LicenseUrl = "https://github.com/mikehadlow/EasyNetQ/blob/master/licence.txt"
          CacheVersion = NuGet.NuGetPackageCache.CurrentCacheVersion
          Version = "0.40.3.352"
          SourceUrl = fakeUrl }

[<Test>]
let ``can detect explicit dependencies for Fleece``() = 
    parse "NuGetOData/Fleece.xml"
    |> shouldEqual 
        { PackageName = "Fleece"
          DownloadUrl = "http://www.nuget.org/api/v2/package/Fleece/0.4.0"
          Unlisted = false
          CacheVersion = NuGet.NuGetPackageCache.CurrentCacheVersion
          LicenseUrl = "https://raw.github.com/mausch/Fleece/master/LICENSE"
          Version = "0.4.0"
          Dependencies = 
            [PackageName "FSharpPlus",DependenciesFileParser.parseVersionRequirement(">= 0.0.4"),makeOrList []
             PackageName "ReadOnlyCollectionInterfaces",DependenciesFileParser.parseVersionRequirement("1.0.0"),makeOrList []
             PackageName "ReadOnlyCollectionExtensions",DependenciesFileParser.parseVersionRequirement(">= 1.2.0"),makeOrList []
             PackageName "System.Json",DependenciesFileParser.parseVersionRequirement(">= 4.0.20126.16343"),makeOrList []]
          SourceUrl = fakeUrl }

[<Test>]
let ``can detect explicit dependencies for ReadOnlyCollectionExtensions``() = 
    let item = parse "NuGetOData/ReadOnlyCollectionExtensions.xml"
    item
    |> shouldEqual 
        { PackageName = "ReadOnlyCollectionExtensions"
          DownloadUrl = "http://www.nuget.org/api/v2/package/ReadOnlyCollectionExtensions/1.2.0"
          Unlisted = false
          CacheVersion = NuGet.NuGetPackageCache.CurrentCacheVersion
          LicenseUrl = "https://github.com/mausch/ReadOnlyCollections/blob/master/license.txt"
          Version = "1.2.0"
          Dependencies = 
            [PackageName "LinqBridge",DependenciesFileParser.parseVersionRequirement(">= 1.3.0"),
              makeOrList [FrameworkRestriction.Between (DotNetFramework(FrameworkVersion.V2), DotNetFramework(FrameworkVersion.V3_5))]
             PackageName "ReadOnlyCollectionInterfaces",DependenciesFileParser.parseVersionRequirement("1.0.0"), 
              makeOrList
               [FrameworkRestriction.AtLeast(DotNetFramework(FrameworkVersion.V2))]]
          SourceUrl = fakeUrl }

[<Test>]
let ``can detect explicit dependencies for Math.Numerics``() = 
    parse "NuGetOData/Math.Numerics.xml"
    |> shouldEqual 
        { PackageName = "MathNet.Numerics"
          DownloadUrl = "http://www.nuget.org/api/v2/package/MathNet.Numerics/3.3.0"
          Unlisted = false
          Version = "3.3.0"
          LicenseUrl = "http://numerics.mathdotnet.com/docs/License.html"
          CacheVersion = NuGet.NuGetPackageCache.CurrentCacheVersion
          Dependencies = 
            [PackageName "TaskParallelLibrary",DependenciesFileParser.parseVersionRequirement(">= 1.0.2856"), makeOrList [FrameworkRestriction.Between(DotNetFramework(FrameworkVersion.V3_5), DotNetFramework(FrameworkVersion.V4))]]
          SourceUrl = fakeUrl }

[<Test>]
let ``can detect explicit dependencies for Math.Numerics.FSharp``() = 
    (parse "NuGetOData/Math.Numerics.FSharp.xml").Dependencies |> Seq.head
    |> shouldEqual 
        (PackageName "MathNet.Numerics",
         DependenciesFileParser.parseVersionRequirement("3.3.0"),makeOrList [])

[<Test>]
let ``can calculate v3 path``() = 
    calculateNuGet3Path "https://www.nuget.org/api/v2" |> shouldEqual (Some "https://api.nuget.org/v3/index.json")
    calculateNuGet3Path "http://www.nuget.org/api/v2" |> shouldEqual (Some "http://api.nuget.org/v3/index.json")

[<Test>]
let ``can detect explicit dependencies for Microsoft.AspNet.WebApi.Client``() = 
    let odata = parse "NuGetOData/Microsoft.AspNet.WebApi.Client.xml"
    odata.PackageName |> shouldEqual "Microsoft.AspNet.WebApi.Client"
    odata.DownloadUrl |> shouldEqual"https://www.nuget.org/api/v2/package/Microsoft.AspNet.WebApi.Client/5.2.3"
    let dependencies = odata.Dependencies |> Array.ofList
    dependencies.[0] |> shouldEqual 
        (PackageName "Newtonsoft.Json", DependenciesFileParser.parseVersionRequirement(">= 6.0.4"), 
            makeOrList [getPortableRestriction("portable-net45+win8+wp8+wp81+wpa81"); FrameworkRestriction.AtLeast(DotNetFramework(FrameworkVersion.V4_5))])
    dependencies.[1] |> shouldEqual
        (PackageName "Microsoft.Net.Http", DependenciesFileParser.parseVersionRequirement(">= 2.2.22"), 
            makeOrList [getPortableRestriction("portable-net45+win8+wp8+wp81+wpa81")])

[<Test>]
let ``can detect explicit dependencies for WindowsAzure.Storage``() = 
    let odata = parse "NuGetOData/WindowsAzure.Storage.xml"
    odata.PackageName |> shouldEqual "WindowsAzure.Storage"
    odata.DownloadUrl |> shouldEqual"https://www.nuget.org/api/v2/package/WindowsAzure.Storage/4.4.1-preview"
    let dependencies = odata.Dependencies |> Array.ofList
    dependencies.[0] |> shouldEqual 
        (PackageName "Microsoft.Data.OData", DependenciesFileParser.parseVersionRequirement(">= 5.6.3"), 
           makeOrList [FrameworkRestriction.AtLeast(DNXCore(FrameworkVersion.V5_0))])

    let vr,pr = 
        match DependenciesFileParser.parseVersionRequirement(">= 4.0.0-beta-22231") with
        | VersionRequirement(vr,pr) -> vr,pr

    dependencies.[18] |> shouldEqual 
        (PackageName "System.Net.Http", VersionRequirement(vr,PreReleaseStatus.All), 
           makeOrList [FrameworkRestriction.AtLeast(DNXCore(FrameworkVersion.V5_0))])

    dependencies.[44] |> shouldEqual 
        (PackageName "Newtonsoft.Json", DependenciesFileParser.parseVersionRequirement(">= 6.0.8"), 
            makeOrList [FrameworkRestriction.AtLeast(WindowsPhone WindowsPhoneVersion.V8); FrameworkRestriction.AtLeast(DotNetFramework(FrameworkVersion.V4))])

[<Test>]
let ``can ignore unknown frameworks``() = 
    parse "NuGetOData/BenchmarkDotNet-UnknownFramework.xml"
    |> shouldEqual 
        { PackageName = "BenchmarkDotNet"
          DownloadUrl = "https://www.nuget.org/api/v2/package/BenchmarkDotNet/0.10.1"
          Dependencies =
            [
                PackageName "BenchmarkDotNet.Toolchains.Roslyn",
                DependenciesFileParser.parseVersionRequirement(">= 0.10.1"),
                makeOrList [FrameworkRestriction.AtLeast (DotNetFramework FrameworkVersion.V4_5)]
            ]
          Unlisted = false
          LicenseUrl = "https://github.com/dotnet/BenchmarkDotNet/blob/master/LICENSE.md"
          CacheVersion = NuGet.NuGetPackageCache.CurrentCacheVersion
          Version = "0.10.1"
          SourceUrl = fakeUrl }