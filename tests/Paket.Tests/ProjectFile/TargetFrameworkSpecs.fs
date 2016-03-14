module Paket.ProjectFile.TargetFrameworkSpecs

open Paket
open NUnit.Framework
open FsUnit

[<Test>]
let ``should detect TargetFramework in Project2 proj file``() =
    ProjectFile.TryLoad("./ProjectFile/TestData/Project2.fsprojtest").Value.GetTargetProfile().ToString()
    |> shouldEqual "net40"

[<Test>]
let ``should detect net40 in empty proj file``() =
    ProjectFile.TryLoad("./ProjectFile/TestData/Empty.fsprojtest").Value.GetTargetProfile().ToString()
    |> shouldEqual "net40-full"

[<Test>]
let ``should detect silverlight framework in new silverlight project2``() =
    ProjectFile.TryLoad("./ProjectFile/TestData/NewSilverlightClassLibrary.csprojtest").Value.GetTargetProfile()
    |> shouldEqual (SinglePlatform(Silverlight("v5.0")))

[<Test>]
let ``should detect portable profile``() =
    ProjectFile.TryLoad("./ProjectFile/TestData/FSharp.Core.Fluent-3.1.fsprojtest").Value.GetTargetProfile()
    |> shouldEqual (PortableProfile("Profile259", [ DotNetFramework FrameworkVersion.V4_5; Windows "v4.5"; WindowsPhoneSilverlight "v8.0"; WindowsPhoneApp "v8.1" ]))
