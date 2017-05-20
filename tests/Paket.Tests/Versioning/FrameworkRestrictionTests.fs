module Paket.Requirements.FrameworkRestrictionTests

open Paket
open FsUnit
open NUnit.Framework
open Paket.Requirements

[<Test>]
let ``combineOrSimplifiesRestrictions``() = 
    let left =
        (FrameworkRestriction.Or
             (FrameworkRestriction.And
                (FrameworkRestriction.And 
                    (FrameworkRestriction.NoRestriction,
                     FrameworkRestriction.Not (FrameworkRestriction.AtLeast (DotNetFramework FrameworkVersion.V3_5))),
                FrameworkRestriction.Not (FrameworkRestriction.AtLeast (DotNetFramework FrameworkVersion.V4_Client))),
             FrameworkRestriction.And
                (FrameworkRestriction.AtLeast (DotNetFramework FrameworkVersion.V3_5),
                 FrameworkRestriction.Not (FrameworkRestriction.AtLeast (DotNetFramework FrameworkVersion.V4_Client)))))
    let right = FrameworkRestriction.AtLeast (DotNetFramework FrameworkVersion.V4_Client)
    combineRestrictionsWithOr left right
    |> shouldEqual FrameworkRestriction.NoRestriction


