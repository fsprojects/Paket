module Paket.InstallModel.FrameworkIdentifierSpecs

open Paket
open NUnit.Framework
open FsUnit

#nowarn "0044" //  Warning FS0044 This construct is deprecated. Use PlatformMatching.extractPlatforms instead

[<Test>]
let ``should understand basic framework versions net20, net40, net45 ...``() = 
    FrameworkDetection.Extract("net20").Value |> shouldEqual (DotNetFramework(FrameworkVersion.V2))
    FrameworkDetection.Extract("net40").Value |> shouldEqual (DotNetFramework(FrameworkVersion.V4))
    FrameworkDetection.Extract("net45").Value |> shouldEqual (DotNetFramework(FrameworkVersion.V4_5))

[<Test>]
let ``should understand basic silverlight``() = 
    FrameworkDetection.Extract("sl").Value |> shouldEqual (Silverlight SilverlightVersion.V3)
    FrameworkDetection.Extract("sl3").Value |> shouldEqual (Silverlight SilverlightVersion.V3)
    FrameworkDetection.Extract("sl4").Value |> shouldEqual (Silverlight SilverlightVersion.V4)

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
    FrameworkDetection.Extract("xamarinios10").Value |> shouldEqual XamariniOS

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
    FrameworkDetection.Extract("xamarinmac20").Value |> shouldEqual XamarinMac

[<Test>]
let ``should serialize xamarinmac``() =
    XamarinMac.ToString() |> shouldEqual "xamarinmac"