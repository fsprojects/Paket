module Paket.Requirements.FrameworkRestrictionTests

open Paket
open FsUnit
open NUnit.Framework
open Paket.Requirements

[<Test>]
let ``CustomProfile is Supported by its Platforms``() =
    let unknownProfile =
        (PlatformMatching.extractPlatforms "portable-net45+monoandroid10+monotouch10+xamarinios10").ToTargetProfile.Value

    unknownProfile.IsSupportedBy (SinglePlatform (DotNetFramework FrameworkVersion.V4_5))
    |> shouldEqual true
    unknownProfile.IsSupportedBy (SinglePlatform (DotNetFramework FrameworkVersion.V4))
    |> shouldEqual false

    unknownProfile.IsSupportedBy (SinglePlatform (MonoAndroid))
    |> shouldEqual true
    unknownProfile.IsSupportedBy (SinglePlatform (MonoTouch))
    |> shouldEqual true
    unknownProfile.IsSupportedBy (SinglePlatform (Silverlight SilverlightVersion.V5))
    |> shouldEqual false

[<Test>]
let ``__unknowntfm__ should not match everything`` () =
    try
        let model =
            InstallModel.CreateFromLibs(Paket.Domain.PackageName "Rx-XAML", SemVer.Parse "2.2.4", FrameworkRestriction.NoRestriction,
                [  { Paket.NuGet.UnparsedPackageFile.FullPath = @"..\Rx-XAML\lib\__unknowntfm__\System.Reactive.Windows.Threading.dll"
                     Paket.NuGet.UnparsedPackageFile.PathWithinPackage = "lib/__unknowntfm__/System.Reactive.Windows.Threading.dll" }  ],
                   [],
                   [],
                   { References = NuspecReferences.All
                     OfficialName = "Reactive Extensions - XAML Support Library"
                     Version = "2.2.4"
                     Dependencies = []
                     LicenseUrl = ""
                     IsDevelopmentDependency = false
                     FrameworkAssemblyReferences = []})
        let target = PortableProfile PortableProfileType.Profile344
        let newModel = model.ApplyFrameworkRestrictions (FrameworkRestriction.ExactlyPlatform target)
        newModel.GetCompileReferences target |> Seq.toArray
        |> shouldEqual [||]
    with e ->
        // Throwing is OK as well.
        ()

[<Test>]
let ``Profile 158 should not support itself``() = 
    PortableProfileSupportCalculation.isSupported Profile158 Profile158
    |> shouldEqual false
[<Test>]
let ``Profile 158 should not support Profile 78, as there is no silverlight on 78``() = 
    PortableProfileSupportCalculation.isSupported Profile158 Profile78
    |> shouldEqual false
    PortableProfileSupportCalculation.isSupported Profile78 Profile158
    |> shouldEqual true
[<Test>]
let ``Profile 344 should support Profile 336, as it has the same frameworks but is lower``() =
    PortableProfileSupportCalculation.isSupported Profile336 Profile344
    |> shouldEqual false
    PortableProfileSupportCalculation.isSupported Profile344 Profile336
    |> shouldEqual true

[<Test>]
let ``Generate Support Table``() = 
    // TODO: Should we include this?
    let mutable supportMap = PortableProfileSupportCalculation.createInitialSupportMap()
    supportMap <- PortableProfileSupportCalculation.optimizeSupportMap supportMap
    
    supportMap
    |> PortableProfileSupportCalculation.toSeq
    |> Seq.iter (fun (p, supported) ->
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
    |> shouldEqual (PortableProfile (PortableProfileType.UnsupportedProfile [MonoAndroid; MonoTouch]))
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
          PortableProfile (PortableProfileType.UnsupportedProfile [MonoAndroid; MonoTouch])
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
let ``Simplify <|| (&& (net452) (native)) (native)>`` () =
    let toSimplify = 
        (FrameworkRestriction.Or[
            FrameworkRestriction.And[
                FrameworkRestriction.Exactly (DotNetFramework FrameworkVersion.V4_5_2)
                FrameworkRestriction.Exactly (Native(NoBuildMode,NoPlatform))]
            FrameworkRestriction.Exactly (Native(NoBuildMode,NoPlatform))])
    toSimplify
    |> shouldEqual (FrameworkRestriction.Exactly (Native(NoBuildMode,NoPlatform)))

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


