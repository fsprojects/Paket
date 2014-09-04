module Paket.ProjectFileConditionSpecs

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
    
