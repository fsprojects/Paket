module Paket.Requirements.FrameworkRestrictionTests

open Paket
open FsUnit
open NUnit.Framework
open Paket.Requirements
[<Test>]
let ``PortableProfile supports PortableProfiles but it is not recursive``() = 
    let portableProfiles =
        KnownTargetProfiles.AllPortableProfiles
        |> List.map PortableProfile
    let getSupported (p:TargetProfile) = p.SupportedPlatforms 
    let supportTree = ()
        //Seq.initInfinite (fun _ -> 0)
        //|> Seq.fold (fun (lastItems) _ -> ()
        //    ) portableProfiles
            
        
    ()


[<Test>]
let ``PlatformMatching works with portable ``() = 
    // portable-net40-sl4
    let l = PlatformMatching.getPlatformsSupporting (KnownTargetProfiles.FindPortableProfile "Profile18")
    // portable-net45-sl5
    let needPortable = KnownTargetProfiles.FindPortableProfile "Profile24"

    l
    |> shouldContain needPortable

[<Test>]
let ``Simplify && (&& (>= net40-full) (< net46)  (>= net20)``() = 
    let toSimplify = 
        (FrameworkRestriction.And[
            FrameworkRestriction.AtLeast (DotNetFramework FrameworkVersion.V4)
            FrameworkRestriction.NotAtLeast (DotNetFramework FrameworkVersion.V4_6)
            FrameworkRestriction.AtLeast (DotNetFramework FrameworkVersion.V2)])
    toSimplify
    |> shouldEqual (FrameworkRestriction.Between (DotNetFramework FrameworkVersion.V4, DotNetFramework FrameworkVersion.V4_6))

[<Test>]
let ``Simplify || (&& (< net40) (< net35)) (&& (< net40) (>= net35)) (>= net40))``() = 
    // Test simplify || (&& (< net40) (< net35)) (&& (< net40) (>= net35)) (>= net40))
    let smaller =
        (FrameworkRestriction.And[
            FrameworkRestriction.NotAtLeast (DotNetFramework FrameworkVersion.V4)
            FrameworkRestriction.NotAtLeast (DotNetFramework FrameworkVersion.V3_5)])
    let between =
        (FrameworkRestriction.And[
            FrameworkRestriction.NotAtLeast (DotNetFramework FrameworkVersion.V4)
            FrameworkRestriction.AtLeast (DotNetFramework FrameworkVersion.V3_5)])
    let rest = FrameworkRestriction.AtLeast (DotNetFramework FrameworkVersion.V4)

    let combined = FrameworkRestriction.Or [ smaller ; between; rest ]
    combined
    |> shouldEqual FrameworkRestriction.NoRestriction

[<Test>]
let ``Empty set should be empty``() = 
    FrameworkRestriction.EmptySet.RepresentedFrameworks
    |> shouldEqual []
    
[<Test>]
let ``NoRestriction set should not be empty``() = 
    FrameworkRestriction.NoRestriction.RepresentedFrameworks
    |> shouldNotEqual []

[<Test>]
let ``combineOr can simplify the NoRestriction set``() = 
    let left =
        (FrameworkRestriction.Or
            [FrameworkRestriction.And
              [ FrameworkRestriction.And 
                   [ FrameworkRestriction.NoRestriction
                     FrameworkRestriction.NotAtLeast (DotNetFramework FrameworkVersion.V3_5)]
                FrameworkRestriction.NotAtLeast (DotNetFramework FrameworkVersion.V4)]
             FrameworkRestriction.And
                [FrameworkRestriction.AtLeast (DotNetFramework FrameworkVersion.V3_5)
                 FrameworkRestriction.NotAtLeast (DotNetFramework FrameworkVersion.V4)]])
    let right = FrameworkRestriction.AtLeast (DotNetFramework FrameworkVersion.V4)
    FrameworkRestriction.combineRestrictionsWithOr left right
    |> shouldEqual FrameworkRestriction.NoRestriction

[<Test>]
let ``combineOr can simplify disjunct sets``() = 
    let left =
         FrameworkRestriction.And[
            FrameworkRestriction.AtLeast (DotNetFramework FrameworkVersion.V4_5_1)
            FrameworkRestriction.NotAtLeast (DotNetFramework FrameworkVersion.V4_6_2)]
    let right = FrameworkRestriction.AtLeast (DotNetFramework FrameworkVersion.V4_6_2)
    // we can simplify this expression to >=net451 because they are disjunct
    let combined = FrameworkRestriction.combineRestrictionsWithOr left right

    combined
    |> shouldEqual (FrameworkRestriction.AtLeast (DotNetFramework FrameworkVersion.V4_5_1))

[<Test>]
let ``combineOr needs to consider partly disjunct sets``() = 
    let left =
         FrameworkRestriction.And[
            FrameworkRestriction.AtLeast (DotNetFramework FrameworkVersion.V4_5_1)
            FrameworkRestriction.NotAtLeast (DotNetStandard DotNetStandardVersion.V1_3)]
    let right = FrameworkRestriction.AtLeast (DotNetStandard DotNetStandardVersion.V1_3)
    // Logic says this is >= net451 but it is >= net451 || >= netstandard13
    FrameworkRestriction.combineRestrictionsWithOr left right
    |> shouldEqual (FrameworkRestriction.Or[
                        FrameworkRestriction.AtLeast (DotNetFramework FrameworkVersion.V4_5_1)
                        FrameworkRestriction.AtLeast (DotNetStandard DotNetStandardVersion.V1_3)])


