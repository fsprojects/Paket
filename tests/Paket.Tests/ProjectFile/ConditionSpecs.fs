module Paket.ProjectFile.ConditionSpecs

open Paket
open NUnit.Framework
open FsUnit

[<Test>]
let ``should detect framework version from path``() =
    FramworkCondition.DetectFromPath(@"..\Rx-Main\lib\net40\Rx.dll").FrameworkVersion |> shouldEqual (Framework "v4.0")
    FramworkCondition.DetectFromPath(@"..\Rx-Main\lib\net45\Rx.dll").FrameworkVersion |> shouldEqual (Framework "v4.5")
    FramworkCondition.DetectFromPath(@"..\Rx-Main\lib\net20\Rx.dll").FrameworkVersion |> shouldEqual (Framework "v2.0")
    FramworkCondition.DetectFromPath(@"..\Rx-Main\lib\net35\Rx.dll").FrameworkVersion |> shouldEqual (Framework "v3.5")
    FramworkCondition.DetectFromPath("../Rx-Main/lib/net35/Rx.dll").FrameworkVersion |> shouldEqual (Framework "v3.5")
    FramworkCondition.DetectFromPath(@"..\NUnit\lib\NUnit.dll").FrameworkVersion |> shouldEqual All
    FramworkCondition.DetectFromPath("../NUnit/lib/NUnit.dll").FrameworkVersion |> shouldEqual All

    FramworkCondition.DetectFromPath(@"..\Rx-log4net\lib\1.0\log4net.dll").FrameworkVersion |> shouldEqual (Framework "v1.0")
    FramworkCondition.DetectFromPath(@"..\Rx-log4net\lib\1.1\log4net.dll").FrameworkVersion |> shouldEqual (Framework "v1.1")
    FramworkCondition.DetectFromPath(@"..\Rx-log4net\lib\2.0\log4net.dll").FrameworkVersion |> shouldEqual (Framework "v2.0")


[<Test>]
let ``should detect client framework version from path``() =
    FramworkCondition.DetectFromPath(@"..\packages\Castle.Core\lib\net40-client\Castle.Core.dll").FrameworkVersion |> shouldEqual (Framework "v4.0")
    FramworkCondition.DetectFromPath(@"..\packages\Castle.Core\lib\net40-client\Castle.Core.dll").FrameworkProfile |> shouldEqual Client

[<Test>]
let ``should detect net40-full as net40``() =
    FramworkCondition.DetectFromPath(@"..\packages\log4net\lib\net40-full\log4net.dll").FrameworkVersion |> shouldEqual (Framework "v4.0")
    FramworkCondition.DetectFromPath(@"..\packages\log4net\lib\net40-full\log4net.dll").FrameworkProfile |> shouldEqual Full
   
    FramworkCondition.DetectFromPath(@"..\packages\log4net\lib\net40\log4net.dll").FrameworkVersion |> shouldEqual (Framework "v4.0")
    FramworkCondition.DetectFromPath(@"..\packages\log4net\lib\net40\log4net.dll").FrameworkProfile |> shouldEqual Full

[<Test>]
let ``should detect net451 as special case of 4.5``() =
    FramworkCondition.DetectFromPath(@"..\Rx-Main\lib\net451\Rx.dll").FrameworkVersion |> shouldEqual (FrameworkExtension("v4.5","v4.5.1") )