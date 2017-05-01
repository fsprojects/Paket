[<NUnit.Framework.TestFixture>]
[<NUnit.Framework.Category "Graph Caching">]
module Paket.GraphCachingParserTests

open System.IO
open NUnit.Framework
open FsUnit
open TestHelpers
open Paket
open Paket.Domain
open Newtonsoft.Json.Linq

[<Test>]
let ``can parse version requirements``() =
    let graphString = """{
  "http://www.nuget.org/api/v2": {
    "Packages": [
      {
        "VersionsRetrieved": "2017-05-01T23:26:05.9741447+02:00",
        "PackageName": "bootstrap",
        "Versions": [
          {
            "Version": "3.2.0",
            "PackageDetails": {
              "Name": "bootstrap",
              "DownloadLink": "http://www.nuget.org/api/v2/package/bootstrap/3.2.0",
              "LicenseUrl": "https://github.com/twbs/bootstrap/blob/master/LICENSE",
              "IsUnlisted": false,
              "Dependencies": [
                {
                  "PackageName": "jquery",
                  "VersionRequirement": ">= 1.9"
                }
              ]
            },
            "RuntimeGraphData": {}
          }
        ]
      }
    ]
  }
}"""
    let graph =
        GraphCache.readGraphJson (JObject.Parse graphString)
        |> Seq.map (fun (source, packagename, cache) -> source, (packagename, cache))
        |> Seq.groupBy fst
        |> Seq.map (fun (source, group) -> source, group |> Seq.map snd |> dict)
        |> dict
    let cache = graph.["http://www.nuget.org/api/v2"].[PackageName "bootstrap"]
    let details, runtimeGraph = cache.Details.[SemVer.Parse "3.2.0"]
    runtimeGraph |> shouldEqual (GraphCache.RuntimeGraphCache.CachedData None)
    details |> shouldNotEqual None
    let d = details.Value
    d.DirectDependencies.Count |> shouldEqual 1
    //d.DirectDependencies.[0]

[<Test>]
let ``can parse empty versions``() =
    let graphString = """{
  "http://www.nuget.org/api/v2": {
    "Packages": [
      {
        "VersionsRetrieved": "2017-05-01T23:26:05.9741447+02:00",
        "PackageName": "bootstrap",
        "Versions": [
          {
            "Version": "3.1.0",
          },
          {
            "Version": "1.1.0",
          },
          {
            "Version": "3.2.0",
            "PackageDetails": {
              "Name": "bootstrap",
              "DownloadLink": "http://www.nuget.org/api/v2/package/bootstrap/3.2.0",
              "LicenseUrl": "https://github.com/twbs/bootstrap/blob/master/LICENSE",
              "IsUnlisted": false,
              "Dependencies": [
                {
                  "PackageName": "jquery",
                  "VersionRequirement": ">= 1.9"
                }
              ]
            },
            "RuntimeGraphData": {}
          }
        ]
      }
    ]
  }
}"""
    let graph =
        GraphCache.readGraphJson (JObject.Parse graphString)
        |> Seq.map (fun (source, packagename, cache) -> source, (packagename, cache))
        |> Seq.groupBy fst
        |> Seq.map (fun (source, group) -> source, group |> Seq.map snd |> dict)
        |> dict
    let cache = graph.["http://www.nuget.org/api/v2"].[PackageName "bootstrap"]
    cache.Details.Count |> shouldEqual 3

    // TODO: Tests version ordering
[<Test>]
let ``can parse versions of runtime graph``() =
    let graphString = """{
  "http://www.nuget.org/api/v2": {
    "Packages": [
      {
        "VersionsRetrieved": "2017-05-01T23:26:05.9741447+02:00",
        "PackageName": "bootstrap",
        "Versions": [
          {
            "Version": "3.2.0",
            "PackageDetails": {
              "Name": "bootstrap",
              "DownloadLink": "http://www.nuget.org/api/v2/package/bootstrap/3.2.0",
              "LicenseUrl": "https://github.com/twbs/bootstrap/blob/master/LICENSE",
              "IsUnlisted": false,
              "Dependencies": [
                {
                  "PackageName": "jquery",
                  "VersionRequirement": ">= 1.9"
                }
              ]
            },
            "RuntimeGraphData": {
              "runtimes": {
                "any": {
                  "#import": [],
                  "System.Collections": {
                    "runtime.any.System.Collections": ">= 4.3"
                  }
                }
              }
            }
          }
        ]
      }
    ]
  }
}"""
    let graph =
        GraphCache.readGraphJson (JObject.Parse graphString)
        |> Seq.map (fun (source, packagename, cache) -> source, (packagename, cache))
        |> Seq.groupBy fst
        |> Seq.map (fun (source, group) -> source, group |> Seq.map snd |> dict)
        |> dict
    let cache = graph.["http://www.nuget.org/api/v2"].[PackageName "bootstrap"]
    cache.Details.Count |> shouldEqual 1
    let _, runtime = cache.Details.[SemVer.Parse "3.2.0"]
    runtime |> shouldNotEqual (GraphCache.RuntimeGraphCache.CachedData None)
    runtime |> shouldNotEqual (GraphCache.RuntimeGraphCache.NotJetCached)
    (**)