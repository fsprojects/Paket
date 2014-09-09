module Paket.ProjectFile.ConditionSpecs

open Paket
open NUnit.Framework
open FsUnit
open System.Linq

[<Test>]
let ``should detect framework version from path``() =
    FramworkCondition.DetectFromPath(@"..\RestSharp\lib\net4\RestSharp.dll").First().Framework |> shouldEqual (DotNetFramework(Framework "v4.0",Full))
    FramworkCondition.DetectFromPath(@"..\Rx-Main\lib\net40\Rx.dll").First().Framework |> shouldEqual (DotNetFramework(Framework "v4.0",Full))
    FramworkCondition.DetectFromPath(@"..\Rx-Main\lib\net45\Rx.dll").First().Framework |> shouldEqual (DotNetFramework(Framework "v4.5",Full))
    FramworkCondition.DetectFromPath(@"..\Rx-Main\lib\net20\Rx.dll").First().Framework |> shouldEqual (DotNetFramework(Framework "v2.0",Full))
    FramworkCondition.DetectFromPath(@"..\Rx-Main\lib\net35\Rx.dll").First().Framework |> shouldEqual (DotNetFramework(Framework "v3.5",Full))
    FramworkCondition.DetectFromPath("../Rx-Main/lib/net35/Rx.dll").First().Framework |> shouldEqual (DotNetFramework(Framework "v3.5",Full))
    FramworkCondition.DetectFromPath(@"..\NUnit\lib\NUnit.dll").First().Framework |> shouldEqual (DotNetFramework(All,Full))
    FramworkCondition.DetectFromPath("../NUnit/lib/NUnit.dll").First().Framework |> shouldEqual (DotNetFramework(All,Full))

[<Test>]
let ``should detect CLR version from path``() =
    FramworkCondition.DetectFromPath(@"..\Rx-log4net\lib\1.0\log4net.dll").First().CLRVersion |> shouldEqual (Some "1.0")
    FramworkCondition.DetectFromPath(@"..\Rx-log4net\lib\1.1\log4net.dll").First().CLRVersion |> shouldEqual (Some "1.1")
    FramworkCondition.DetectFromPath(@"..\Rx-log4net\lib\2.0\log4net.dll").First().CLRVersion |> shouldEqual (Some "2.0")
    FramworkCondition.DetectFromPath(@"..\Rx-log4net\lib\2.0\log4net.dll").First().Framework |> shouldEqual (DotNetFramework(All,Full))


[<Test>]
let ``should detect client framework version from path``() =
    FramworkCondition.DetectFromPath(@"..\packages\Castle.Core\lib\net40-client\Castle.Core.dll").First().Framework |> shouldEqual (DotNetFramework(Framework "v4.0",Client))

[<Test>]
let ``should detect net40-full as net40``() =
    FramworkCondition.DetectFromPath(@"..\packages\log4net\lib\net40-full\log4net.dll").First().Framework |> shouldEqual(DotNetFramework(Framework "v4.0",Full))
    FramworkCondition.DetectFromPath(@"..\packages\log4net\lib\net40\log4net.dll").First().Framework |> shouldEqual (DotNetFramework(Framework "v4.0",Full))

[<Test>]
let ``should detect net451``() =
    FramworkCondition.DetectFromPath(@"..\Rx-Main\lib\net451\Rx.dll").First().Framework |> shouldEqual (DotNetFramework(Framework "v4.5.1",Full))

[<Test>]
let ``should detect Silverlight version from path``() =
    FramworkCondition.DetectFromPath(@"..\..\packages\RestSharp\lib\sl5\RestSharp.Silverlight.dll").First().Framework |> shouldEqual (Silverlight("v5.0"))
    FramworkCondition.DetectFromPath(@"..\..\packages\RestSharp\lib\sl4\RestSharp.Silverlight.dll").First().Framework |> shouldEqual (Silverlight("v4.0"))
    FramworkCondition.DetectFromPath(@"..\..\packages\SpecFlow\lib\sl3\Specflow.Silverlight.dll").First().Framework |> shouldEqual (Silverlight("v3.0"))

[<Test>]
let ``should detect WindowsPhone version from path``() =
    FramworkCondition.DetectFromPath(@"..\..\packages\RestSharp\lib\sl4-wp71\RestSharp.WindowsPhone.dll").First().Framework |> shouldEqual (WindowsPhoneApp("7.1"))
    FramworkCondition.DetectFromPath(@"..\..\packages\RestSharp\lib\sl4-wp\TechTalk.SpecFlow.WindowsPhone7.dll").First().Framework |> shouldEqual (WindowsPhoneApp("7.1"))

[<Test>]
let ``should detect framework version from uppercase path``() =
    FramworkCondition.DetectFromPath(@"..\packages\GitVersion.1.2.0\Lib\Net45\GitVersionCore.dll").First().Framework |> shouldEqual (DotNetFramework(Framework "v4.5",Full))

[<Test>]
let ``should detect net45-full``() =
    FramworkCondition.DetectFromPath(@"..\packages\Ninject\lib\net45-full\Ninject.dll").First().Framework |> shouldEqual (DotNetFramework(Framework "v4.5",Full))

[<Test>]
let ``should detect net``() =
    FramworkCondition.DetectFromPath(@"..\packages\RhinoMocks\lib\net\Rhino.Mocks.dll").First().Framework |> shouldEqual (DotNetFramework(All,Full))

[<Test>]
let ``should detect multi-libs``() =
    FramworkCondition.DetectFromPath(@"..\packages\Janitor.Fody\Lib\portable-net4+sl5+wp8+win8+wpa81+MonoAndroid16+MonoTouch40\Janitor.dll").First().Framework |> shouldEqual (DotNetFramework(Framework "v4.0",Full))
    FramworkCondition.DetectFromPath(@"..\packages\Janitor.Fody\Lib\portable-net4+sl5+wp8+win8+wpa81+MonoAndroid16+MonoTouch40\Janitor.dll").Skip(1).First().Framework |> shouldEqual (Silverlight "v5.0")
