module Paket.Requirements.FrameworkRestrictionTests

open Paket
open FsUnit
open NUnit.Framework
open Paket.Requirements

let isSupported (portable:PortableProfileType) (other:PortableProfileType) =
    let name, tfs = portable.ProfileName, portable.Frameworks
        
    let otherName, otherfws = other.ProfileName, other.Frameworks
    let weSupport =
        tfs
        |> List.collect (fun tf -> tf.RawSupportedPlatformsTransitive)

    let relevantFrameworks =
        otherfws
        |> Seq.filter (fun fw ->
            weSupport |> List.exists ((=) fw))
        |> Seq.length
    relevantFrameworks >= tfs.Length

[<Test>]
let ``Profile 158 should not support itself``() = 
    isSupported Profile158 Profile158
    |> shouldEqual false
[<Test>]
let ``Profile 158 should not support Profile 78, as there is no silverlight on 78``() = 
    isSupported Profile158 Profile78
    |> shouldEqual false
    isSupported Profile78 Profile158
    |> shouldEqual true
[<Test>]
let ``Profile 344 should support Profile 336, as it has the same frameworks but is lower``() =
    isSupported Profile336 Profile344
    |> shouldEqual false
    isSupported Profile344 Profile336
    |> shouldEqual true

[<Test>]
let ``Generate Support Table``() = 
    // TODO: Should we include this?
    let getSupported (portable:PortableProfileType) =
            let name, tfs = portable.ProfileName, portable.Frameworks
            KnownTargetProfiles.AllPortableProfiles
            |> List.filter (fun p -> p.ProfileName <> name)
            |> List.filter (fun other -> isSupported portable other)
            |> List.map PortableProfile
    let mutable supportMap =
        KnownTargetProfiles.AllPortableProfiles
        |> List.map (fun p ->
            p,
            getSupported p
            |> List.choose (function PortableProfile p -> Some p | _ -> failwithf "Expected portable"))
        |> dict

    let rec buildSupportMap p =
        let directMap = supportMap.[p]
        directMap
        |> List.append (directMap |> List.collect buildSupportMap)
        
    let filterMap pos (supportMap:System.Collections.Generic.IDictionary<_,_>) =
        supportMap
        |> Seq.map (fun (kv:System.Collections.Generic.KeyValuePair<_,_>) -> 
            let profile = kv.Key
            let supported : PortableProfileType list = kv.Value
            
            profile,
            if supported.Length < pos + 1 then
                supported
            else
                // try to optimize on the 'pos' position
                let curPos = supported.[pos]
                let supportList = buildSupportMap curPos // supportMap.[curPos] // 
                (supported |> List.take pos |> List.filter (fun s -> supportList |> List.contains s |> not))
                @ [curPos] @
                (supported
                 |> List.skip (pos + 1)
                 |> List.filter (fun s -> supportList |> List.contains s |> not))
            ) 
        |> dict

    for i in 0 .. 10 do
        for i in 0 .. 15 do supportMap <- filterMap i supportMap
    
    supportMap
    |> Seq.iter (fun (kv:System.Collections.Generic.KeyValuePair<_,_>) -> 
        let p = kv.Key
        let supported : PortableProfileType list = kv.Value
        System.Diagnostics.Debug.WriteLine(sprintf "| %s ->" p.ProfileName)
        System.Diagnostics.Debug.WriteLine("    [ ")
        supported
        |> List.iter (fun p -> 
            System.Diagnostics.Debug.WriteLine(sprintf "      %s" p.ProfileName))
        
        System.Diagnostics.Debug.WriteLine("    ] "))
        
    System.Diagnostics.Debug.WriteLine(" --- Finished")

[<Test>]
let ``Unknown Portables are detected correctly``() = 
    PlatformMatching.extractPlatforms "portable-monotouch+monoandroid"
    |> function { Platforms = o } -> TargetProfile.FindPortable o
    |> shouldEqual (PortableProfile (PortableProfileType.UnsupportedProfile [MonoTouch; MonoAndroid]))
[<Test>]
let ``Portables are detected correctly``() = 
    // http://nugettoolsdev.azurewebsites.net/4.0.0/parse-framework?framework=portable-net451%2Bwin81%2Bwpa81%2Bwaspt2
    let portables =
        [ "net40"
          "portable-monotouch+monoandroid"
          "portable-net40+sl5+win8+wp8+wpa81"; "portable-net45+winrt45+wp8+wpa81"
          "portable-win81+wpa81"
          "portable-windows8+net45+wp8"
          "sl5"; "win8"
          "wp8" ]
        |> List.map PlatformMatching.extractPlatforms
        |> List.map (function { Platforms = [ h] } -> SinglePlatform h | {Platforms = o} -> TargetProfile.FindPortable o)
    let expected =
        [ SinglePlatform (DotNetFramework FrameworkVersion.V4);
          PortableProfile (PortableProfileType.UnsupportedProfile [MonoTouch; MonoAndroid])
          PortableProfile (PortableProfileType.Profile328); PortableProfile (PortableProfileType.Profile259)
          PortableProfile (PortableProfileType.Profile32)
          PortableProfile (PortableProfileType.Profile78)
          SinglePlatform (Silverlight SilverlightVersion.V5); SinglePlatform (Windows WindowsVersion.V8)
          SinglePlatform (WindowsPhone WindowsPhoneVersion.V8) ]
    portables
    |> shouldEqual expected

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
let ``Simplify (>=net20) && (>=net20)``() = 
    let toSimplify = 
        (FrameworkRestriction.And[
            FrameworkRestriction.AtLeast (DotNetFramework FrameworkVersion.V2)
            FrameworkRestriction.AtLeast (DotNetFramework FrameworkVersion.V2)])
    toSimplify
    |> shouldEqual (FrameworkRestriction.AtLeast (DotNetFramework FrameworkVersion.V2))

[<Test>]
let ``Simplify (>=net20) || (>=net20)``() = 
    let toSimplify = 
        (FrameworkRestriction.Or[
            FrameworkRestriction.AtLeast (DotNetFramework FrameworkVersion.V2)
            FrameworkRestriction.AtLeast (DotNetFramework FrameworkVersion.V2)])
    toSimplify
    |> shouldEqual (FrameworkRestriction.AtLeast (DotNetFramework FrameworkVersion.V2))

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


