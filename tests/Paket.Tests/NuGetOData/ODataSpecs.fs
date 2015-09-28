module Paket.ODataSpecs

open Paket
open NUnit.Framework
open FsUnit
open System.IO
open Paket.NuGetV2

open Paket.NuGetV3
open Paket.Requirements
open Domain

let fakeUrl = "http://doesntmatter"

let parse fileName =
    parseODataDetails(fakeUrl,PackageName "package",SemVer.Parse "0",File.ReadAllText fileName)

[<Test>]
let ``can detect explicit dependencies for Fantomas``() = 
    parse "NuGetOData/Fantomas.xml"
    |> shouldEqual 
        { PackageName = "Fantomas"
          DownloadUrl = "http://www.nuget.org/api/v2/package/Fantomas/1.6.0"
          Dependencies = [PackageName "FSharp.Compiler.Service",DependenciesFileParser.parseVersionRequirement(">= 0.0.73"), []]
          Unlisted = false
          LicenseUrl = "http://github.com/dungpa/fantomas/blob/master/LICENSE.md"
          CacheVersion = NugetPackageCache.CurrentCacheVersion
          SourceUrl = fakeUrl }

[<Test>]
let ``can detect explicit dependencies for Rx-PlaformServices``() = 
    parse "NuGetOData/Rx-PlatformServices.xml"
    |> shouldEqual 
        { PackageName = "Rx-PlatformServices"
          DownloadUrl = "https://www.nuget.org/api/v2/package/Rx-PlatformServices/2.3.0"
          Dependencies = 
                [PackageName "Rx-Interfaces",DependenciesFileParser.parseVersionRequirement(">= 2.2"), []
                 PackageName "Rx-Core",DependenciesFileParser.parseVersionRequirement(">= 2.2"), []]
          Unlisted = true
          LicenseUrl = "http://go.microsoft.com/fwlink/?LinkID=261272"
          CacheVersion = NugetPackageCache.CurrentCacheVersion
          SourceUrl = fakeUrl }

[<Test>]
let ``can detect explicit dependencies for EasyNetQ``() = 
    parse "NuGetOData/EasyNetQ.xml"
    |> shouldEqual 
        { PackageName = "EasyNetQ"
          DownloadUrl = "https://www.nuget.org/api/v2/package/EasyNetQ/0.40.3.352"
          Dependencies = 
                [PackageName "RabbitMQ.Client",DependenciesFileParser.parseVersionRequirement(">= 3.4.3"), []]
          Unlisted = false
          LicenseUrl = "https://github.com/mikehadlow/EasyNetQ/blob/master/licence.txt"
          CacheVersion = NugetPackageCache.CurrentCacheVersion
          SourceUrl = fakeUrl }

[<Test>]
let ``can detect explicit dependencies for Fleece``() = 
    parse "NuGetOData/Fleece.xml"
    |> shouldEqual 
        { PackageName = "Fleece"
          DownloadUrl = "http://www.nuget.org/api/v2/package/Fleece/0.4.0"
          Unlisted = false
          CacheVersion = NugetPackageCache.CurrentCacheVersion
          LicenseUrl = "https://raw.github.com/mausch/Fleece/master/LICENSE"
          Dependencies = 
            [PackageName "FSharpPlus",DependenciesFileParser.parseVersionRequirement(">= 0.0.4"), []
             PackageName "ReadOnlyCollectionInterfaces",DependenciesFileParser.parseVersionRequirement("1.0.0"), []
             PackageName "ReadOnlyCollectionExtensions",DependenciesFileParser.parseVersionRequirement(">= 1.2.0"), []
             PackageName "System.Json",DependenciesFileParser.parseVersionRequirement(">= 4.0.20126.16343"), []]
          SourceUrl = fakeUrl }

[<Test>]
let ``can detect explicit dependencies for ReadOnlyCollectionExtensions``() = 
    parse "NuGetOData/ReadOnlyCollectionExtensions.xml"
    |> shouldEqual 
        { PackageName = "ReadOnlyCollectionExtensions"
          DownloadUrl = "http://www.nuget.org/api/v2/package/ReadOnlyCollectionExtensions/1.2.0"
          Unlisted = false
          CacheVersion = NugetPackageCache.CurrentCacheVersion
          LicenseUrl = "https://github.com/mausch/ReadOnlyCollections/blob/master/license.txt"
          Dependencies = 
            [PackageName "LinqBridge",DependenciesFileParser.parseVersionRequirement(">= 1.3.0"), [FrameworkRestriction.Exactly (DotNetFramework(FrameworkVersion.V2))]
             PackageName "ReadOnlyCollectionInterfaces",DependenciesFileParser.parseVersionRequirement("1.0.0"), 
               [FrameworkRestriction.Exactly(DotNetFramework(FrameworkVersion.V2))
                FrameworkRestriction.Exactly(DotNetFramework(FrameworkVersion.V3_5))
                FrameworkRestriction.AtLeast(DotNetFramework(FrameworkVersion.V4_Client))]]
          SourceUrl = fakeUrl }

[<Test>]
let ``can detect explicit dependencies for Math.Numerics``() = 
    parse "NuGetOData/Math.Numerics.xml"
    |> shouldEqual 
        { PackageName = "MathNet.Numerics"
          DownloadUrl = "http://www.nuget.org/api/v2/package/MathNet.Numerics/3.3.0"
          Unlisted = false
          LicenseUrl = "http://numerics.mathdotnet.com/docs/License.html"
          CacheVersion = NugetPackageCache.CurrentCacheVersion
          Dependencies = 
            [PackageName "TaskParallelLibrary",DependenciesFileParser.parseVersionRequirement(">= 1.0.2856"), [FrameworkRestriction.Exactly (DotNetFramework(FrameworkVersion.V3_5))]]
          SourceUrl = fakeUrl }

[<Test>]
let ``can detect explicit dependencies for Math.Numerics.FSharp``() = 
    (parse "NuGetOData/Math.Numerics.FSharp.xml").Dependencies |> Seq.head
    |> shouldEqual 
        (PackageName "MathNet.Numerics",
         DependenciesFileParser.parseVersionRequirement("3.3.0"),[])

[<Test>]
let ``can calculate v3 path``() = 
    calculateNuGet3Path "https://nuget.org/api/v2" |> shouldEqual (Some "https://api.nuget.org/v3/index.json")
    calculateNuGet3Path "http://nuget.org/api/v2" |> shouldEqual (Some "http://api.nuget.org/v3/index.json")

[<Test>]
let ``can read all versions from single page with multiple entries``() =
    let getUrlContentsStub _ _ = async { return File.ReadAllText "NuGetOData/NUnit.xml" }
    
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


[<Test>]
let ``can detect explicit dependencies for Microsoft.AspNet.WebApi.Client``() = 
    let odata = parse "NuGetOData/Microsoft.AspNet.WebApi.Client.xml"
    odata.PackageName |> shouldEqual "Microsoft.AspNet.WebApi.Client"
    odata.DownloadUrl |> shouldEqual"https://www.nuget.org/api/v2/package/Microsoft.AspNet.WebApi.Client/5.2.3"
    let dependencies = odata.Dependencies |> Array.ofList
    dependencies.[0] |> shouldEqual 
        (PackageName "Newtonsoft.Json", DependenciesFileParser.parseVersionRequirement(">= 6.0.4"), 
                [FrameworkRestriction.Portable("portable-wp80+win+net45+wp81+wpa81"); FrameworkRestriction.AtLeast(DotNetFramework(FrameworkVersion.V4_5))])
    dependencies.[1] |> shouldEqual
        (PackageName "Microsoft.Net.Http", DependenciesFileParser.parseVersionRequirement(">= 2.2.22"), 
                [FrameworkRestriction.Portable("portable-wp80+win+net45+wp81+wpa81")])

[<Test>]
let ``can detect explicit dependencies for WindowsAzure.Storage``() = 
    let odata = parse "NuGetOData/WindowsAzure.Storage.xml"
    odata.PackageName |> shouldEqual "WindowsAzure.Storage"
    odata.DownloadUrl |> shouldEqual"https://www.nuget.org/api/v2/package/WindowsAzure.Storage/4.4.1-preview"
    let dependencies = odata.Dependencies |> Array.ofList
    dependencies.[0] |> shouldEqual 
        (PackageName "Microsoft.Data.OData", DependenciesFileParser.parseVersionRequirement(">= 5.6.3"), 
                [FrameworkRestriction.Exactly(DNXCore(FrameworkVersion.V5_0))])

    let vr,pr = 
        match DependenciesFileParser.parseVersionRequirement(">= 4.0.0-beta-22231") with
        | VersionRequirement(vr,pr) -> vr,pr

    dependencies.[18] |> shouldEqual 
        (PackageName "System.Net.Http", VersionRequirement(vr,PreReleaseStatus.All), 
                [FrameworkRestriction.Exactly(DNXCore(FrameworkVersion.V5_0))])

    dependencies.[44] |> shouldEqual 
        (PackageName "Newtonsoft.Json", DependenciesFileParser.parseVersionRequirement(">= 6.0.8"), 
                [FrameworkRestriction.Exactly(WindowsPhoneSilverlight("v8.0")); FrameworkRestriction.AtLeast(DotNetFramework(FrameworkVersion.V4_Client))])