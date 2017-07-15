module Paket.FilterVersionSpecs

open Paket
open NUnit.Framework
open FsUnit

let isInRangeIgnorePreRelease (versionRange:VersionRange) semVer =
    VersionRequirement(versionRange,PreReleaseStatus.No).IsInRange (SemVer.Parse semVer,true)

let isInRangeNoPreRelease (versionRange:VersionRange) semVer =
    VersionRequirement(versionRange,PreReleaseStatus.No).IsInRange (SemVer.Parse semVer)

let isInRangePreRelease (versionRange:VersionRange) semVer =
    VersionRequirement(versionRange,PreReleaseStatus.All).IsInRange (SemVer.Parse semVer)

let isInRange (version:VersionRequirement) semVer =
    version.IsInRange (SemVer.Parse semVer)

[<Test>]
let ``compares versions with prerelease even if ignorePreRelease is true``() =
    // Check that range.IsInRange(version, ignorePreRelease=true) only ignores PreReleaseStatus.No
    // specified in 'range'. The actual versions should still be compared correctly.
    // Minimum
    "2.1-pre1" |> isInRangeIgnorePreRelease (VersionRange.Minimum (SemVer.Parse "2.0")) |> shouldEqual true
    "2.1-pre1" |> isInRangeIgnorePreRelease (VersionRange.Minimum (SemVer.Parse "2.1")) |> shouldEqual false
    "2.1-pre1" |> isInRangeIgnorePreRelease (VersionRange.Minimum (SemVer.Parse "2.1-pre1")) |> shouldEqual true
    "2.1-pre1" |> isInRangeIgnorePreRelease (VersionRange.Minimum (SemVer.Parse "2.1-pre2")) |> shouldEqual false
    "2.1" |> isInRangeIgnorePreRelease (VersionRange.Minimum (SemVer.Parse "2.1-pre1")) |> shouldEqual true
    // Maximum
    "2.1-pre1" |> isInRangeIgnorePreRelease (VersionRange.Maximum (SemVer.Parse "2.1")) |> shouldEqual true
    "2.1-pre1" |> isInRangeIgnorePreRelease (VersionRange.Maximum (SemVer.Parse "2.0")) |> shouldEqual false
    "2.1-pre1" |> isInRangeIgnorePreRelease (VersionRange.Maximum (SemVer.Parse "2.1-pre1")) |> shouldEqual true
    "2.1-pre1" |> isInRangeIgnorePreRelease (VersionRange.Maximum (SemVer.Parse "2.1-pre0")) |> shouldEqual false
    "2.1" |> isInRangeIgnorePreRelease (VersionRange.Maximum (SemVer.Parse "2.1-pre1")) |> shouldEqual false
    // Specific
    "2.1-pre1" |> isInRangeIgnorePreRelease (VersionRange.Specific (SemVer.Parse "2.1-pre1")) |> shouldEqual true
    "2.1-pre1" |> isInRangeIgnorePreRelease (VersionRange.Specific (SemVer.Parse "2.1")) |> shouldEqual false
    "2.1" |> isInRangeIgnorePreRelease (VersionRange.Specific (SemVer.Parse "2.1-pre1")) |> shouldEqual false
    // Range
    "2.1-pre1" |> isInRangeIgnorePreRelease (VersionRange.Range (VersionRangeBound.Excluding, (SemVer.Parse "2.0"), (SemVer.Parse "2.1"), VersionRangeBound.Excluding)) |> shouldEqual true
    "2.1-pre1" |> isInRangeIgnorePreRelease (VersionRange.Range (VersionRangeBound.Excluding, (SemVer.Parse "2.1"), (SemVer.Parse "2.2"), VersionRangeBound.Excluding)) |> shouldEqual false
    "2.1-pre1" |> isInRangeIgnorePreRelease (VersionRange.Range (VersionRangeBound.Including, (SemVer.Parse "2.0"), (SemVer.Parse "2.1"), VersionRangeBound.Including)) |> shouldEqual true
    "2.1-pre1" |> isInRangeIgnorePreRelease (VersionRange.Range (VersionRangeBound.Including, (SemVer.Parse "2.1"), (SemVer.Parse "2.2"), VersionRangeBound.Including)) |> shouldEqual false

[<Test>]
let ``can check if in range for Specific``() =
    "2.2" |> isInRangeNoPreRelease (VersionRange.Specific (SemVer.Parse "2.2")) |> shouldEqual true
    "2.4" |> isInRangeNoPreRelease (VersionRange.Specific (SemVer.Parse "2.2")) |> shouldEqual false
    "2.2" |> isInRangeNoPreRelease (VersionRange.Specific (SemVer.Parse "2.4")) |> shouldEqual false
    
[<Test>]
let ``can check if in range for Minimum``() =
    "2.1" |> isInRangeNoPreRelease (VersionRange.Minimum (SemVer.Parse "2.2")) |> shouldEqual false
    "2.2" |> isInRangeNoPreRelease (VersionRange.Minimum (SemVer.Parse "2.2")) |> shouldEqual true
    "3.0" |> isInRangeNoPreRelease (VersionRange.Minimum (SemVer.Parse "2.2")) |> shouldEqual true
    "1.1-beta" |> isInRangePreRelease (VersionRange.Minimum(SemVer.Parse "1.0-beta")) |> shouldEqual true
    "2.0.3" |> isInRangeNoPreRelease (VersionRange.AtLeast "0") |> shouldEqual true
    "1.0.0-rc3-23805" |> isInRangePreRelease (VersionRange.Minimum (SemVer.Parse "0")) |> shouldEqual true
    "1.0.0-rc3-23805" |> isInRangeNoPreRelease (VersionRange.Minimum (SemVer.Parse "0")) |> shouldEqual false
    
[<Test>]
let ``can check if in range for GreaterThan``() =
    "2.1" |> isInRangeNoPreRelease (VersionRange.GreaterThan (SemVer.Parse "2.2")) |> shouldEqual false
    "2.2" |> isInRangeNoPreRelease (VersionRange.GreaterThan (SemVer.Parse "2.2")) |> shouldEqual false
    "3.0" |> isInRangeNoPreRelease (VersionRange.GreaterThan (SemVer.Parse "2.2")) |> shouldEqual true

[<Test>]
let ``can check if in range for Maximum``() =
    "2.0" |> isInRangeNoPreRelease (VersionRange.Maximum (SemVer.Parse "2.2")) |> shouldEqual true
    "2.2" |> isInRangeNoPreRelease (VersionRange.Maximum (SemVer.Parse "2.2")) |> shouldEqual true
    "3.0" |> isInRangeNoPreRelease (VersionRange.Maximum (SemVer.Parse "2.2")) |> shouldEqual false

[<Test>]
let ``can check if in range for LessThan``() =
    "2.0" |> isInRangeNoPreRelease (VersionRange.LessThan (SemVer.Parse "2.2")) |> shouldEqual true
    "2.2" |> isInRangeNoPreRelease (VersionRange.LessThan (SemVer.Parse "2.2")) |> shouldEqual false
    "3.0" |> isInRangeNoPreRelease (VersionRange.LessThan (SemVer.Parse "2.2")) |> shouldEqual false

[<Test>]
let ``can check if in range for LessThan with prerelease``() =
    "3.0.0-alpha1" |> isInRange (DependenciesFileParser.parseVersionRequirement "< 3.0 prerelease") |> shouldEqual false
    
[<Test>]
let ``can check if in range for Range``() =
    "2.1" |> isInRangeNoPreRelease (VersionRange.Range (VersionRangeBound.Excluding, (SemVer.Parse "2.2"), (SemVer.Parse "3.0"), VersionRangeBound.Excluding)) |> shouldEqual false
    "2.2" |> isInRangeNoPreRelease (VersionRange.Range (VersionRangeBound.Excluding, (SemVer.Parse "2.2"), (SemVer.Parse "3.0"), VersionRangeBound.Excluding)) |> shouldEqual false
    "2.5" |> isInRangeNoPreRelease (VersionRange.Range (VersionRangeBound.Excluding, (SemVer.Parse "2.2"), (SemVer.Parse "3.0"), VersionRangeBound.Excluding)) |> shouldEqual true
    "3.0" |> isInRangeNoPreRelease (VersionRange.Range (VersionRangeBound.Excluding, (SemVer.Parse "2.2"), (SemVer.Parse "3.0"), VersionRangeBound.Excluding)) |> shouldEqual false
    "3.2" |> isInRangeNoPreRelease (VersionRange.Range (VersionRangeBound.Excluding, (SemVer.Parse "2.2"), (SemVer.Parse "3.0"), VersionRangeBound.Excluding)) |> shouldEqual false

    "2.1" |> isInRangeNoPreRelease (VersionRange.Range (VersionRangeBound.Excluding, (SemVer.Parse "2.2"), (SemVer.Parse "3.0"), VersionRangeBound.Including)) |> shouldEqual false
    "2.2" |> isInRangeNoPreRelease (VersionRange.Range (VersionRangeBound.Excluding, (SemVer.Parse "2.2"), (SemVer.Parse "3.0"), VersionRangeBound.Including)) |> shouldEqual false
    "2.5" |> isInRangeNoPreRelease (VersionRange.Range (VersionRangeBound.Excluding, (SemVer.Parse "2.2"), (SemVer.Parse "3.0"), VersionRangeBound.Including)) |> shouldEqual true
    "3.0" |> isInRangeNoPreRelease (VersionRange.Range (VersionRangeBound.Excluding, (SemVer.Parse "2.2"), (SemVer.Parse "3.0"), VersionRangeBound.Including)) |> shouldEqual true
    "3.2" |> isInRangeNoPreRelease (VersionRange.Range (VersionRangeBound.Excluding, (SemVer.Parse "2.2"), (SemVer.Parse "3.0"), VersionRangeBound.Including)) |> shouldEqual false

    "2.1" |> isInRangeNoPreRelease (VersionRange.Range (VersionRangeBound.Including, (SemVer.Parse "2.2"), (SemVer.Parse "3.0"), VersionRangeBound.Excluding)) |> shouldEqual false
    "2.2" |> isInRangeNoPreRelease (VersionRange.Range (VersionRangeBound.Including, (SemVer.Parse "2.2"), (SemVer.Parse "3.0"), VersionRangeBound.Excluding)) |> shouldEqual true
    "2.5" |> isInRangeNoPreRelease (VersionRange.Range (VersionRangeBound.Including, (SemVer.Parse "2.2"), (SemVer.Parse "3.0"), VersionRangeBound.Excluding)) |> shouldEqual true
    "3.0" |> isInRangeNoPreRelease (VersionRange.Range (VersionRangeBound.Including, (SemVer.Parse "2.2"), (SemVer.Parse "3.0"), VersionRangeBound.Excluding)) |> shouldEqual false
    "3.2" |> isInRangeNoPreRelease (VersionRange.Range (VersionRangeBound.Including, (SemVer.Parse "2.2"), (SemVer.Parse "3.0"), VersionRangeBound.Excluding)) |> shouldEqual false

    "2.1" |> isInRangeNoPreRelease (VersionRange.Range (VersionRangeBound.Including, (SemVer.Parse "2.2"), (SemVer.Parse "3.0"), VersionRangeBound.Including)) |> shouldEqual false
    "2.2" |> isInRangeNoPreRelease (VersionRange.Range (VersionRangeBound.Including, (SemVer.Parse "2.2"), (SemVer.Parse "3.0"), VersionRangeBound.Including)) |> shouldEqual true
    "2.5" |> isInRangeNoPreRelease (VersionRange.Range (VersionRangeBound.Including, (SemVer.Parse "2.2"), (SemVer.Parse "3.0"), VersionRangeBound.Including)) |> shouldEqual true
    "3.0" |> isInRangeNoPreRelease (VersionRange.Range (VersionRangeBound.Including, (SemVer.Parse "2.2"), (SemVer.Parse "3.0"), VersionRangeBound.Including)) |> shouldEqual true
    "3.2" |> isInRangeNoPreRelease (VersionRange.Range (VersionRangeBound.Including, (SemVer.Parse "2.2"), (SemVer.Parse "3.0"), VersionRangeBound.Including)) |> shouldEqual false

[<Test>]
let ``can check if in range for 4-parts range``() =
    "1.0.0.3108" |> isInRange (DependenciesFileParser.parseVersionRequirement "1.0.0.3108") |> shouldEqual true
    "1.0.0.2420" |> isInRange (DependenciesFileParser.parseVersionRequirement "~> 1.0") |> shouldEqual true

[<Test>]
let ``does include prerelease when twiddle wakka``() =
    "1.0.0-alpha002" |> isInRange (DependenciesFileParser.parseVersionRequirement "~> 1.0 alpha") |> shouldEqual true
    "1.0" |> isInRange (DependenciesFileParser.parseVersionRequirement "~> 1.0 alpha") |> shouldEqual true

[<Test>]
let ``does not skip version when twiddle wakka``() =
    "3.0.0-alpha1" |> isInRange (DependenciesFileParser.parseVersionRequirement "~> 2.0") |> shouldEqual false

[<Test>]
let ``does not skip version when twiddle wakka with prerelease``() =
    "3.0.0-alpha1" |> isInRange (DependenciesFileParser.parseVersionRequirement "~> 2.0 prerelease") |> shouldEqual false
    "2.0-alpha" |> isInRange (DependenciesFileParser.parseVersionRequirement "~> 2.0 prerelease") |> shouldEqual true

[<Test>]
let ``can support trailing 0``() =
    "1.2.3" |> isInRange (DependenciesFileParser.parseVersionRequirement "1.2.3.0") |> shouldEqual true

[<Test>]
let ``can support alpha version``() = 
    "1.2.3-alpha001" |> isInRange (DependenciesFileParser.parseVersionRequirement "1.2.3-alpha001") |> shouldEqual true
    "1.2.3-alpha001" |> isInRange (DependenciesFileParser.parseVersionRequirement "1.2.3") |> shouldEqual false
    "1.2.3-alpha003" |> isInRange (DependenciesFileParser.parseVersionRequirement ">= 1") |> shouldEqual false

    "1.2.3-alpha003" |> isInRange (DependenciesFileParser.parseVersionRequirement ">= 1 prerelease") |> shouldEqual true
    "1.2.3-alpha023" |> isInRange (DependenciesFileParser.parseVersionRequirement ">= 1 alpha") |> shouldEqual true
    "1.2.3-alpha023" |> isInRange (DependenciesFileParser.parseVersionRequirement ">= 1 alpha rc") |> shouldEqual true
    "1.2.3-alpha023" |> isInRange (DependenciesFileParser.parseVersionRequirement ">= 1 beta rc") |> shouldEqual false

[<Test>]
let ``can support rc version``() = 
    "1.2.3-rec003" |> isInRange (DependenciesFileParser.parseVersionRequirement ">= 1 prerelease") |> shouldEqual true
    "1.2.3-rc2" |> isInRange (DependenciesFileParser.parseVersionRequirement ">= 1 alpha") |> shouldEqual false
    "1.2.3-rc2" |> isInRange (DependenciesFileParser.parseVersionRequirement ">= 1 beta rc") |> shouldEqual true

    "1.2.3-rc2" |> isInRange (DependenciesFileParser.parseVersionRequirement ">= 2 beta rc") |> shouldEqual false

[<Test>]
let ``can support "build" version``() = 
    "0.9.0-build06428" |> isInRange (DependenciesFileParser.parseVersionRequirement ">= 0.9.0-build06428") |> shouldEqual true

[<Test>]
let ``prerelase version of same version is in range``() = 
    "1.2.3-alpha001" |> isInRange (DependenciesFileParser.parseVersionRequirement ">= 1.2.3 prerelease") |> shouldEqual true
    "1.2.3-alpha001" |> isInRange (DependenciesFileParser.parseVersionRequirement "1.2.3 prerelease") |> shouldEqual true
    "1.2.3-alpha001" |> isInRange (DependenciesFileParser.parseVersionRequirement "> 1.2.3 prerelease") |> shouldEqual false
    "1.0.11" |> isInRange (DependenciesFileParser.parseVersionRequirement ">= 1.0 prerelease") |> shouldEqual true
    "1.0.12-build0025" |> isInRange (DependenciesFileParser.parseVersionRequirement ">= 1.0 prerelease") |> shouldEqual true

[<Test>]
let ``can check if in range for prerelease range``() =
    let r = VersionRange.Range(VersionRangeBound.Including, SemVer.Parse "2.0.0-prerelease", SemVer.Parse "2.0.0", VersionRangeBound.Including)
    "1.0.0.3108" |> isInRangePreRelease r |> shouldEqual false
    "2.0.0-unstable2" |> isInRangePreRelease r |> shouldEqual true
    "2.0-unstable2" |> isInRangePreRelease r |> shouldEqual true
    "2.0-alpha1" |> isInRangePreRelease r |> shouldEqual true