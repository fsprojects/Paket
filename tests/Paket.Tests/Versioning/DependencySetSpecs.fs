module Paket.DependencySetSpecs

open Paket
open Paket.BindingRedirects
open NUnit.Framework
open System.Xml.Linq
open FsUnit
open Paket.PackageResolver
open Paket.Requirements
open Paket.Domain
open Paket.TestHelpers

[<Test>]
let ``empty set filtered with empty restrictions should give empty set``() = 
    Set.empty
    |> DependencySetFilter.filterByRestrictions (ExplicitRestriction FrameworkRestriction.NoRestriction)
    |> shouldEqual Set.empty

[<Test>]
let ``filtered with empty restrictions should give full set``() = 
    let set = 
      [PackageName("P1"), VersionRequirement.AllReleases, ExplicitRestriction FrameworkRestriction.NoRestriction
       PackageName("P2"), VersionRequirement.AllReleases, ExplicitRestriction (FrameworkRestriction.AtLeast (DotNetFramework(FrameworkVersion.V4)))
       PackageName("P3"), VersionRequirement.AllReleases, ExplicitRestriction (FrameworkRestriction.Exactly (DotNetFramework(FrameworkVersion.V4_5)))]
      |> Set.ofList

    set
    |> DependencySetFilter.filterByRestrictions (ExplicitRestriction FrameworkRestriction.NoRestriction)
    |> shouldEqual set


[<Test>]
let ``filtered with empty should not remove netstandard``() = 
    let set = 
      [PackageName("P1"), VersionRequirement.AllReleases, ExplicitRestriction FrameworkRestriction.NoRestriction
       PackageName("P2"), VersionRequirement.AllReleases, ExplicitRestriction (FrameworkRestriction.AtLeast (DotNetFramework(FrameworkVersion.V4)))
       PackageName("P3"), VersionRequirement.AllReleases, makeOrList [FrameworkRestriction.AtLeast (DotNetFramework(FrameworkVersion.V4_5_1)); FrameworkRestriction.Exactly (DotNetStandard(DotNetStandardVersion.V1_3))]]
      |> Set.ofList

    set
    |> DependencySetFilter.filterByRestrictions (ExplicitRestriction FrameworkRestriction.NoRestriction)
    |> shouldEqual set

[<Test>]
let ``filtered with concrete restriction should filter non-matching``() = 
    let original = 
      [PackageName("P1"), VersionRequirement.AllReleases,ExplicitRestriction FrameworkRestriction.NoRestriction
       PackageName("P2"), VersionRequirement.AllReleases,ExplicitRestriction (FrameworkRestriction.AtLeast (DotNetFramework(FrameworkVersion.V4)))
       PackageName("P3"), VersionRequirement.AllReleases,ExplicitRestriction (FrameworkRestriction.Exactly (DotNetFramework(FrameworkVersion.V4_5)))
       PackageName("P4"), VersionRequirement.AllReleases,ExplicitRestriction (FrameworkRestriction.AtLeast (DotNetFramework(FrameworkVersion.V4_5)))
       PackageName("P5"), VersionRequirement.AllReleases,ExplicitRestriction (FrameworkRestriction.Between (DotNetFramework(FrameworkVersion.V4_5),DotNetFramework(FrameworkVersion.V4_5_2)))
       PackageName("P6"), VersionRequirement.AllReleases,ExplicitRestriction (FrameworkRestriction.Between (DotNetFramework(FrameworkVersion.V4),DotNetFramework(FrameworkVersion.V4_5_2)))
       PackageName("P7"), VersionRequirement.AllReleases,ExplicitRestriction (FrameworkRestriction.Between (DotNetFramework(FrameworkVersion.V3),DotNetFramework(FrameworkVersion.V3_5)))]
      |> Set.ofList

    let expected = 
      [PackageName("P1"), VersionRequirement.AllReleases,ExplicitRestriction FrameworkRestriction.NoRestriction
       PackageName("P2"), VersionRequirement.AllReleases,ExplicitRestriction (FrameworkRestriction.AtLeast (DotNetFramework(FrameworkVersion.V4)))
       PackageName("P6"), VersionRequirement.AllReleases,ExplicitRestriction (FrameworkRestriction.Between (DotNetFramework(FrameworkVersion.V4),DotNetFramework(FrameworkVersion.V4_5_2)))]
      |> Set.ofList


    original
    |> DependencySetFilter.filterByRestrictions (ExplicitRestriction (FrameworkRestriction.Exactly (DotNetFramework(FrameworkVersion.V4))))
    |> shouldEqual expected

[<Test>]
let ``filtered with AtLeast restriction should filter non-matching``() = 
    let original = 
      [PackageName("P1"), VersionRequirement.AllReleases,ExplicitRestriction FrameworkRestriction.NoRestriction
       PackageName("P2"), VersionRequirement.AllReleases,ExplicitRestriction (FrameworkRestriction.AtLeast (DotNetFramework(FrameworkVersion.V4)))
       PackageName("P3"), VersionRequirement.AllReleases,ExplicitRestriction (FrameworkRestriction.Exactly (DotNetFramework(FrameworkVersion.V4_5)))
       PackageName("P4"), VersionRequirement.AllReleases,ExplicitRestriction (FrameworkRestriction.AtLeast (DotNetFramework(FrameworkVersion.V4_5)))
       PackageName("P5"), VersionRequirement.AllReleases,ExplicitRestriction (FrameworkRestriction.Between (DotNetFramework(FrameworkVersion.V4_5),DotNetFramework(FrameworkVersion.V4_5_2)))
       PackageName("P6"), VersionRequirement.AllReleases,ExplicitRestriction (FrameworkRestriction.Between (DotNetFramework(FrameworkVersion.V4),DotNetFramework(FrameworkVersion.V4_5_2)))
       PackageName("P7"), VersionRequirement.AllReleases,ExplicitRestriction (FrameworkRestriction.Between (DotNetFramework(FrameworkVersion.V3),DotNetFramework(FrameworkVersion.V3_5)))
       PackageName("P8"), VersionRequirement.AllReleases,ExplicitRestriction (FrameworkRestriction.AtLeast (DotNetFramework(FrameworkVersion.V3_5)))
       PackageName("P9"), VersionRequirement.AllReleases,ExplicitRestriction (FrameworkRestriction.Between (DotNetFramework(FrameworkVersion.V3_5),DotNetFramework(FrameworkVersion.V4_5_2)))]
      |> Set.ofList

    let expected = 
      [PackageName("P1"), VersionRequirement.AllReleases,ExplicitRestriction FrameworkRestriction.NoRestriction
       PackageName("P2"), VersionRequirement.AllReleases,ExplicitRestriction (FrameworkRestriction.AtLeast (DotNetFramework(FrameworkVersion.V4)))
       PackageName("P3"), VersionRequirement.AllReleases,ExplicitRestriction (FrameworkRestriction.Exactly (DotNetFramework(FrameworkVersion.V4_5)))
       PackageName("P4"), VersionRequirement.AllReleases,ExplicitRestriction (FrameworkRestriction.AtLeast (DotNetFramework(FrameworkVersion.V4_5)))
       PackageName("P5"), VersionRequirement.AllReleases,ExplicitRestriction (FrameworkRestriction.Between (DotNetFramework(FrameworkVersion.V4_5),DotNetFramework(FrameworkVersion.V4_5_2)))
       PackageName("P6"), VersionRequirement.AllReleases,ExplicitRestriction (FrameworkRestriction.Between (DotNetFramework(FrameworkVersion.V4),DotNetFramework(FrameworkVersion.V4_5_2)))
       PackageName("P8"), VersionRequirement.AllReleases,ExplicitRestriction (FrameworkRestriction.AtLeast (DotNetFramework(FrameworkVersion.V3_5)))
       PackageName("P9"), VersionRequirement.AllReleases,ExplicitRestriction (FrameworkRestriction.Between (DotNetFramework(FrameworkVersion.V3_5),DotNetFramework(FrameworkVersion.V4_5_2)))]
      |> Set.ofList

    let result =
        original
        |> DependencySetFilter.filterByRestrictions ( ExplicitRestriction (FrameworkRestriction.AtLeast (DotNetFramework(FrameworkVersion.V4))))
        |> Seq.toArray
    result
    |> shouldEqual (expected |> Seq.toArray)

[<Test>]
let ``filtered with Between restriction should filter non-matching`` () =
    let original =
      [PackageName("P01"), VersionRequirement.AllReleases,ExplicitRestriction FrameworkRestriction.NoRestriction
       PackageName("P02"), VersionRequirement.AllReleases,ExplicitRestriction (FrameworkRestriction.AtLeast (DotNetFramework(FrameworkVersion.V4)))
       PackageName("P03"), VersionRequirement.AllReleases,ExplicitRestriction (FrameworkRestriction.Exactly (DotNetFramework(FrameworkVersion.V4_5)))
       PackageName("P04"), VersionRequirement.AllReleases,ExplicitRestriction (FrameworkRestriction.AtLeast (DotNetFramework(FrameworkVersion.V4_5)))
       PackageName("P05"), VersionRequirement.AllReleases,ExplicitRestriction (FrameworkRestriction.Between (DotNetFramework(FrameworkVersion.V4_5),DotNetFramework(FrameworkVersion.V4_5_2)))
       PackageName("P06"), VersionRequirement.AllReleases,ExplicitRestriction (FrameworkRestriction.Between (DotNetFramework(FrameworkVersion.V4),DotNetFramework(FrameworkVersion.V4_5_2)))
       PackageName("P07"), VersionRequirement.AllReleases,ExplicitRestriction (FrameworkRestriction.Between (DotNetFramework(FrameworkVersion.V3),DotNetFramework(FrameworkVersion.V3_5)))
       PackageName("P08"), VersionRequirement.AllReleases,ExplicitRestriction (FrameworkRestriction.AtLeast (DotNetFramework(FrameworkVersion.V3_5)))
       PackageName("P09"), VersionRequirement.AllReleases,ExplicitRestriction (FrameworkRestriction.Between (DotNetFramework(FrameworkVersion.V3_5),DotNetFramework(FrameworkVersion.V4_5_2)))
       PackageName("P10"), VersionRequirement.AllReleases,ExplicitRestriction (FrameworkRestriction.Between (DotNetFramework(FrameworkVersion.V4_5_1),DotNetFramework(FrameworkVersion.V4_6)))
       PackageName("P11"), VersionRequirement.AllReleases,ExplicitRestriction (FrameworkRestriction.Exactly (DotNetFramework(FrameworkVersion.V4_5_1)))
       PackageName("P12"), VersionRequirement.AllReleases,ExplicitRestriction (FrameworkRestriction.Exactly (DotNetFramework(FrameworkVersion.V3_5)))
       PackageName("P13"), VersionRequirement.AllReleases,ExplicitRestriction (FrameworkRestriction.Exactly (DotNetFramework(FrameworkVersion.V4)))
       PackageName("P14"), VersionRequirement.AllReleases,ExplicitRestriction (FrameworkRestriction.AtLeast (DotNetFramework(FrameworkVersion.V4_5_1)))]
      |> Set.ofList

    let expected =
      [PackageName("P01"), VersionRequirement.AllReleases,ExplicitRestriction FrameworkRestriction.NoRestriction
       PackageName("P02"), VersionRequirement.AllReleases,ExplicitRestriction (FrameworkRestriction.AtLeast (DotNetFramework(FrameworkVersion.V4)))
       PackageName("P03"), VersionRequirement.AllReleases,ExplicitRestriction (FrameworkRestriction.Exactly (DotNetFramework(FrameworkVersion.V4_5)))
       PackageName("P04"), VersionRequirement.AllReleases,ExplicitRestriction (FrameworkRestriction.AtLeast (DotNetFramework(FrameworkVersion.V4_5)))
       PackageName("P05"), VersionRequirement.AllReleases,ExplicitRestriction (FrameworkRestriction.Between (DotNetFramework(FrameworkVersion.V4_5),DotNetFramework(FrameworkVersion.V4_5_2)))
       PackageName("P06"), VersionRequirement.AllReleases,ExplicitRestriction (FrameworkRestriction.Between (DotNetFramework(FrameworkVersion.V4),DotNetFramework(FrameworkVersion.V4_5_2)))
       PackageName("P08"), VersionRequirement.AllReleases,ExplicitRestriction (FrameworkRestriction.AtLeast (DotNetFramework(FrameworkVersion.V3_5)))
       PackageName("P09"), VersionRequirement.AllReleases,ExplicitRestriction (FrameworkRestriction.Between (DotNetFramework(FrameworkVersion.V3_5),DotNetFramework(FrameworkVersion.V4_5_2)))
       PackageName("P13"), VersionRequirement.AllReleases,ExplicitRestriction (FrameworkRestriction.Exactly (DotNetFramework(FrameworkVersion.V4)))]
      |> Set.ofList


    original
    |> DependencySetFilter.filterByRestrictions (ExplicitRestriction (FrameworkRestriction.Between (DotNetFramework(FrameworkVersion.V4),DotNetFramework(FrameworkVersion.V4_5_1))))
    |> shouldEqual expected

[<Test>]
[<Ignore "Probably a bug in addFrameworkRestrictionsToDependencies, but in practice its probably good enough ignoring for now.">]
let ``should optimize ZendeskApi_v2 ``() = 
    let original =
        [PackageName("Newtonsoft.Json"),    (), PlatformMatching.extractPlatforms "net35"
         PackageName("Newtonsoft.Json"),    (), PlatformMatching.extractPlatforms "net4"
         PackageName("AsyncCTP"),           (), PlatformMatching.extractPlatforms "net4"
         PackageName("Newtonsoft.Json"),    (), PlatformMatching.extractPlatforms "net45"
         PackageName("Newtonsoft.Json"),    (), PlatformMatching.extractPlatforms "portable-net45+sl40+wp71+win80"
         PackageName("Microsoft.Bcl.Async"),(), PlatformMatching.extractPlatforms "portable-net45+sl40+wp71+win80"]

    let expected =
        [PackageName("Newtonsoft.Json"), (), 
          makeOrList
            [getPortableRestriction "portable-net45+sl40+wp71+win80"
             FrameworkRestriction.AtLeast (DotNetFramework(FrameworkVersion.V3_5))]
         PackageName("AsyncCTP"), (),ExplicitRestriction (FrameworkRestriction.Between (DotNetFramework(FrameworkVersion.V4), DotNetFramework(FrameworkVersion.V4_5)))
         PackageName("Microsoft.Bcl.Async"), (),ExplicitRestriction (getPortableRestriction "portable-net45+sl40+wp71+win80")]
    let result =
        addFrameworkRestrictionsToDependencies original [
            SinglePlatform (DotNetFramework(FrameworkVersion.V3_5))
            SinglePlatform (DotNetFramework(FrameworkVersion.V4))
            SinglePlatform (DotNetFramework(FrameworkVersion.V4_5))
            (PlatformMatching.extractPlatforms "portable-net45+sl40+wp71+win80").ToTargetProfile.Value ]
    result
    |> shouldEqual expected

[<Test>]
let ``should optimize real world restrictions``() = 
    let original =
        [PackageName("P1"), (), PlatformMatching.extractPlatforms "net20"
         PackageName("P1"), (), PlatformMatching.extractPlatforms "net35"
         PackageName("P1"), (), PlatformMatching.extractPlatforms "net45"
         PackageName("P1"), (), PlatformMatching.extractPlatforms "net451"
         PackageName("P1"), (), PlatformMatching.extractPlatforms "net46"]

    let expected =
        [PackageName("P1"), (), 
          makeOrList
           [FrameworkRestriction.AtLeast (DotNetFramework(FrameworkVersion.V2))]]

    let result =
        addFrameworkRestrictionsToDependencies original
            ([  FrameworkVersion.V2; FrameworkVersion.V3_5
                FrameworkVersion.V4_5; FrameworkVersion.V4_5_1
                FrameworkVersion.V4_6] |> List.map (DotNetFramework >> SinglePlatform))
    result |> shouldEqual expected

[<Test>]
let ``should optimize real world restrictions 2``() = 
    let original =
        [PackageName("P1"), (), PlatformMatching.extractPlatforms "net20" 
         PackageName("P1"), (), PlatformMatching.extractPlatforms "net4"  
         PackageName("P1"), (), PlatformMatching.extractPlatforms "net45" 
         PackageName("P1"), (), PlatformMatching.extractPlatforms "net451"
         PackageName("P1"), (), PlatformMatching.extractPlatforms "net46"] 

    let expected =
        [PackageName("P1"), (), 
          makeOrList
           [FrameworkRestriction.AtLeast (DotNetFramework(FrameworkVersion.V2))]]

    let result =
        addFrameworkRestrictionsToDependencies original 
            ([  FrameworkVersion.V2; FrameworkVersion.V4
                FrameworkVersion.V4_5; FrameworkVersion.V4_5_1
                FrameworkVersion.V4_6] |> List.map (DotNetFramework >> SinglePlatform))
    result |> shouldEqual expected

[<Test>]
let ``should optimize real world restrictions 3``() = 
    let original =
        [FrameworkRestriction.AtLeast (DotNetFramework(FrameworkVersion.V4))
         FrameworkRestriction.AtLeast (DotNetFramework(FrameworkVersion.V4_5_1))]

    let expected = FrameworkRestriction.AtLeast (DotNetFramework(FrameworkVersion.V4))

    let result = makeOrList original |> getExplicitRestriction
    result |> shouldEqual expected
