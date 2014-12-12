module Paket.NugetVersionRangeParserSpecs

open Paket
open NUnit.Framework
open FsUnit

[<Test>]
let ``can detect latest version``() = 
    NugetVersionRangeParser.parse "" |> shouldEqual VersionRequirement.AllReleases

let parseRange text = NugetVersionRangeParser.parse(text).Range

[<Test>]
let ``can detect specific version``() = 
    parseRange "[2.2]" |> shouldEqual (VersionRange.Specific (SemVer.Parse "2.2"))
    parseRange "[1.2]" |> shouldEqual (VersionRange.Specific (SemVer.Parse "1.2"))

[<Test>]
let ``can detect minimum version``() = 
    parseRange "2.2" |> shouldEqual (VersionRange.Minimum (SemVer.Parse "2.2"))
    parseRange"1.2" |> shouldEqual (VersionRange.Minimum (SemVer.Parse "1.2"))

[<Test>]
let ``can detect greater than version``() = 
    parseRange "(2.2,)" |> shouldEqual (VersionRange.GreaterThan (SemVer.Parse "2.2"))
    parseRange "(1.2,)" |> shouldEqual (VersionRange.GreaterThan (SemVer.Parse "1.2"))

[<Test>]
let ``can detect maximum version``() = 
    parseRange "(,2.2]" |> shouldEqual (VersionRange.Maximum (SemVer.Parse "2.2"))
    parseRange "(,1.2]" |> shouldEqual (VersionRange.Maximum (SemVer.Parse "1.2"))

[<Test>]
let ``can detect less than version``() = 
    parseRange "(,2.2)" |> shouldEqual (VersionRange.LessThan (SemVer.Parse "2.2"))
    parseRange "(,1.2)" |> shouldEqual (VersionRange.LessThan (SemVer.Parse "1.2"))

[<Test>]
let ``can detect range version``() = 
    parseRange "(2.2,3)" 
        |> shouldEqual (VersionRange.Range(VersionRangeBound.Excluding, SemVer.Parse "2.2", SemVer.Parse "3", VersionRangeBound.Excluding))
    parseRange "(2.2,3]" 
        |> shouldEqual (VersionRange.Range(VersionRangeBound.Excluding, SemVer.Parse "2.2", SemVer.Parse "3", VersionRangeBound.Including))
    parseRange "[2.2,3)" 
        |> shouldEqual (VersionRange.Range(VersionRangeBound.Including, SemVer.Parse "2.2", SemVer.Parse "3", VersionRangeBound.Excluding))
    parseRange "[2.2,3]" 
        |> shouldEqual (VersionRange.Range(VersionRangeBound.Including, SemVer.Parse "2.2", SemVer.Parse "3", VersionRangeBound.Including))

        
[<Test>]
let ``can detect "null" version``() = 
    NugetVersionRangeParser.parse "null" 
    |> shouldEqual (DependenciesFileParser.parseVersionRequirement ">= 0")