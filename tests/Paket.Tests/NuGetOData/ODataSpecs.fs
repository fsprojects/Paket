module Paket.ODataSpecs

open Paket
open NUnit.Framework
open FsUnit
open System.IO
open Paket.Nuget

let fakeUrl = "http://doesntmatter"

let parse fileName =
    File.ReadAllText fileName
    |> getODataDetails fakeUrl

[<Test>]
let ``can detect explicit dependencies for Fantomas``() = 
    parse "NuGetOData/Fantomas.xml"
    |> shouldEqual 
        { Name = "Fantomas"
          DownloadUrl = "http://www.nuget.org/api/v2/package/Fantomas/1.6.0"
          Dependencies = ["FSharp.Compiler.Service",DependenciesFileParser.parseVersionRequirement(">= 0.0.73"), []]
          SourceUrl = fakeUrl }


[<Test>]
let ``can detect explicit dependencies for Fleece``() = 
    parse "NuGetOData/Fleece.xml"
    |> shouldEqual 
        { Name = "Fleece"
          DownloadUrl = "http://www.nuget.org/api/v2/package/Fleece/0.4.0"
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
        { Name = "ReadOnlyCollectionExtensions"
          DownloadUrl = "http://www.nuget.org/api/v2/package/ReadOnlyCollectionExtensions/1.2.0"
          Dependencies = 
            ["LinqBridge",DependenciesFileParser.parseVersionRequirement(">= 1.3.0"), []
             "ReadOnlyCollectionInterfaces",DependenciesFileParser.parseVersionRequirement("1.0.0"), []
             "ReadOnlyCollectionInterfaces",DependenciesFileParser.parseVersionRequirement("1.0.0"), []
             "ReadOnlyCollectionInterfaces",DependenciesFileParser.parseVersionRequirement("1.0.0"), []]
          SourceUrl = fakeUrl }