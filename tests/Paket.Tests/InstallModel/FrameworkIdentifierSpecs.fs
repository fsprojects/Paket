module Paket.InstallModel.FrameworkIdentifierSpecs

open Paket
open NUnit.Framework
open FsUnit

[<Test>]
let ``should understand basic framework versions net20, net40, net45 ...``() = 
    FrameworkDetection.Extract("net20").Value |> shouldEqual (DotNetFramework(FrameworkVersion.V2))
    FrameworkDetection.Extract("net40").Value |> shouldEqual (DotNetFramework(FrameworkVersion.V4_Client))
    FrameworkDetection.Extract("net45").Value |> shouldEqual (DotNetFramework(FrameworkVersion.V4_5))

[<Test>]
let ``should serialize basic framework versions net20, net40, net45 ...``() = 
    DotNetFramework(FrameworkVersion.V2).ToString() |> shouldEqual "net20"
    DotNetFramework(FrameworkVersion.V4_Client).ToString() |> shouldEqual "net40"
    DotNetFramework(FrameworkVersion.V4_5).ToString() |> shouldEqual "net45"