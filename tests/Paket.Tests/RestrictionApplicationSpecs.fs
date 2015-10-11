module Packet.RestrictionApplicationSpecs

open System.IO
open Paket
open Paket.Domain
open Chessie.ErrorHandling
open FsUnit
open NUnit.Framework
open TestHelpers
open Paket.Requirements

[<Test>]
let ``>= net40 does not include silverlight (#1124)`` () =
    /// https://github.com/fsprojects/Paket/issues/1124
    let restrictions = [FrameworkRestriction.AtLeast(DotNetFramework(FrameworkVersion.V4))]
    let targets = KnownTargetProfiles.DotNetFrameworkProfiles @ KnownTargetProfiles.SilverlightProfiles
    let restricted = applyRestrictionsToTargets restrictions targets
    
    restricted |> shouldEqual KnownTargetProfiles.DotNetFrameworkProfiles