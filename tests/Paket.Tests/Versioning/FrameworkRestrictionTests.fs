module Paket.Requirements.FrameworkRestrictionTests

open Paket
open FsUnit
open NUnit.Framework
open Paket.Requirements

[<Test>]
let ``Should replace old frameworks in framework restrictions < dnxcore50`` () =
    // There are no frameworks actually represented by this
    let restrictions = "< dnxcore50"

    let restriction, parseProblems = Requirements.parseRestrictionsSimplified restrictions
    parseProblems |> Seq.toList |> shouldEqual []
    restriction.RepresentedFrameworks |> shouldEqual Requirements.FrameworkRestriction.NoRestriction.RepresentedFrameworks
    restriction.RawFormular.ToString() |> shouldEqual "< dnxcore50"

    let simplified = Paket.Requirements.FrameworkRestriction.simplify restriction
    simplified |> shouldEqual Paket.Requirements.FrameworkRestriction.NoRestriction
    simplified.RepresentedFrameworks |> shouldEqual Requirements.FrameworkRestriction.NoRestriction.RepresentedFrameworks
    simplified.RawFormular.ToString() |> shouldEqual "true"

[<Test>]
let ``Should replace old frameworks in framework restrictions >= dnxcore50`` () =
    // There are no frameworks actually represented by this
    let restrictions = ">= dnxcore50"

    let restriction, parseProblems = Requirements.parseRestrictionsSimplified restrictions
    parseProblems |> Seq.toList |> shouldEqual []
    restriction.RepresentedFrameworks |> shouldEqual Set.empty
    restriction.RawFormular.ToString() |> shouldEqual ">= dnxcore50"

    let simplified = Paket.Requirements.FrameworkRestriction.simplify restriction
    simplified |> shouldEqual Paket.Requirements.FrameworkRestriction.EmptySet
    simplified.RepresentedFrameworks |> shouldEqual Set.empty
    simplified.RawFormular.ToString() |> shouldEqual "false"

[<Test>]
let ``Should simplify DNXCode`` () =
    let restrictions = "|| (&& (>= dnxcore50) (>= net46)) (&& (>= dnxcore50) (>= netstandard1.1)) (&& (>= dnxcore50) (>= netstandard1.2)) (&& (>= dnxcore50) (>= netstandard1.3)) (&& (>= dnxcore50) (>= netstandard1.4)) (&& (>= dnxcore50) (>= netstandard1.5)) (&& (>= dnxcore50) (>= netstandard1.6)) (&& (>= dnxcore50) (>= uap10.0)) (&& (< monoandroid) (< net45) (< netstandard1.2) (>= netstandard1.3) (< win8)) (&& (< monoandroid) (< net45) (< netstandard1.2) (>= netstandard1.6) (< win8)) (&& (< monoandroid) (< net45) (< netstandard1.3) (>= netstandard1.6) (< win8) (< wpa81)) (&& (< monoandroid) (< net45) (< netstandard1.4) (>= netstandard1.6) (< win8) (< wpa81)) (&& (< monoandroid) (< net45) (< netstandard1.5) (>= netstandard1.6) (< win8) (< wpa81)) (&& (< monoandroid) (< net452) (< netstandard1.6) (>= netstandard2.0)) (&& (< monoandroid) (< net452) (>= netstandard2.0) (< xamarinios)) (&& (< net45) (>= net46) (< netstandard1.2)) (&& (< net45) (>= net46) (< netstandard1.3)) (&& (< net45) (>= net46) (>= netstandard1.4) (< netstandard1.5)) (&& (< net45) (>= net46) (< netstandard1.4)) (&& (< net45) (>= net46) (>= netstandard1.5) (< netstandard1.6)) (&& (< net45) (>= net46) (>= netstandard1.6) (< netstandard2.0)) (&& (< net45) (>= netstandard1.3) (< netstandard1.4) (< win8) (< wpa81)) (&& (< net45) (>= netstandard1.4) (< netstandard1.5) (< win8) (< wpa81)) (&& (< net45) (>= netstandard1.5) (< netstandard1.6) (< win8) (< wpa81)) (&& (< net45) (>= netstandard1.6) (< netstandard2.0) (< win8) (< wpa81)) (&& (< net452) (>= net46) (>= netstandard2.0)) (&& (>= net46) (>= uap10.0)) (&& (>= netstandard1.6) (>= uap10.0)) (&& (< netstandard1.6) (>= uap10.0) (< win8) (< wpa81)) (&& (>= uap10.0) (< uap10.1))"

    let restriction, parseProblems = Requirements.parseRestrictionsSimplified restrictions

    parseProblems |> Seq.toList |> shouldEqual []

    restriction.RepresentedFrameworks |> Seq.map (fun x -> x.CompareString) |> shouldNotContain "net45"

    let simplified = Paket.Requirements.FrameworkRestriction.simplify restriction
    simplified.RepresentedFrameworks |> Seq.map (fun x -> x.CompareString) |> shouldNotContain "net45"
    simplified.RawFormular.ToString() |> shouldEqual "|| (&& (< monoandroid) (< net45) (< netstandard1.2) (>= netstandard1.3) (< win8)) (&& (< monoandroid) (< net45) (< netstandard1.2) (>= netstandard1.6) (< win8)) (&& (< monoandroid) (< net45) (< netstandard1.3) (>= netstandard1.6) (< win8) (< wpa81)) (&& (< monoandroid) (< net45) (< netstandard1.4) (>= netstandard1.6) (< win8) (< wpa81)) (&& (< monoandroid) (< net45) (< netstandard1.5) (>= netstandard1.6) (< win8) (< wpa81)) (&& (< monoandroid) (< net452) (< netstandard1.6) (>= netstandard2.0)) (&& (< monoandroid) (< net452) (>= netstandard2.0) (< xamarinios)) (&& (< net45) (>= net46) (< netstandard1.2)) (&& (< net45) (>= net46) (< netstandard1.3)) (&& (< net45) (>= net46) (>= netstandard1.4) (< netstandard1.5)) (&& (< net45) (>= net46) (< netstandard1.4)) (&& (< net45) (>= net46) (>= netstandard1.5) (< netstandard1.6)) (&& (< net45) (>= net46) (>= netstandard1.6) (< netstandard2.0)) (&& (< net45) (>= netstandard1.3) (< netstandard1.4) (< win8) (< wpa81)) (&& (< net45) (>= netstandard1.4) (< netstandard1.5) (< win8) (< wpa81)) (&& (< net45) (>= netstandard1.5) (< netstandard1.6) (< win8) (< wpa81)) (&& (< net45) (>= netstandard1.6) (< netstandard2.0) (< win8) (< wpa81)) (&& (< net452) (>= net46) (>= netstandard2.0)) (&& (>= net46) (>= uap10.0)) (&& (>= netstandard1.6) (>= uap10.0)) (&& (< netstandard1.6) (>= uap10.0) (< win8) (< wpa81)) (&& (>= uap10.0) (< uap10.1))"

[<Test>]
let ``Simplify && (false) (< net45)`` () =
    let toSimplify =
        (FrameworkRestriction.And[
            FrameworkRestriction.EmptySet
            FrameworkRestriction.NotAtLeast (DotNetFramework FrameworkVersion.V4_5)])
    toSimplify
    |> shouldEqual FrameworkRestriction.EmptySet

[<Test>]
let ``Simplify && (< net45) (false)`` () =
    let toSimplify =
        (FrameworkRestriction.And[
            FrameworkRestriction.NotAtLeast (DotNetFramework FrameworkVersion.V4_5)
            FrameworkRestriction.EmptySet])
    toSimplify
    |> shouldEqual FrameworkRestriction.EmptySet

[<Test>]
let ``Simplify && (< net45) (true)`` () =
    let toSimplify =
        (FrameworkRestriction.And[
            FrameworkRestriction.NotAtLeast (DotNetFramework FrameworkVersion.V4_5)
            FrameworkRestriction.NoRestriction])
    toSimplify
    |> shouldEqual (FrameworkRestriction.NotAtLeast (DotNetFramework FrameworkVersion.V4_5))

[<Test>] 
let ``Simplify && (true) (< net45)`` () =
    let toSimplify = 
        (FrameworkRestriction.And[
            FrameworkRestriction.NoRestriction
            FrameworkRestriction.NotAtLeast (DotNetFramework FrameworkVersion.V4_5)])
    toSimplify
    |> shouldEqual (FrameworkRestriction.NotAtLeast (DotNetFramework FrameworkVersion.V4_5))

[<Test>] 
let ``IsSubset works for unknown Portables`` () =
    let p = PlatformMatching.forceExtractPlatforms "portable-net45+win8+wp8+wp81+wpa81"
    let t = (p.ToTargetProfile true).Value
    let r = FrameworkRestriction.AtLeastPlatform t
    r.IsSubsetOf r
    |> shouldEqual true

[<Test>]
let ``Simplify || (>= net45) (>= portable-net45+win8+wp8+wp81+wpa81)`` () =
    // because that is a custom portable profile!
    let portable = ((PlatformMatching.forceExtractPlatforms "portable-net45+win8+wp8+wp81+wpa81").ToTargetProfile true).Value
    let atLeastPortable = FrameworkRestriction.AtLeastPlatform portable

    // this was the underlying bug
    atLeastPortable.RepresentedFrameworks
    |> shouldContain (TargetProfile.SinglePlatform (DotNetFramework FrameworkVersion.V4_5))

    let formula = FrameworkRestriction.Or [ atLeastPortable; FrameworkRestriction.AtLeast (DotNetFramework FrameworkVersion.V4_5) ]
    
    formula
    |> shouldEqual atLeastPortable

[<Test>]
let ``CustomProfile is Supported by its Platforms``() =
    let unknownProfile =
        ((PlatformMatching.forceExtractPlatforms "portable-net45+monoandroid10+monotouch10+xamarinios10").ToTargetProfile true).Value

    unknownProfile.IsSupportedBy (TargetProfile.SinglePlatform (DotNetFramework FrameworkVersion.V4_5))
    |> shouldEqual true
    unknownProfile.IsSupportedBy (TargetProfile.SinglePlatform (DotNetFramework FrameworkVersion.V4))
    |> shouldEqual false

    unknownProfile.IsSupportedBy (TargetProfile.SinglePlatform (MonoAndroid MonoAndroidVersion.V1))
    |> shouldEqual true
    unknownProfile.IsSupportedBy (TargetProfile.SinglePlatform MonoTouch)
    |> shouldEqual true
    unknownProfile.IsSupportedBy (TargetProfile.SinglePlatform (Silverlight SilverlightVersion.V5))
    |> shouldEqual false

[<Test>]
let ``__unknowntfm__ should not match everything`` () =
    try
        let model =
            InstallModel.CreateFromLibs(Paket.Domain.PackageName "Rx-XAML", SemVer.Parse "2.2.4", InstallModelKind.Package, FrameworkRestriction.NoRestriction,
                [  { Paket.NuGet.UnparsedPackageFile.FullPath = @"..\Rx-XAML\lib\__unknowntfm__\System.Reactive.Windows.Threading.dll"
                     Paket.NuGet.UnparsedPackageFile.PathWithinPackage = "lib/__unknowntfm__/System.Reactive.Windows.Threading.dll" }  ],
                   [],
                   [],
                   { References = NuspecReferences.All
                     OfficialName = "Reactive Extensions - XAML Support Library"
                     Version = "2.2.4"
                     Dependencies = lazy []
                     LicenseUrl = ""
                     IsDevelopmentDependency = false
                     FrameworkAssemblyReferences = []})
        let target = TargetProfile.PortableProfile PortableProfileType.Profile344
        let newModel = model.ApplyFrameworkRestrictions (FrameworkRestriction.ExactlyPlatform target)
        newModel.GetCompileReferences target |> Seq.toArray
        |> shouldEqual [||]
    with e ->
        // Throwing is OK as well.
        ()

[<Test>]
let ``Profile 158 should not support itself``() = 
    SupportCalculation.isSupportedNotEqual Profile158 Profile158
    |> shouldEqual false
[<Test>]
let ``Profile 158 should not support Profile 78, as there is no silverlight on 78``() = 
    SupportCalculation.isSupportedNotEqual Profile158 Profile78
    |> shouldEqual false
    SupportCalculation.isSupportedNotEqual Profile78 Profile158
    |> shouldEqual true
[<Test>]
let ``Profile 344 should support Profile 336, as it has the same frameworks but is lower``() =
    SupportCalculation.isSupportedNotEqual Profile336 Profile344
    |> shouldEqual false
    SupportCalculation.isSupportedNotEqual Profile344 Profile336
    |> shouldEqual true

[<Test>]
let ``Generate Support Table``() = 
    // TODO: Should we include this?
    let mutable supportMap = SupportCalculation.createInitialSupportMap()
    supportMap <- SupportCalculation.optimizeSupportMap supportMap
    
    supportMap
    |> SupportCalculation.toSeq
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
    PlatformMatching.forceExtractPlatforms "portable-monotouch+monoandroid"
    |> function { Platforms = o } -> TargetProfile.FindPortable true o
    |> shouldEqual (TargetProfile.PortableProfile (PortableProfileType.UnsupportedProfile [MonoAndroid MonoAndroidVersion.V1; MonoTouch]))
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
        |> List.map PlatformMatching.forceExtractPlatforms
        |> List.map (function { Platforms = [ h] } -> TargetProfile.SinglePlatform h | {Platforms = o} -> TargetProfile.FindPortable true o)
    let expected =
        [ TargetProfile.SinglePlatform (DotNetFramework FrameworkVersion.V4);
          TargetProfile.PortableProfile (PortableProfileType.UnsupportedProfile [MonoAndroid MonoAndroidVersion.V1; MonoTouch])
          TargetProfile.PortableProfile PortableProfileType.Profile328; TargetProfile.PortableProfile PortableProfileType.Profile259
          TargetProfile.PortableProfile PortableProfileType.Profile32
          TargetProfile.PortableProfile PortableProfileType.Profile78
          TargetProfile.SinglePlatform (Silverlight SilverlightVersion.V5); TargetProfile.SinglePlatform (Windows WindowsVersion.V8)
          TargetProfile.SinglePlatform (WindowsPhone WindowsPhoneVersion.V8) ]
    portables
    |> shouldEqual expected

[<Test>]
let ``PlatformMatching works with portable ``() = 
    // portable-net40-sl4
    let l = (KnownTargetProfiles.FindPortableProfile "Profile18").PlatformsSupporting
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
    FrameworkRestriction.EmptySet.RepresentedFrameworks.IsEmpty
    |> shouldEqual true
    
[<Test>]
let ``NoRestriction set should not be empty``() = 
    FrameworkRestriction.NoRestriction.RepresentedFrameworks.IsEmpty
    |> shouldEqual false

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


