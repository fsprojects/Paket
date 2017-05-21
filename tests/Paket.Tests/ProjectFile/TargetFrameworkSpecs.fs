module Paket.ProjectFile.TargetFrameworkSpecs

open Paket
open NUnit.Framework
open FsUnit
open Paket.TestHelpers

let TestData: obj[][] = 
    [|
        // project file name, 
        //  expected TargetProfile 
        //  expected TargetProfile.ToString
        //  expected TargetFramework
        [|"Project2.fsprojtest";
            (SinglePlatform(DotNetFramework FrameworkVersion.V4));
            "net40";
            (Some(DotNetFramework FrameworkVersion.V4))|];
        [|"Empty.fsprojtest";
            (SinglePlatform(DotNetFramework FrameworkVersion.V4));
            "net40-full";
            (Some(DotNetFramework FrameworkVersion.V4))|];
        [|"NewSilverlightClassLibrary.csprojtest";
            (SinglePlatform(Silverlight SilverlightVersion.V5));
            "sl50";
            (Some(Silverlight SilverlightVersion.V5))|];
        [|"FSharp.Core.Fluent-3.1.fsprojtest";
            (PortableProfile("Profile259", [ DotNetFramework FrameworkVersion.V4_5; Windows WindowsVersion.V8; WindowsPhone WindowsPhoneVersion.V8; WindowsPhoneApp WindowsPhoneAppVersion.V8_1 ]));
            "portable-net45+netcore45+wpa81+wp8+MonoAndroid1+MonoTouch1";
            (Some(DotNetFramework FrameworkVersion.V4_5))|];
    |]

[<Test>]
[<TestCaseSource("TestData")>]
let ``should detect the correct framework on test projects`` projectFile expectedProfile expectedProfileString expectedTargetFramework =
    ensureDir()
    let p = ProjectFile.TryLoad("./ProjectFile/TestData/" + projectFile).Value
    p.GetTargetProfile() |> shouldEqual expectedProfile
    p.GetTargetProfile().ToString() |> shouldEqual expectedProfileString
    p.GetTargetFramework() |> shouldEqual expectedTargetFramework

