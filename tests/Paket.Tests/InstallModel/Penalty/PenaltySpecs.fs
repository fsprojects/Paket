namespace Paket.InstallModel

open Paket
open NUnit.Framework
open FsUnit
open Paket.PlatformMatching

module ``Given a target platform`` = 
    [<Test>]
    let ``it should return no penalty for the same platform``() = 
        getPlatformPenalty (DotNetFramework FrameworkVersion.V4_5) (DotNetFramework FrameworkVersion.V4_5) 
        |> shouldEqual 0
    
    [<Test>]
    let ``it should return the right penalty for a compatible platform``() = 
        getPlatformPenalty (DotNetFramework FrameworkVersion.V4_5) (DotNetFramework FrameworkVersion.V4) 
        |> shouldEqual 1
    
    [<Test>]
    let ``it should return > 1000 for an incompatible platform``() = 
        getPlatformPenalty (DotNetFramework FrameworkVersion.V4_5) (Silverlight "v5.0")
         |> shouldBeGreaterThan maxPenalty

module ``Given a path`` = 
    [<Test>]
    let ``it should split it into the right platforms``() = 
        extractPlatforms "net40+win8" |> shouldEqual [| DotNetFramework FrameworkVersion.V4_Client
                                                        Windows "v4.5" |]
    
    [<Test>]
    let ``it should ignore 'portable-'``() = 
        extractPlatforms "portable-net40+win8" |> shouldEqual [| DotNetFramework FrameworkVersion.V4_Client
                                                                 Windows "v4.5" |]
    
    [<Test>]
    let ``it should return no penalty for a matching .NET framework``() = 
        getPenalty [ DotNetFramework FrameworkVersion.V4_5 ] "net45" |> shouldEqual 0
    
    [<Test>]
    let ``it should return no penalty for a matching portable profile``() = 
        getPenalty [ DotNetFramework FrameworkVersion.V4_Client
                     Silverlight "v4.0" ] "net40+sl4"
        |> shouldEqual 0
    
    [<Test>]
    let ``it should return 1 for a compatible portable profile``() = 
        getPenalty [ DotNetFramework FrameworkVersion.V4_Client
                     Silverlight "v5.0" ] "net40+sl4"
        |> shouldEqual 1
    
    [<Test>]
    let ``it should return the correct penalty for compatible .NET Frameworks``() = 
        let path = "net20"
        getPenalty [ DotNetFramework FrameworkVersion.V2 ] path |> shouldEqual 0
        getPenalty [ DotNetFramework FrameworkVersion.V3 ] path |> shouldEqual 1
        getPenalty [ DotNetFramework FrameworkVersion.V3_5 ] path |> shouldEqual 2
        getPenalty [ DotNetFramework FrameworkVersion.V4_Client ] path |> shouldEqual 3

module ``Given an empty path`` = 
    [<Test>]
    let ``it should be okay to use from .NET``() = 
        getPenalty [ DotNetFramework FrameworkVersion.V4_5 ] "" |> shouldBeSmallerThan 1000

    [<Test>]
    let ``it should be okay to use from a portable profile``() = 
        getPenalty [ DotNetFramework FrameworkVersion.V4_5; Windows "v4.5"; WindowsPhoneApp "v8.1" ] "" |> shouldBeSmallerThan 1000

module ``Given a list of paths`` = 
    let paths = 
        [ "net40"; "portable-monotouch+monoandroid"; "portable-net40+sl5+win8+wp8+wpa81"; 
          "portable-net45+winrt45+wp8+wpa81"; "portable-win81+wpa81"; "portable-windows8+net45+wp8"; "sl5"; "win8"; 
          "wp8" ]
    
    [<Test>]
    let ``it should find the best match for .NET 4.0``() = 
        findBestMatch paths (SinglePlatform(DotNetFramework FrameworkVersion.V4)) |> shouldEqual (Some "net40")
    
    [<Test>]
    let ``it should find the best match for Silverlight 5``() = 
        findBestMatch paths (SinglePlatform(Silverlight "v5.0")) |> shouldEqual (Some "sl5")
    
    [<Test>]
    let ``it should find no match for Silverlight 4``() = 
        findBestMatch paths (SinglePlatform(Silverlight "v4.0")) |> shouldEqual None
    
    [<Test>]
    let ``it should prefer (older) full .NET frameworks over portable class libraries``() = 
        findBestMatch paths (SinglePlatform(DotNetFramework FrameworkVersion.V4_5)) |> shouldEqual (Some "net40")
    
    module ``when I get the supported target profiles`` = 
        let supportedTargetProfiles = getSupportedTargetProfiles paths
        
        [<Test>]
        let ``it should contain profile 32``() = 
            supportedTargetProfiles.["portable-win81+wpa81"] 
            |> shouldContain (KnownTargetProfiles.FindPortableProfile "Profile32")
        
        [<Test>]
        let ``it should not contain profile 41``() = 
            let flattend =
                seq { 
                    for item in supportedTargetProfiles do
                        yield! item.Value
                }
            flattend |> shouldNotContain (KnownTargetProfiles.FindPortableProfile "Profile41")
