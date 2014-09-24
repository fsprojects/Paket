module Paket.NugetVersionRangeSpecs

open Paket
open NUnit.Framework
open FsUnit

[<Test>]
let ``can detect latest version``() = 
    Nuget.parseVersionRange "" |> shouldEqual VersionRequirement.NoRestriction

let parseRange text = Nuget.parseVersionRange(text).Range

[<Test>]
let ``can detect specific version``() = 
    parseRange "[2.2]" |> shouldEqual (VersionRange.Specific (SemVer.parse "2.2"))
    parseRange "[1.2]" |> shouldEqual (VersionRange.Specific (SemVer.parse "1.2"))

[<Test>]
let ``can detect minimum version``() = 
    parseRange "2.2" |> shouldEqual (VersionRange.Minimum (SemVer.parse "2.2"))
    parseRange"1.2" |> shouldEqual (VersionRange.Minimum (SemVer.parse "1.2"))

[<Test>]
let ``can detect greater than version``() = 
    parseRange "(2.2,)" |> shouldEqual (VersionRange.GreaterThan (SemVer.parse "2.2"))
    parseRange "(1.2,)" |> shouldEqual (VersionRange.GreaterThan (SemVer.parse "1.2"))

[<Test>]
let ``can detect maximum version``() = 
    parseRange "(,2.2]" |> shouldEqual (VersionRange.Maximum (SemVer.parse "2.2"))
    parseRange "(,1.2]" |> shouldEqual (VersionRange.Maximum (SemVer.parse "1.2"))

[<Test>]
let ``can detect less than version``() = 
    parseRange "(,2.2)" |> shouldEqual (VersionRange.LessThan (SemVer.parse "2.2"))
    parseRange "(,1.2)" |> shouldEqual (VersionRange.LessThan (SemVer.parse "1.2"))

[<Test>]
let ``can detect range version``() = 
    parseRange "(2.2,3)" 
        |> shouldEqual (VersionRange.Range(Excluding, SemVer.parse "2.2", SemVer.parse "3", Excluding))
    parseRange "(2.2,3]" 
        |> shouldEqual (VersionRange.Range(Excluding, SemVer.parse "2.2", SemVer.parse "3", Including))
    parseRange "[2.2,3)" 
        |> shouldEqual (VersionRange.Range(Including, SemVer.parse "2.2", SemVer.parse "3", Excluding))
    parseRange "[2.2,3]" 
        |> shouldEqual (VersionRange.Range(Including, SemVer.parse "2.2", SemVer.parse "3", Including))

        
[<Test>]
let ``can detect "null" version``() = 
    Nuget.parseVersionRange "null" |> shouldEqual (DependenciesFileParser.parseVersionRange ">= 0")