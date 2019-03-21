module Paket.InterprojectReferencesConstraintSpecs

open Paket
open Paket.InterprojectReferencesConstraint
open NUnit.Framework
open FsUnit

[<TestCase("min", "1.2.3", "1.2.3")>]
[<TestCase("fix", "1.2.3", "[1.2.3]")>]
[<TestCase("keep-major", "1.2.3", "[1.2.3,2.0.0)")>]
[<TestCase("keep-minor", "1.2.3", "[1.2.3,1.3.0)")>]
[<TestCase("keep-patch", "1.2.3", "[1.2.3,1.2.4)")>]
[<TestCase("keep-major", "1.2.3-alpha1", "[1.2.3-alpha1,2.0.0)")>]
[<TestCase("keep-major", "1.2.3.0", "[1.2.3,2.0.0)")>]
[<TestCase("keep-minor", "1.2.3.0", "[1.2.3,1.3.0)")>]
[<TestCase("keep-patch", "1.2.3.0", "[1.2.3,1.2.4)")>]
let ``constraint creates correct version range`` constraintOptionValue version expectedRange =
    let irc = (InterprojectReferencesConstraint.Parse constraintOptionValue).Value
    let v = SemVer.Parse version
    let expectedConstraint = VersionRequirement.Parse expectedRange
    irc.CreateVersionRequirements v |> shouldEqual expectedConstraint.Range
