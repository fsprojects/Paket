module Paket.ODataSpecs

open Paket
open NUnit.Framework
open FsUnit
open System.IO
open Paket.Nuget

let fakeUrl = "http://doesntmatter"

[<Test>]
let ``can detect explicit dependencies for Fantomas``() = 
    File.ReadAllText("NuGetOdata/Fantomas.xml")
    |> getODataDetails fakeUrl
    |> shouldEqual 
        { Name = "Fantomas"
          DownloadUrl = "http://www.nuget.org/api/v2/package/Fantomas/1.6.0"
          Dependencies = ["FSharp.Compiler.Service",DependenciesFileParser.parseVersionRequirement(">= 0.0.73")]
          SourceUrl = fakeUrl }