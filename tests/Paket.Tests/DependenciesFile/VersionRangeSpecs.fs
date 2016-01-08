module Paket.DependenciesFile.VersionRangeSpecs

open Paket
open NUnit.Framework
open FsUnit

let parseRange text = DependenciesFileParser.parseVersionRequirement(text).Range

[<Test>]
let ``can detect minimum version``() = 
    parseRange ">= 2.2" |> shouldEqual (VersionRange.AtLeast "2.2")
    parseRange ">= 1.2" |> shouldEqual (VersionRange.AtLeast "1.2")

[<Test>]
let ``can detect specific version``() = 
    parseRange "2.2" |> shouldEqual (VersionRange.Exactly "2.2")
    parseRange "1.2" |> shouldEqual (VersionRange.Exactly "1.2")

    parseRange "= 2.2" |> shouldEqual (VersionRange.Exactly "2.2")
    parseRange "= 1.2" |> shouldEqual (VersionRange.Exactly "1.2")

[<Test>]
let ``can detect ordinary Between``() = 
    parseRange "~> 2.2" |> shouldEqual (VersionRange.Between("2.2","3.0"))
    parseRange "~> 1.2" |> shouldEqual (VersionRange.Between("1.2","2.0"))
    (VersionRequirement(parseRange "~> 1.0",PreReleaseStatus.All)).IsInRange(SemVer.Parse("1.0.071.9556")) |> shouldEqual true
    (VersionRequirement(parseRange "~> 1.0",PreReleaseStatus.All)).IsInRange(SemVer.Parse("1.0.071.9432")) |> shouldEqual true

[<Test>]
let ``can detect lower versions for ~>``() = 
    parseRange "~> 3.2.0.0" |> shouldEqual (VersionRange.Between("3.2.0.0","3.2.1.0"))

    parseRange "~> 1.2.3.4" |> shouldEqual (VersionRange.Between("1.2.3.4","1.2.4.0"))
    parseRange "~> 1.2.3" |> shouldEqual (VersionRange.Between("1.2.3","1.3.0"))
    parseRange "~> 1.2" |> shouldEqual (VersionRange.Between("1.2","2.0"))
    parseRange "~> 1.0" |> shouldEqual (VersionRange.Between("1.0","2.0"))
    parseRange "~> 1" |> shouldEqual (VersionRange.Between("1","2"))

[<Test>]
let ``can detect greater-than``() = 
    parseRange "> 3.2" |> shouldEqual (VersionRange.GreaterThan(SemVer.Parse "3.2"))

[<Test>]
let ``can detect less-than``() = 
    parseRange "< 3.1" |> shouldEqual (VersionRange.LessThan(SemVer.Parse "3.1"))

[<Test>]
let ``can detect less-than-or-equal``() = 
    parseRange "<= 3.1" |> shouldEqual (VersionRange.Maximum(SemVer.Parse "3.1"))

[<Test>]
let ``can detect range``() = 
    parseRange ">= 1.2.3 < 1.5" |> shouldEqual (VersionRange.Range(VersionRangeBound.Including,SemVer.Parse "1.2.3",SemVer.Parse("1.5"), VersionRangeBound.Excluding))
    parseRange ">= 1.2.3 <   1.5" |> shouldEqual (VersionRange.Range(VersionRangeBound.Including,SemVer.Parse "1.2.3",SemVer.Parse("1.5"), VersionRangeBound.Excluding))
    parseRange "> 1.2.3 < 1.5" |> shouldEqual (VersionRange.Range(VersionRangeBound.Excluding,SemVer.Parse "1.2.3",SemVer.Parse("1.5"), VersionRangeBound.Excluding))
    parseRange "> 1.2.3 <= 2.5" |> shouldEqual (VersionRange.Range(VersionRangeBound.Excluding,SemVer.Parse "1.2.3",SemVer.Parse("2.5"), VersionRangeBound.Including))
    parseRange ">= 1.2 <= 2.5" |> shouldEqual (VersionRange.Range(VersionRangeBound.Including,SemVer.Parse "1.2",SemVer.Parse("2.5"), VersionRangeBound.Including))
    parseRange "~> 1.2 >= 1.2.3" |> shouldEqual (VersionRange.Range(VersionRangeBound.Including,SemVer.Parse "1.2.3",SemVer.Parse("2.0"), VersionRangeBound.Excluding))
    parseRange "~> 1.2 > 1.2.3" |> shouldEqual (VersionRange.Range(VersionRangeBound.Excluding,SemVer.Parse "1.2.3",SemVer.Parse("2.0"), VersionRangeBound.Excluding))

[<Test>]
let ``can detect minimum NuGet version``() = 
    VersionRequirement.Parse "0" |> shouldEqual (DependenciesFileParser.parseVersionRequirement ">= 0")
    VersionRequirement.Parse "" |> shouldEqual (DependenciesFileParser.parseVersionRequirement ">= 0")
    VersionRequirement.Parse null |> shouldEqual (DependenciesFileParser.parseVersionRequirement ">= 0")

    parseRange "" |> shouldEqual (parseRange ">= 0")
    parseRange null |> shouldEqual (parseRange ">= 0")

[<Test>]
let ``can detect prereleases``() = 
    DependenciesFileParser.parseVersionRequirement "<= 3.1" 
    |> shouldEqual (VersionRequirement(VersionRange.Maximum(SemVer.Parse "3.1"),PreReleaseStatus.No))

    DependenciesFileParser.parseVersionRequirement "<= 3.1 prerelease" 
    |> shouldEqual (VersionRequirement(VersionRange.Maximum(SemVer.Parse "3.1"),PreReleaseStatus.All))

    DependenciesFileParser.parseVersionRequirement "> 3.1 alpha beta"
    |> shouldEqual (VersionRequirement(VersionRange.GreaterThan(SemVer.Parse "3.1"),(PreReleaseStatus.Concrete ["alpha"; "beta"])))

[<Test>]
let ``can detect override operator``() = 
    parseRange "== 3.2.0.0" |> shouldEqual (VersionRange.OverrideAll(SemVer.Parse "3.2.0.0"))

[<Test>]
let ``can detect override operator for beta``() = 
    parseRange "== 0.0.5-beta" |> shouldEqual (VersionRange.OverrideAll(SemVer.Parse "0.0.5-beta"))