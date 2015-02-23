module Paket.NugetVersionRangeSerializerSpecs

open Paket
open NUnit.Framework
open FsUnit

[<Test>]
let ``can detect latest version``() = 
    VersionRequirement.Parse "" |> shouldEqual VersionRequirement.AllReleases

let format(versionRange:VersionRange) = versionRange.FormatInNuGetSyntax()

[<Test>]
let ``can detect specific version``() = 
    VersionRange.Specific(SemVer.Parse "2.2").FormatInNuGetSyntax() |> shouldEqual "[2.2]"
    VersionRange.Specific(SemVer.Parse "1.2").FormatInNuGetSyntax() |> shouldEqual "[1.2]"

[<Test>]
let ``can detect minimum version``() = 
    VersionRange.Minimum(SemVer.Parse "2.2").FormatInNuGetSyntax() |> shouldEqual "2.2"
    VersionRange.Minimum(SemVer.Parse "1.2").FormatInNuGetSyntax() |> shouldEqual "1.2"
    VersionRange.Minimum(SemVer.Parse "0").FormatInNuGetSyntax() |> shouldEqual ""

[<Test>]
let ``can detect greater than version``() = 
    VersionRange.GreaterThan(SemVer.Parse "2.2").FormatInNuGetSyntax() |> shouldEqual "(2.2,)"
    VersionRange.GreaterThan(SemVer.Parse "1.2").FormatInNuGetSyntax() |> shouldEqual "(1.2,)"

[<Test>]
let ``can detect maximum version``() = 
    VersionRange.Maximum(SemVer.Parse "2.2").FormatInNuGetSyntax() |> shouldEqual "(,2.2]"
    VersionRange.Maximum(SemVer.Parse "0").FormatInNuGetSyntax() |> shouldEqual "(,0]"
    VersionRange.Maximum(SemVer.Parse "1.2").FormatInNuGetSyntax() |> shouldEqual "(,1.2]"

[<Test>]
let ``can detect less than version``() = 
    VersionRange.LessThan(SemVer.Parse "2.2").FormatInNuGetSyntax() |> shouldEqual "(,2.2)"
    VersionRange.LessThan(SemVer.Parse "1.2").FormatInNuGetSyntax() |> shouldEqual "(,1.2)"

[<Test>]
let ``can detect range version``() = 
    VersionRange.Range(VersionRangeBound.Excluding, SemVer.Parse "2.2", SemVer.Parse "3", VersionRangeBound.Excluding).FormatInNuGetSyntax() |> shouldEqual "(2.2,3)" 
    VersionRange.Range(VersionRangeBound.Excluding, SemVer.Parse "2.2", SemVer.Parse "3", VersionRangeBound.Including).FormatInNuGetSyntax() |> shouldEqual "(2.2,3]" 
    VersionRange.Range(VersionRangeBound.Including, SemVer.Parse "2.2", SemVer.Parse "3", VersionRangeBound.Excluding).FormatInNuGetSyntax() |> shouldEqual "[2.2,3)" 
    VersionRange.Range(VersionRangeBound.Including, SemVer.Parse "2.2", SemVer.Parse "3", VersionRangeBound.Including).FormatInNuGetSyntax() |> shouldEqual "[2.2,3]" 