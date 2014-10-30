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


[<Test>]
let ``can detect explicit dependencies for Fleece``() = 
    File.ReadAllText("NuGetOdata/Fleece.xml")
    |> getODataDetails fakeUrl
    |> shouldEqual 
        { Name = "Fleece"
          DownloadUrl = "http://www.nuget.org/api/v2/package/Fleece/0.4.0"
          Dependencies = 
            ["FSharpPlus",DependenciesFileParser.parseVersionRequirement(">= 0.0.4")
             "ReadOnlyCollectionInterfaces",DependenciesFileParser.parseVersionRequirement("1.0.0")
             "ReadOnlyCollectionExtensions",DependenciesFileParser.parseVersionRequirement(">= 1.2.0")
             "System.Json",DependenciesFileParser.parseVersionRequirement(">= 4.0.20126.16343")]
          SourceUrl = fakeUrl }

[<Test>]
let ``can detect explicit dependencies for ReadOnlyCollectionExtensions``() = 
    File.ReadAllText("NuGetOdata/ReadOnlyCollectionExtensions.xml")
    |> getODataDetails fakeUrl
    |> shouldEqual 
        { Name = "ReadOnlyCollectionExtensions"
          DownloadUrl = "http://www.nuget.org/api/v2/package/ReadOnlyCollectionExtensions/1.2.0"
          Dependencies = 
            ["LinqBridge",DependenciesFileParser.parseVersionRequirement(">= 1.3.0")
             "ReadOnlyCollectionInterfaces",DependenciesFileParser.parseVersionRequirement("1.0.0")
             "ReadOnlyCollectionInterfaces",DependenciesFileParser.parseVersionRequirement("1.0.0")
             "ReadOnlyCollectionInterfaces",DependenciesFileParser.parseVersionRequirement("1.0.0")]
          SourceUrl = fakeUrl }