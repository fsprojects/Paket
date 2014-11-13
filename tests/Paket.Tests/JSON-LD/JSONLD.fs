module Paket.JSONLDSpecs

open Paket
open NUnit.Framework
open FsUnit
open System.IO

open Paket.NuGetV3

let parse fileName =
    File.ReadAllText fileName
    |> getJSONLDDetails
    |> Seq.toList

[<Test>]
let ``can extract all versions from Rx-Platformservice.json``() = 
    parse "JSON-LD/Rx-PlatformServices.json" 
    |> shouldEqual 
           [ "2.0.20304-beta"; "2.0.20612-rc"; "2.0.20622-rc"; "2.0.20814"; "2.0.20823"; "2.0.21030"; "2.0.21103"; 
             "2.0.21114"; "2.1.30204"; "2.1.30214"; "2.2.0-beta"; "2.2.0"; "2.2.1-beta"; "2.2.1"; "2.2.2"; "2.2.3"; 
             "2.2.4"; "2.2.5"; "2.3.0" ]

let rootJSON = """{
 "version": "3.0.0-preview.1",
 "resources": [
  {
   "@id": "https://preview-search.nuget.org/search/query",
   "@type": "SearchQueryService"
  },
  {
   "@id": "https://preview-search.nuget.org/search/autocomplete",
   "@type": "SearchAutocompleteService"
  },
  {
   "@id": "http://api-metrics.nuget.org/DownloadEvent",
   "@type": "MetricsService"
  },
  {
   "@id": "https://az320820.vo.msecnd.net/registrations-0/",
   "@type": "RegistrationsBaseUrl"
  },
  {
   "@id": "http://preview.nuget.org/ver3-ctp1/islatest/segment_index.json",
   "@type": "LatestVersionsList"
  },
  {
   "@id": "http://preview.nuget.org/ver3-ctp1/islateststable/segment_index.json",
   "@type": "LatestStableVersionsList"
  },
  {
   "@id": "http://preview.nuget.org/ver3-ctp1/allversions/segment_index.json",
   "@type": "AllVersionsList"
  },
  {
   "@id": "http://www.nuget.org/api/v2",
   "@type": "LegacyGallery"
  }
 ],
 "@context": {
  "@vocab": "http://schema.nuget.org/services#"
 }
}"""

[<Test>]
let ``can extract search service``() = 
    rootJSON
    |> getSearchAutocompleteService
    |> shouldEqual (Some "https://preview-search.nuget.org/search/autocomplete")