namespace Paket.InstallModel

open Paket
open NUnit.Framework
open FsUnit
open Paket.PlatformMatching

module ``Given a target platform`` =

    [<Test>]
    let ``it should return no penalty for the same platform``() =
        getFrameworkPenalty (DotNetFramework FrameworkVersion.V4_5, DotNetFramework FrameworkVersion.V4_5)
        |> shouldEqual 0

    [<Test>]
    let ``it should return the right penalty for a compatible platform``() =
        getFrameworkPenalty (DotNetFramework FrameworkVersion.V4_5, DotNetFramework FrameworkVersion.V4)
        |> shouldEqual 2

    [<Test>]
    let ``it should return > 1000 for an incompatible platform``() =
        getFrameworkPenalty (DotNetFramework FrameworkVersion.V4_5, Silverlight SilverlightVersion.V5)
         |> shouldBeGreaterThan MaxPenalty

    [<Test>]
    let ``it should prefer .net proper``() =
        let p1 = getFrameworkPenalty (DotNetFramework FrameworkVersion.V4_6_2, DotNetFramework FrameworkVersion.V4_5_1)
        let p2 = getFrameworkPenalty (DotNetFramework FrameworkVersion.V4_6_2, DotNetStandard DotNetStandardVersion.V1_5)
        p1 |> shouldBeSmallerThan p2

module ``Given a path`` =
    [<Test>]
    let ``it should split it into the right platforms``() =
        extractPlatforms "net40+win8"
        |> shouldEqual
            { Platforms = [ DotNetFramework FrameworkVersion.V4; Windows WindowsVersion.V8 ]
              Name = "net40+win8" }

    [<Test>]
    let ``it should ignore 'portable-'``() =
        extractPlatforms "portable-net40+win8"
        |> shouldEqual
            { Platforms = [ DotNetFramework FrameworkVersion.V4; Windows WindowsVersion.V8 ]
              Name = "portable-net40+win8" }

    [<Test>]
    let ``it should return no penalty for a matching .NET framework``() =
        let path = extractPlatforms "net45"
        getFrameworkPathPenalty [ DotNetFramework FrameworkVersion.V4_5 ] path |> shouldEqual 0

    [<Test>]
    let ``it should return no penalty for a matching portable profile``() =
        let path = extractPlatforms "net40+sl4"
        getFrameworkPathPenalty 
            [ DotNetFramework FrameworkVersion.V4
              Silverlight SilverlightVersion.V4 ] path
        |> shouldEqual 0

    [<Test>]
    let ``it should return 1 for a compatible portable profile``() =
        let path = extractPlatforms "net40+sl4"
        getFrameworkPathPenalty 
            [ DotNetFramework FrameworkVersion.V4
              Silverlight SilverlightVersion.V5 ] path
        |> shouldEqual 1

    [<Test>]
    let ``it should return the correct penalty for compatible .NET Frameworks``() =
        let path = extractPlatforms "net20"
        getFrameworkPathPenalty [ DotNetFramework FrameworkVersion.V2 ] path |> shouldEqual 0
        getFrameworkPathPenalty [ DotNetFramework FrameworkVersion.V3 ] path |> shouldEqual 1
        getFrameworkPathPenalty [ DotNetFramework FrameworkVersion.V3_5 ] path |> shouldEqual 2
        getFrameworkPathPenalty [ DotNetFramework FrameworkVersion.V4 ] path |> shouldEqual 3

module ``Given an empty path`` =
    [<Test>]
    let ``it should be okay to use from .NET``() =
        let path = extractPlatforms ""
        getFrameworkPathPenalty [ DotNetFramework FrameworkVersion.V4_5 ] path |> shouldBeSmallerThan 2000

    [<Test>]
    let ``it should be okay to use from a portable profile``() =
        let path = extractPlatforms ""
        getFrameworkPathPenalty [ DotNetFramework FrameworkVersion.V4_5; Windows WindowsVersion.V8; WindowsPhoneApp WindowsPhoneAppVersion.V8_1 ] path |> shouldBeSmallerThan 2000

module ``Given a list of paths`` =
    let paths =
        [ "net40"; "portable-monotouch+monoandroid"; "portable-net40+sl5+win8+wp8+wpa81"
          "portable-net45+winrt45+wp8+wpa81"; "portable-win81+wpa81"; "portable-windows8+net45+wp8"; "sl5"; "win8"
          "wp8" ]
        |> List.map extractPlatforms
    let find n =
        paths
        |> Seq.find (fun p -> p.Name = n)

    [<Test>]
    let ``it should find the best match for .NET 4.0``() =
        findBestMatch (paths, SinglePlatform(DotNetFramework FrameworkVersion.V4)) |> shouldEqual (find "net40" |> Some)

    [<Test>]
    let ``it should find the best match for Silverlight 5``() =
        findBestMatch (paths, SinglePlatform(Silverlight SilverlightVersion.V5)) |> shouldEqual (find "sl5"|> Some)

    [<Test>]
    let ``it should find no match for Silverlight 4``() =
        findBestMatch (paths, SinglePlatform(Silverlight SilverlightVersion.V4)) |> shouldEqual None

    [<Test>]
    let ``it should prefer (older) full .NET frameworks over portable class libraries``() =
        findBestMatch (paths, SinglePlatform(DotNetFramework FrameworkVersion.V4_5)) |> shouldEqual (find "net40"|> Some)

    module ``when I get the supported target profiles`` =
        let supportedTargetProfiles = getSupportedTargetProfiles paths

        [<Test>]
        let ``it should contain profile 32``() =
            supportedTargetProfiles.[find "portable-win81+wpa81"]
            |> shouldContain (KnownTargetProfiles.FindPortableProfile "Profile32")

        [<Test>]
        let ``it should not contain profile 41``() =
            let flattend =
                seq {
                    for item in supportedTargetProfiles do
                        yield! item.Value
                }
            flattend |> shouldNotContain (KnownTargetProfiles.FindPortableProfile "Profile41")

module ``ProfileAnalyzer tests`` =
    ()
    //[<Test>]
    //let ``test that we cannot detect portable-net40+sl4+win8+wp71+wpa81`` () =
    //    let res = Paket.PlatformMatching.tryGetProfile (extractPlatforms "portable-net40+sl4+win8+wp71+wpa81")
    //    res |> shouldEqual None

module ``General Penalty checks`` =
    [<Test>]
    let ``prefer net20 over emtpy folder``()=
        Paket.PlatformMatching.findBestMatch ([""; "net20"] |> List.map extractPlatforms, SinglePlatform(DotNetFramework(FrameworkVersion.V4_6_1)))
        |> shouldEqual (Some (extractPlatforms "net20"))

    [<Test>]
    let ``best match for DotNet Standard 1.0``()=
        Paket.PlatformMatching.findBestMatch (["net20"; "net40"; "net45"; "net451"]|> List.map extractPlatforms, SinglePlatform(DotNetStandard(DotNetStandardVersion.V1_0)))
        |> shouldEqual (None)


    [<Test>]
    let ``best match for DotNet Standard 1.1``()=
        Paket.PlatformMatching.findBestMatch (["net20"; "net40"; "net45"; "net451"]|> List.map extractPlatforms, SinglePlatform(DotNetStandard(DotNetStandardVersion.V1_1)))
        |> shouldEqual (None)

    [<Test>]
    let ``best match for DotNet Standard 1.5``()=
        Paket.PlatformMatching.findBestMatch (["net20"; "net40"; "net45"; "net451"]|> List.map extractPlatforms, SinglePlatform(DotNetStandard(DotNetStandardVersion.V1_5)))
        |> shouldEqual (None)

    [<Test>]
    let ``best match for net45``()=
        Paket.PlatformMatching.findBestMatch
          (["netstandard10"; "netstandard11"; "netstandard12"; "netstandard13"; "netstandard14"; "netstandard15"; "netstandard16"]|> List.map extractPlatforms,
           SinglePlatform(DotNetFramework(FrameworkVersion.V4_5)))
        |> shouldEqual (Some (extractPlatforms "netstandard11"))


    [<Test>]
    let ``best match for netstandard in portable``()=
        Paket.PlatformMatching.findBestMatch
          (["portable-netcore451+wpa81"]|> List.map extractPlatforms,
           SinglePlatform(DotNetStandard(DotNetStandardVersion.V1_1)))
        |> shouldEqual (None)

        Paket.PlatformMatching.findBestMatch
          (["portable-netcore451+wpa81"]|> List.map extractPlatforms,
           SinglePlatform(DotNetStandard(DotNetStandardVersion.V1_2)))
        |> shouldEqual (Some (extractPlatforms "portable-netcore451+wpa81"))

        Paket.PlatformMatching.findBestMatch
          (["portable-netcore451+wpa81"]|> List.map extractPlatforms,
           SinglePlatform(DotNetStandard(DotNetStandardVersion.V1_3)))
        |> shouldEqual (Some (extractPlatforms "portable-netcore451+wpa81"))

        Paket.PlatformMatching.findBestMatch
          (["portable-netcore451+wpa81"]|> List.map extractPlatforms,
           SinglePlatform(DotNetStandard(DotNetStandardVersion.V1_4)))
        |> shouldEqual (Some (extractPlatforms "portable-netcore451+wpa81"))

        Paket.PlatformMatching.findBestMatch
          (["portable-netcore451+wpa81"]|> List.map extractPlatforms,
           SinglePlatform(DotNetStandard(DotNetStandardVersion.V1_5)))
        |> shouldEqual (Some (extractPlatforms "portable-netcore451+wpa81"))

        Paket.PlatformMatching.findBestMatch
          (["portable-netcore451+wpa81"]|> List.map extractPlatforms,
           SinglePlatform(DotNetStandard(DotNetStandardVersion.V1_6)))
        |> shouldEqual (Some (extractPlatforms "portable-netcore451+wpa81"))

    [<Test>]
    let ``best match for netstandard, netstandard is preferred``()=
        Paket.PlatformMatching.findBestMatch
          (["portable-win81+wpa81"; "netstandard1.3"]|> List.map extractPlatforms,
           SinglePlatform(DotNetStandard(DotNetStandardVersion.V1_2)))
        |> shouldEqual (Some (extractPlatforms "portable-win81+wpa81"))

        Paket.PlatformMatching.findBestMatch
          (["portable-win81+wpa81"; "netstandard1.3"]|> List.map extractPlatforms,
           SinglePlatform(DotNetStandard(DotNetStandardVersion.V1_3)))
        |> shouldEqual (Some (extractPlatforms "netstandard1.3"))

        Paket.PlatformMatching.findBestMatch
          (["portable-win81+wpa81"; "netstandard1.3"]|> List.map extractPlatforms,
           SinglePlatform(DotNetStandard(DotNetStandardVersion.V1_4)))
        |> shouldEqual (Some (extractPlatforms "netstandard1.3"))

    [<Test>]
    let ``best match for netstandard, use possible.``()=
        Paket.PlatformMatching.findBestMatch
          // Profile31 (supports netstandard1.0),  Profile32 (supports netstandard1.2)
          (["portable-netcore451+wp81"; "portable-netcore451+wpa81"]|> List.map extractPlatforms,
           SinglePlatform(DotNetStandard(DotNetStandardVersion.V1_0)))
        |> shouldEqual (Some (extractPlatforms "portable-netcore451+wp81"))

        Paket.PlatformMatching.findBestMatch
          // Profile31 (supports netstandard1.0),  Profile32 (supports netstandard1.2)
          (["portable-netcore451+wp81"; "portable-netcore451+wpa81"]|> List.map extractPlatforms,
           SinglePlatform(DotNetStandard(DotNetStandardVersion.V1_2)))
        |> shouldEqual (Some (extractPlatforms "portable-netcore451+wpa81"))

        Paket.PlatformMatching.findBestMatch
          // Profile31 (supports netstandard1.0),  Profile32 (supports netstandard1.2)
          (["portable-netcore451+wp81"; "portable-netcore451+wpa81"]|> List.map extractPlatforms,
           SinglePlatform(DotNetStandard(DotNetStandardVersion.V1_4)))
        |> shouldEqual (Some (extractPlatforms "portable-netcore451+wpa81"))

    [<Test>]
    let ``make sure not all portable profiles match``()=
        // Not all portable profiles have a match.
        Paket.PlatformMatching.findBestMatch
          (["portable-net45+win8"]|> List.map extractPlatforms,
           SinglePlatform(DotNetStandard(DotNetStandardVersion.V1_0)))
        |> shouldEqual (None)

    [<Test>]
    let ``best match for net451``()=
        Paket.PlatformMatching.findBestMatch
          (["netstandard10"; "netstandard11"; "netstandard12"; "netstandard13"; "netstandard14"; "netstandard15"; "netstandard16"]|> List.map extractPlatforms,
           SinglePlatform(DotNetFramework(FrameworkVersion.V4_5_1)))
        |> shouldEqual (Some (extractPlatforms "netstandard12"))

    [<Test>]
    let ``best match for net463``()=
        Paket.PlatformMatching.findBestMatch
          (["netstandard10"; "netstandard11"; "netstandard12"; "netstandard13"; "netstandard14"; "netstandard15"; "netstandard16"]|> List.map extractPlatforms,
           SinglePlatform(DotNetFramework(FrameworkVersion.V4_6_3)))
        |> shouldEqual (Some (extractPlatforms "netstandard16"))

