module Paket.InstallModel.FrameworkIdentifierSpecs

open Paket
open NUnit.Framework
open FsUnit

let private extract path = (PlatformMatching.forceExtractPlatforms path).Platforms

[<Test>]
let ``should understand basic framework versions net20, net40, net45 ...``() = 
    extract "net20" |> shouldEqual [ DotNetFramework(FrameworkVersion.V2) ]
    extract "net40" |> shouldEqual [ DotNetFramework(FrameworkVersion.V4) ]
    extract "net45" |> shouldEqual [ DotNetFramework(FrameworkVersion.V4_5) ]

[<Test>]
let ``should understand basic silverlight``() = 
    extract "sl" |> shouldEqual [ Silverlight SilverlightVersion.V3 ]
    extract "sl3" |> shouldEqual [ Silverlight SilverlightVersion.V3 ]
    extract "sl4" |> shouldEqual [ Silverlight SilverlightVersion.V4 ]

[<Test>]
let ``should serialize basic framework versions net20, net40, net45 ...``() = 
    DotNetFramework(FrameworkVersion.V2).ToString() |> shouldEqual "net20"
    DotNetFramework(FrameworkVersion.V4).ToString() |> shouldEqual "net40"
    DotNetFramework(FrameworkVersion.V4_5).ToString() |> shouldEqual "net45"

[<Test>]
let ``should serialize silverlight framework identifier correctly``() =
    (Silverlight SilverlightVersion.V5).ToString() |> shouldEqual "sl5"

[<Test>]
let ``should understand xamarinios``() =
    extract "xamarinios10" |> shouldEqual [ XamariniOS ]

[<Test>]
let ``should serialize xamarinios``() =
    XamariniOS.ToString() |> shouldEqual "xamarinios"

[<Test>]
let ``should serialize xamarintvos``() =
    XamarinTV.ToString() |> shouldEqual "xamarintvos"
    
[<Test>]
let ``should serialize xamarinwtchos``() =
    XamarinWatch.ToString() |> shouldEqual "xamarinwatchos"

[<Test>]
let ``should understand xamarinmac``() =
    extract "xamarinmac20" |> shouldEqual [ XamarinMac ]

[<Test>]
let ``should serialize xamarinmac``() =
    XamarinMac.ToString() |> shouldEqual "xamarinmac"

[<Test>]
let ``should understand net8.0-windows10.0.22621.0`` () =
    extract "net8.0-windows10.0.22621.0"
    |> shouldEqual [ DotNet8Windows Net8WindowsVersion.V10_0_22621_0 ]