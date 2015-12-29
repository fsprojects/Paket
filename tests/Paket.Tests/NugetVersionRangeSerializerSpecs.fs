module Paket.NugetVersionRangeSerializerSpecs

open Paket
open NUnit.Framework
open FsUnit

[<Test>]
let ``can format latest version``() = 
    VersionRequirement.Parse "" |> shouldEqual VersionRequirement.AllReleases

let format(versionRange:VersionRange) = VersionRequirement(versionRange,PreReleaseStatus.No).FormatInNuGetSyntax()

[<Test>]
let ``can format specific version``() = 
    VersionRange.Specific(SemVer.Parse "2.2") |> format |> shouldEqual "[2.2.0]"
    VersionRange.Specific(SemVer.Parse "1.2") |> format |> shouldEqual "[1.2.0]"

[<Test>]
let ``can format minimum version``() = 
    VersionRange.Minimum(SemVer.Parse "2.2") |> format |> shouldEqual "2.2.0"
    VersionRange.Minimum(SemVer.Parse "1.2") |> format |> shouldEqual "1.2.0"
    VersionRange.Minimum(SemVer.Parse "7") |> format |> shouldEqual "7.0.0"
    VersionRange.Minimum(SemVer.Parse "0") |> format |> shouldEqual ""
    VersionRange.Minimum(SemVer.Parse "1.0-beta") |> format |> shouldEqual "1.0.0-beta"

[<Test>]
let ``can format greater than version``() = 
    VersionRange.GreaterThan(SemVer.Parse "2.2") |> format |> shouldEqual "(2.2.0,)"
    VersionRange.GreaterThan(SemVer.Parse "1.2") |> format |> shouldEqual "(1.2.0,)"

[<Test>]
let ``can format maximum version``() = 
    VersionRange.Maximum(SemVer.Parse "2.2") |> format |> shouldEqual "(,2.2.0]"
    VersionRange.Maximum(SemVer.Parse "0") |> format |> shouldEqual "(,0.0.0]"
    VersionRange.Maximum(SemVer.Parse "1.2") |> format |> shouldEqual "(,1.2.0]"

[<Test>]
let ``can format less than version``() = 
    VersionRange.LessThan(SemVer.Parse "2.2") |> format |> shouldEqual "(,2.2.0)"
    VersionRange.LessThan(SemVer.Parse "1.2") |> format |> shouldEqual "(,1.2.0)"

[<Test>]
let ``can format prereleases``() = 
    VersionRange.Specific(SemVer.Parse "1.0-unstable0021") |> format |> shouldEqual "[1.0.0-unstable0021]"
    VersionRange.Minimum(SemVer.Parse "1.0-unstable0021") |> format |> shouldEqual "1.0.0-unstable0021"


[<Test>]
let ``can format range version``() = 
    VersionRange.Range(VersionRangeBound.Excluding, SemVer.Parse "2.2", SemVer.Parse "3", VersionRangeBound.Excluding) |> format |> shouldEqual "(2.2.0,3.0.0)" 
    VersionRange.Range(VersionRangeBound.Excluding, SemVer.Parse "2.2", SemVer.Parse "3", VersionRangeBound.Including) |> format |> shouldEqual "(2.2.0,3.0.0]" 
    VersionRange.Range(VersionRangeBound.Including, SemVer.Parse "2.2", SemVer.Parse "3", VersionRangeBound.Excluding) |> format |> shouldEqual "[2.2.0,3.0.0)" 
    VersionRange.Range(VersionRangeBound.Including, SemVer.Parse "2.2", SemVer.Parse "3", VersionRangeBound.Including) |> format |> shouldEqual "[2.2.0,3.0.0]" 