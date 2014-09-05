module Paket.ProjectFile.ConditionSpecs

open Paket
open NUnit.Framework
open FsUnit

[<Test>]
let ``should detect framework version from path``() =
    FramworkCondition.DetectFromPath(@"..\Rx-Main\lib\net40\Rx.dll").Framework |> shouldEqual (Framework "v4.0")
    FramworkCondition.DetectFromPath(@"..\Rx-Main\lib\net45\Rx.dll").Framework |> shouldEqual (Framework "v4.5")
    FramworkCondition.DetectFromPath(@"..\Rx-Main\lib\net20\Rx.dll").Framework |> shouldEqual (Framework "v2.0")
    FramworkCondition.DetectFromPath(@"..\Rx-Main\lib\net35\Rx.dll").Framework |> shouldEqual (Framework "v3.5")
    FramworkCondition.DetectFromPath("../Rx-Main/lib/net35/Rx.dll").Framework |> shouldEqual (Framework "v3.5")
    FramworkCondition.DetectFromPath(@"..\NUnit\lib\NUnit.dll").Framework |> shouldEqual All
    FramworkCondition.DetectFromPath("../NUnit/lib/NUnit.dll").Framework |> shouldEqual All

    FramworkCondition.DetectFromPath(@"..\Rx-log4net\lib\1.0\log4net.dll").Framework |> shouldEqual (Framework "v1.0")
    FramworkCondition.DetectFromPath(@"..\Rx-log4net\lib\1.1\log4net.dll").Framework |> shouldEqual (Framework "v1.1")
    FramworkCondition.DetectFromPath(@"..\Rx-log4net\lib\2.0\log4net.dll").Framework |> shouldEqual (Framework "v2.0")


[<Test>]
let ``should detect client framework version from path``() =
    // TODO: this is a temporary hack and needs to be fixed
    FramworkCondition.DetectFromPath(@"..\packages\Castle.Core\lib\net40-client\Castle.Core.dll").Framework |> shouldEqual (Framework "v4.0")

[<Test>]
let ``should detect net40-full as net40``() =
    FramworkCondition.DetectFromPath(@"..\packages\log4net\lib\net40-full\log4net.dll").Framework |> shouldEqual (Framework "v4.0")
   