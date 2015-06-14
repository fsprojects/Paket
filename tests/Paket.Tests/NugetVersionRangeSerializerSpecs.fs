module Paket.NugetVersionRangeSerializerSpecs

open Paket
open NUnit.Framework
open FsUnit

[<Test>]
let ``can format latest version``() = 
    VersionRequirement.Parse "" |> shouldEqual VersionRequirement.AllReleases

let format(versionRange:VersionRange) = versionRange.FormatInNuGetSyntax()

[<Test>]
let ``can format specific version``() = 
    VersionRange.Specific(SemVer.Parse "2.2").FormatInNuGetSyntax() |> shouldEqual "[2.2.0]"
    VersionRange.Specific(SemVer.Parse "1.2").FormatInNuGetSyntax() |> shouldEqual "[1.2.0]"

[<Test>]
let ``can format minimum version``() = 
    VersionRange.Minimum(SemVer.Parse "2.2").FormatInNuGetSyntax() |> shouldEqual "2.2.0"
    VersionRange.Minimum(SemVer.Parse "1.2").FormatInNuGetSyntax() |> shouldEqual "1.2.0"
    VersionRange.Minimum(SemVer.Parse "7").FormatInNuGetSyntax() |> shouldEqual "7.0.0"
    VersionRange.Minimum(SemVer.Parse "0").FormatInNuGetSyntax() |> shouldEqual ""
    VersionRange.Minimum(SemVer.Parse "1.0-beta").FormatInNuGetSyntax() |> shouldEqual "1.0.0-beta"

[<Test>]
let ``can format greater than version``() = 
    VersionRange.GreaterThan(SemVer.Parse "2.2").FormatInNuGetSyntax() |> shouldEqual "(2.2.0,)"
    VersionRange.GreaterThan(SemVer.Parse "1.2").FormatInNuGetSyntax() |> shouldEqual "(1.2.0,)"

[<Test>]
let ``can format maximum version``() = 
    VersionRange.Maximum(SemVer.Parse "2.2").FormatInNuGetSyntax() |> shouldEqual "(,2.2.0]"
    VersionRange.Maximum(SemVer.Parse "0").FormatInNuGetSyntax() |> shouldEqual "(,0.0.0]"
    VersionRange.Maximum(SemVer.Parse "1.2").FormatInNuGetSyntax() |> shouldEqual "(,1.2.0]"

[<Test>]
let ``can format less than version``() = 
    VersionRange.LessThan(SemVer.Parse "2.2").FormatInNuGetSyntax() |> shouldEqual "(,2.2.0)"
    VersionRange.LessThan(SemVer.Parse "1.2").FormatInNuGetSyntax() |> shouldEqual "(,1.2.0)"

[<Test>]
let ``can format range version``() = 
    VersionRange.Range(VersionRangeBound.Excluding, SemVer.Parse "2.2", SemVer.Parse "3", VersionRangeBound.Excluding).FormatInNuGetSyntax() |> shouldEqual "(2.2.0,3.0.0)" 
    VersionRange.Range(VersionRangeBound.Excluding, SemVer.Parse "2.2", SemVer.Parse "3", VersionRangeBound.Including).FormatInNuGetSyntax() |> shouldEqual "(2.2.0,3.0.0]" 
    VersionRange.Range(VersionRangeBound.Including, SemVer.Parse "2.2", SemVer.Parse "3", VersionRangeBound.Excluding).FormatInNuGetSyntax() |> shouldEqual "[2.2.0,3.0.0)" 
    VersionRange.Range(VersionRangeBound.Including, SemVer.Parse "2.2", SemVer.Parse "3", VersionRangeBound.Including).FormatInNuGetSyntax() |> shouldEqual "[2.2.0,3.0.0]" 