module Paket.ODataSpecs

open Paket
open NUnit.Framework
open FsUnit
open System.IO
open Pri.LongPath
open Paket.NuGetV2

open Paket.NuGetV3
open Paket.Requirements
open Paket.TestHelpers
open Domain
open Paket.NuGetCache
open System.Xml

let fakeUrl = "http://doesntmatter"

let parseList fileName =
    System.Environment.CurrentDirectory <- Path.GetDirectoryName __SOURCE_DIRECTORY__
    let doc = XmlDocument()
    doc.Load (fileName:string)
    parseODataListDetails("tenp",fakeUrl,PackageName "package",SemVer.Parse "0",doc)
let parseEntry fileName =
    System.Environment.CurrentDirectory <- Path.GetDirectoryName __SOURCE_DIRECTORY__
    let doc = XmlDocument()
    doc.Load (fileName:string)
    parseODataEntryDetails("tenp",fakeUrl,PackageName "package",SemVer.Parse "0",doc)

[<Test>]
let ``can detect explicit dependencies for Fantomas``() = 
    parseEntry "NuGetOData/Fantomas.xml"
    |> shouldEqual 
        { PackageName = "Fantomas"
          DownloadUrl = "http://www.nuget.org/api/v2/package/Fantomas/1.6.0"
          SerializedDependencies = [PackageName "FSharp.Compiler.Service",DependenciesFileParser.parseVersionRequirement(">= 0.0.73"), "true"]
          Unlisted = false
          LicenseUrl = "http://github.com/dungpa/fantomas/blob/master/LICENSE.md"
          CacheVersion = NuGet.NuGetPackageCache.CurrentCacheVersion
          Version = "1.6.0"
          SourceUrl = fakeUrl }

[<Test>]
let ``can parse an empty odata result``() =
    parseList "NuGetOData/EmptyFeedList.xml"
    |> shouldEqual ODataSearchResult.EmptyResult

[<Test>]
let ``can detect explicit dependencies for Rx-PlaformServices``() = 
    parseList "NuGetOData/Rx-PlatformServices.xml"
    |> shouldEqual
       ({ PackageName = "Rx-PlatformServices"
          DownloadUrl = "https://www.nuget.org/api/v2/package/Rx-PlatformServices/2.3.0"
          SerializedDependencies = 
                [PackageName "Rx-Interfaces",DependenciesFileParser.parseVersionRequirement(">= 2.2"), "true"
                 PackageName "Rx-Core",DependenciesFileParser.parseVersionRequirement(">= 2.2"), "true"]
          Unlisted = true
          LicenseUrl = "http://go.microsoft.com/fwlink/?LinkID=261272"
          Version = "2.3.0"
          CacheVersion = NuGet.NuGetPackageCache.CurrentCacheVersion
          SourceUrl = fakeUrl }
        |> ODataSearchResult.Match)

[<Test>]
let ``can detect explicit dependencies for EasyNetQ``() = 
    parseList "NuGetOData/EasyNetQ.xml"
    |> shouldEqual 
       ({ PackageName = "EasyNetQ"
          DownloadUrl = "https://www.nuget.org/api/v2/package/EasyNetQ/0.40.3.352"
          SerializedDependencies = 
                [PackageName "RabbitMQ.Client",DependenciesFileParser.parseVersionRequirement(">= 3.4.3"),"true"]
          Unlisted = false
          LicenseUrl = "https://github.com/mikehadlow/EasyNetQ/blob/master/licence.txt"
          CacheVersion = NuGet.NuGetPackageCache.CurrentCacheVersion
          Version = "0.40.3.352"
          SourceUrl = fakeUrl }
        |> ODataSearchResult.Match)

[<Test>]
let ``can detect explicit dependencies for Fleece``() = 
    parseEntry "NuGetOData/Fleece.xml"
    |> shouldEqual 
        { PackageName = "Fleece"
          DownloadUrl = "http://www.nuget.org/api/v2/package/Fleece/0.4.0"
          Unlisted = false
          CacheVersion = NuGet.NuGetPackageCache.CurrentCacheVersion
          LicenseUrl = "https://raw.github.com/mausch/Fleece/master/LICENSE"
          Version = "0.4.0"
          SerializedDependencies = 
            [PackageName "FSharpPlus",DependenciesFileParser.parseVersionRequirement(">= 0.0.4"),"true"
             PackageName "ReadOnlyCollectionInterfaces",DependenciesFileParser.parseVersionRequirement("1.0.0"),"true"
             PackageName "ReadOnlyCollectionExtensions",DependenciesFileParser.parseVersionRequirement(">= 1.2.0"),"true"
             PackageName "System.Json",DependenciesFileParser.parseVersionRequirement(">= 4.0.20126.16343"),"true"]
          SourceUrl = fakeUrl }

[<Test>]
let ``can detect explicit dependencies for ReadOnlyCollectionExtensions``() = 
    let item = parseEntry "NuGetOData/ReadOnlyCollectionExtensions.xml"
    item
    |> shouldEqual 
        { PackageName = "ReadOnlyCollectionExtensions"
          DownloadUrl = "http://www.nuget.org/api/v2/package/ReadOnlyCollectionExtensions/1.2.0"
          Unlisted = false
          CacheVersion = NuGet.NuGetPackageCache.CurrentCacheVersion
          LicenseUrl = "https://github.com/mausch/ReadOnlyCollections/blob/master/license.txt"
          Version = "1.2.0"
          SerializedDependencies = 
            [PackageName "LinqBridge",DependenciesFileParser.parseVersionRequirement(">= 1.3.0"), "&& (>= net20) (< net35)"
             PackageName "ReadOnlyCollectionInterfaces",DependenciesFileParser.parseVersionRequirement("1.0.0"), ">= net20"]
          SourceUrl = fakeUrl }

[<Test>]
let ``can detect explicit dependencies for Math.Numerics``() = 
    parseList "NuGetOData/Math.Numerics.xml"
    |> shouldEqual 
       ({ PackageName = "MathNet.Numerics"
          DownloadUrl = "http://www.nuget.org/api/v2/package/MathNet.Numerics/3.3.0"
          Unlisted = false
          Version = "3.3.0"
          LicenseUrl = "http://numerics.mathdotnet.com/docs/License.html"
          CacheVersion = NuGet.NuGetPackageCache.CurrentCacheVersion
          SerializedDependencies = 
            [PackageName "TaskParallelLibrary",DependenciesFileParser.parseVersionRequirement(">= 1.0.2856"), "&& (>= net35) (< net40)" ]
          SourceUrl = fakeUrl }
        |> ODataSearchResult.Match)

[<Test>]
let ``can detect explicit dependencies for Math.Numerics.FSharp``() = 
    (parseList "NuGetOData/Math.Numerics.FSharp.xml") |> ODataSearchResult.get |> NuGet.NuGetPackageCache.getDependencies |> Seq.head
    |> shouldEqual 
        (PackageName "MathNet.Numerics",
         DependenciesFileParser.parseVersionRequirement("3.3.0"),makeOrList [])

[<Test>]
let ``can calculate v3 path``() = 
    calculateNuGet3Path "https://www.nuget.org/api/v2" |> shouldEqual (Some "https://api.nuget.org/v3/index.json")
    calculateNuGet3Path "http://www.nuget.org/api/v2" |> shouldEqual (Some "http://api.nuget.org/v3/index.json")

[<Test>]
let ``can detect explicit dependencies for Microsoft.AspNet.WebApi.Client``() = 
    let odata = parseList "NuGetOData/Microsoft.AspNet.WebApi.Client.xml" |> ODataSearchResult.get
    odata.PackageName |> shouldEqual "Microsoft.AspNet.WebApi.Client"
    odata.DownloadUrl |> shouldEqual"https://www.nuget.org/api/v2/package/Microsoft.AspNet.WebApi.Client/5.2.3"
    let dependencies = odata|> NuGet.NuGetPackageCache.getDependencies |> Array.ofList
    dependencies.[0] |> shouldEqual 
        (PackageName "Newtonsoft.Json", DependenciesFileParser.parseVersionRequirement(">= 6.0.4"), 
            makeOrList [getPortableRestriction("portable-net45+win8+wp8+wp81+wpa81"); FrameworkRestriction.AtLeast(DotNetFramework(FrameworkVersion.V4_5))])
    dependencies.[1] |> shouldEqual
        (PackageName "Microsoft.Net.Http", DependenciesFileParser.parseVersionRequirement(">= 2.2.22"), 
            FrameworkRestriction.And [getPortableRestriction("portable-net45+win8+wp8+wp81+wpa81"); FrameworkRestriction.NotAtLeast(DotNetFramework(FrameworkVersion.V4_5))]
            |> ExplicitRestriction)

[<Test>]
let ``can detect explicit dependencies for WindowsAzure.Storage``() = 
    let odata = parseList "NuGetOData/WindowsAzure.Storage.xml" |> ODataSearchResult.get
    odata.PackageName |> shouldEqual "WindowsAzure.Storage"
    odata.DownloadUrl |> shouldEqual"https://www.nuget.org/api/v2/package/WindowsAzure.Storage/4.4.1-preview"
    let dependencies = odata|> NuGet.NuGetPackageCache.getDependencies |> Array.ofList
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
    let parsed = parseList "NuGetOData/BenchmarkDotNet-UnknownFramework.xml" |> ODataSearchResult.get
    parsed
    |> shouldEqual 
        { PackageName = "BenchmarkDotNet"
          DownloadUrl = "https://www.nuget.org/api/v2/package/BenchmarkDotNet/0.10.1"
          SerializedDependencies =
            [
                PackageName "BenchmarkDotNet.Toolchains.Roslyn",
                DependenciesFileParser.parseVersionRequirement(">= 0.10.1"),
                ">= net45"
            ]
          Unlisted = false
          LicenseUrl = "https://github.com/dotnet/BenchmarkDotNet/blob/master/LICENSE.md"
          CacheVersion = NuGet.NuGetPackageCache.CurrentCacheVersion
          Version = "0.10.1"
          SourceUrl = fakeUrl }