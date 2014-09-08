module Paket.ProjectFile.ConditionSpecs

open Paket
open NUnit.Framework
open FsUnit

[<Test>]
let ``should detect framework version from path``() =
    FramworkCondition.DetectFromPath(@"..\Rx-Main\lib\net40\Rx.dll").Framework|> shouldEqual (DotNetFramework(Framework "v4.0",Full))
    FramworkCondition.DetectFromPath(@"..\Rx-Main\lib\net45\Rx.dll").Framework|> shouldEqual (DotNetFramework(Framework "v4.5",Full))
    FramworkCondition.DetectFromPath(@"..\Rx-Main\lib\net20\Rx.dll").Framework|> shouldEqual (DotNetFramework(Framework "v2.0",Full))
    FramworkCondition.DetectFromPath(@"..\Rx-Main\lib\net35\Rx.dll").Framework|> shouldEqual (DotNetFramework(Framework "v3.5",Full))
    FramworkCondition.DetectFromPath("../Rx-Main/lib/net35/Rx.dll").Framework|> shouldEqual (DotNetFramework(Framework "v3.5",Full))
    FramworkCondition.DetectFromPath(@"..\NUnit\lib\NUnit.dll").Framework|> shouldEqual (DotNetFramework(All,Full))
    FramworkCondition.DetectFromPath("../NUnit/lib/NUnit.dll").Framework|> shouldEqual (DotNetFramework(All,Full))

[<Test>]
let ``should detect CLR version from path``() =
    FramworkCondition.DetectFromPath(@"..\Rx-log4net\lib\1.0\log4net.dll").CLRVersion |> shouldEqual (Some "1.0")
    FramworkCondition.DetectFromPath(@"..\Rx-log4net\lib\1.1\log4net.dll").CLRVersion |> shouldEqual (Some "1.1")
    FramworkCondition.DetectFromPath(@"..\Rx-log4net\lib\2.0\log4net.dll").CLRVersion |> shouldEqual (Some "2.0")
    FramworkCondition.DetectFromPath(@"..\Rx-log4net\lib\2.0\log4net.dll").Framework|> shouldEqual (DotNetFramework(All,Full))


[<Test>]
let ``should detect client framework version from path``() =
    FramworkCondition.DetectFromPath(@"..\packages\Castle.Core\lib\net40-client\Castle.Core.dll").Framework|> shouldEqual (DotNetFramework(Framework "v4.0",Client))

[<Test>]
let ``should detect net40-full as net40``() =
    FramworkCondition.DetectFromPath(@"..\packages\log4net\lib\net40-full\log4net.dll").Framework|> shouldEqual(DotNetFramework(Framework "v4.0",Full))
    FramworkCondition.DetectFromPath(@"..\packages\log4net\lib\net40\log4net.dll").Framework|> shouldEqual (DotNetFramework(Framework "v4.0",Full))

[<Test>]
let ``should detect net451 as special case of 4.5``() =
    FramworkCondition.DetectFromPath(@"..\Rx-Main\lib\net451\Rx.dll").Framework|> shouldEqual (DotNetFramework(FrameworkExtension("v4.5","v4.5.1"),Full))

[<Test>]
let ``should detect Silverlight version from path``() =
    FramworkCondition.DetectFromPath(@"..\..\packages\RestSharp\lib\sl4\RestSharp.Silverlight.dll").Framework|> shouldEqual (Silverlight("v4.0"))
    FramworkCondition.DetectFromPath(@"..\..\packages\SpecFlow\lib\sl3\Specflow.Silverlight.dll").Framework|> shouldEqual (Silverlight("v3.0"))

[<Test>]
let ``should detect WindowsPhone version from path``() =
    FramworkCondition.DetectFromPath(@"..\..\packages\RestSharp\lib\sl4-wp71\RestSharp.WindowsPhone.dll").Framework|> shouldEqual (WindowsPhoneApp("7.1"))
    FramworkCondition.DetectFromPath(@"..\..\packages\RestSharp\lib\sl4-wp\TechTalk.SpecFlow.WindowsPhone7.dll").Framework|> shouldEqual (WindowsPhoneApp("7.1"))
