module Paket.NugetVersionRangeParserSpecs

open Paket
open NUnit.Framework
open FsUnit

[<Test>]
let ``can detect latest version``() = 
    VersionRequirement.Parse "" |> shouldEqual VersionRequirement.AllReleases

let parseRange text = VersionRequirement.Parse(text).Range

[<Test>]
let ``can detect specific version``() = 
    parseRange "[2.2]" |> shouldEqual (VersionRange.Specific (SemVer.Parse "2.2"))
    parseRange "[1.2]" |> shouldEqual (VersionRange.Specific (SemVer.Parse "1.2"))

[<Test>]
let ``can detect minimum version``() = 
    parseRange "2.2" |> shouldEqual (VersionRange.Minimum (SemVer.Parse "2.2"))
    parseRange "1.2" |> shouldEqual (VersionRange.Minimum (SemVer.Parse "1.2"))
    parseRange "1.0-beta" |> shouldEqual (VersionRange.Minimum(SemVer.Parse "1.0-beta"))

[<Test>]
let ``can detect minimum version for pre-releases``() = 
    VersionRequirement.Parse("1.0-beta").PreReleases |> shouldEqual (PreReleaseStatus.Concrete ["beta"])
    VersionRequirement.Parse("1.0-beta.2").PreReleases |> shouldEqual (PreReleaseStatus.Concrete ["beta"])
    VersionRequirement.Parse("1.0-beta02").PreReleases |> shouldEqual (PreReleaseStatus.Concrete ["beta"])
    VersionRequirement.Parse("1.0-3.beta").PreReleases |> shouldEqual (PreReleaseStatus.Concrete ["beta"])
    VersionRequirement.Parse("1.0.3-beta-4").PreReleases |> shouldEqual (PreReleaseStatus.Concrete ["beta"])
    VersionRequirement.Parse("1.0-3.beta.4").PreReleases |> shouldEqual (PreReleaseStatus.Concrete ["beta"])

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
let ``can detect range version with prerelease``() = 
    parseRange "[2.0.0-prerelease,2.0.0]" 
        |> shouldEqual (VersionRange.Range(VersionRangeBound.Including, SemVer.Parse "2.0.0-prerelease", SemVer.Parse "2.0.0", VersionRangeBound.Including))
    parseRange "[2.0.0-prerelease,2.0.0)" 
        |> shouldEqual (VersionRange.Range(VersionRangeBound.Including, SemVer.Parse "2.0.0-prerelease", SemVer.Parse "2.0.0", VersionRangeBound.Excluding))
    parseRange "[2.0.0-prerelease001,2.0.0)" 
        |> shouldEqual (VersionRange.Range(VersionRangeBound.Including, SemVer.Parse "2.0.0-prerelease001", SemVer.Parse "2.0.0", VersionRangeBound.Excluding))
    parseRange "[2.0.0-pre-release.1,2.0.0)" 
        |> shouldEqual (VersionRange.Range(VersionRangeBound.Including, SemVer.Parse "2.0.0-pre-release.1", SemVer.Parse "2.0.0", VersionRangeBound.Excluding))

[<Test>]
let ``can detect open range version``() = 
    parseRange "[2.2,)"
        |> shouldEqual (VersionRange.Minimum (SemVer.Parse "2.2"))
    parseRange "[2.2,]" 
        |> shouldEqual (VersionRange.Minimum (SemVer.Parse "2.2"))
    parseRange "[2.2, ]" 
        |> shouldEqual (VersionRange.Minimum (SemVer.Parse "2.2"))
    parseRange "[,2.2]" 
        |> shouldEqual (VersionRange.Maximum (SemVer.Parse "2.2"))
    
[<Test>]
let ``can detect "null" version``() = 
    VersionRequirement.Parse "null" 
    |> shouldEqual (DependenciesFileParser.parseVersionRequirement ">= 0")