module Paket.ProjectFile.ConditionSpecs

open Paket
open NUnit.Framework
open FsUnit

let element x = 
    match x with 
    | Some y -> y
    | None -> failwith "not found"

[<Test>]
let ``should detect framework version from path``() =
    FrameworkDetection.DetectFromPath(@"..\RestSharp\lib\net4\RestSharp.dll")|> element |> shouldEqual (DotNetFramework(FrameworkVersion.V4))
    FrameworkDetection.DetectFromPath(@"..\Rx-Main\lib\net40\Rx.dll")|> element |> shouldEqual (DotNetFramework(FrameworkVersion.V4))
    FrameworkDetection.DetectFromPath(@"..\Rx-Main\lib\net45\Rx.dll")|> element |> shouldEqual (DotNetFramework(FrameworkVersion.V4_5))
    FrameworkDetection.DetectFromPath(@"..\Rx-Main\lib\net20\Rx.dll")|> element |> shouldEqual (DotNetFramework(FrameworkVersion.V2))
    FrameworkDetection.DetectFromPath(@"..\Rx-Main\lib\net35\Rx.dll")|> element |> shouldEqual (DotNetFramework(FrameworkVersion.V3_5))
    FrameworkDetection.DetectFromPath("../Rx-Main/lib/net35/Rx.dll")|> element |> shouldEqual (DotNetFramework(FrameworkVersion.V3_5))
    FrameworkDetection.DetectFromPath(@"..\NUnit\lib\NUnit.dll")|> element |> shouldEqual (DotNetFramework(FrameworkVersion.V1))
    FrameworkDetection.DetectFromPath("../NUnit/lib/NUnit.dll")|> element |> shouldEqual (DotNetFramework(FrameworkVersion.V1))

[<Test>]
let ``should detect CLR version from path``() =
    FrameworkDetection.DetectFromPath(@"..\Rx-log4net\lib\1.0\log4net.dll")|> element |> shouldEqual (DotNetFramework(FrameworkVersion.V1))
    FrameworkDetection.DetectFromPath(@"..\Rx-log4net\lib\1.1\log4net.dll")|> element |> shouldEqual (DotNetFramework(FrameworkVersion.V1_1))
    FrameworkDetection.DetectFromPath(@"..\Rx-log4net\lib\2.0\log4net.dll")|> element |> shouldEqual (DotNetFramework(FrameworkVersion.V2))

[<Test>]
let ``should detect client framework version from path``() =
    FrameworkDetection.DetectFromPath(@"..\packages\Castle.Core\lib\net40-client\Castle.Core.dll")|> element |> shouldEqual (DotNetFramework(FrameworkVersion.V4))

[<Test>]
let ``should detect client framework version from Lib path``() =
    FrameworkDetection.DetectFromPath(@"..\packages\Castle.Core\Lib\net40-client\Castle.Core.dll")|> element |> shouldEqual (DotNetFramework(FrameworkVersion.V4))

[<Test>]
let ``should detect net40-full as net40``() =
    FrameworkDetection.DetectFromPath(@"..\packages\log4net\lib\net40-full\log4net.dll")|> element |> shouldEqual(DotNetFramework(FrameworkVersion.V4))
    FrameworkDetection.DetectFromPath(@"..\packages\log4net\lib\net40\log4net.dll")|> element |> shouldEqual (DotNetFramework(FrameworkVersion.V4))

[<Test>]
let ``should detect net451``() =
    FrameworkDetection.DetectFromPath(@"..\Rx-Main\lib\net451\Rx.dll")|> element |> shouldEqual (DotNetFramework(FrameworkVersion.V4_5_1))

[<Test>]
let ``should detect Silverlight version from path``() =
    FrameworkDetection.DetectFromPath(@"..\..\packages\RestSharp\lib\sl5\RestSharp.Silverlight.dll")|> element |> shouldEqual (Silverlight SilverlightVersion.V5)
    FrameworkDetection.DetectFromPath(@"..\..\packages\RestSharp\lib\sl4\RestSharp.Silverlight.dll")|> element |> shouldEqual (Silverlight SilverlightVersion.V4)
    FrameworkDetection.DetectFromPath(@"..\..\packages\SpecFlow\lib\sl3\Specflow.Silverlight.dll")|> element |> shouldEqual (Silverlight SilverlightVersion.V3)

[<Test>]
let ``should detect WindowsPhone version from path``() =
    FrameworkDetection.DetectFromPath(@"..\..\packages\RestSharp\lib\sl4-wp75\RestSharp.WindowsPhone.dll")|> element |> shouldEqual (WindowsPhone WindowsPhoneVersion.V7_5)
    FrameworkDetection.DetectFromPath(@"..\..\packages\RestSharp\lib\sl4-wp71\RestSharp.WindowsPhone.dll")|> element |> shouldEqual (WindowsPhone WindowsPhoneVersion.V7_1)
    FrameworkDetection.DetectFromPath(@"..\..\packages\RestSharp\lib\sl4-wp\TechTalk.SpecFlow.WindowsPhone7.dll")|> element |> shouldEqual (WindowsPhone WindowsPhoneVersion.V7_1)

[<Test>]
let ``should detect framework version from uppercase path``() =
    FrameworkDetection.DetectFromPath(@"..\packages\GitVersion.1.2.0\Lib\Net45\GitVersionCore.dll")|> element |> shouldEqual (DotNetFramework(FrameworkVersion.V4_5))

[<Test>]
let ``should detect net45-full``() =
    FrameworkDetection.DetectFromPath(@"..\packages\Ninject\lib\net45-full\Ninject.dll")|> element |> shouldEqual (DotNetFramework(FrameworkVersion.V4_5))

[<Test>]
let ``should detect net``() =
    FrameworkDetection.DetectFromPath(@"..\packages\RhinoMocks\lib\net\Rhino.Mocks.dll")|> element |> shouldEqual (DotNetFramework(FrameworkVersion.V1))

[<Test>]
let ``should detect with spaces``() =
    FrameworkDetection.DetectFromPath(@"..\packages\FSharpx.Core\lib\.NetFramework 3.5\FSharp.Core.dll")|> element |> shouldEqual (DotNetFramework(FrameworkVersion.V3_5))

[<Test>]
let ``should detect 35, 40 and 45``() =
    FrameworkDetection.DetectFromPath(@"..\packages\FSharpx.Core\lib\35\FSharp.Core.dll")|> element |> shouldEqual (DotNetFramework(FrameworkVersion.V3_5))
    FrameworkDetection.DetectFromPath(@"..\packages\FSharpx.Core\lib\40\FSharp.Core.dll")|> element |> shouldEqual (DotNetFramework(FrameworkVersion.V4))
    FrameworkDetection.DetectFromPath(@"..\packages\FSharpx.Core\lib\45\FSharp.Core.dll")|> element |> shouldEqual (DotNetFramework(FrameworkVersion.V4_5))
    