module Paket.ProjectFile.TargetFrameworkSpecs

open Paket
open NUnit.Framework
open FsUnit

let TestData: obj[][] = 
    [|
        // project file name, 
        //  expected TargetProfile 
        //  expected TargetProfile.ToString
        //  expected TargetFramework
        [|"Project2.fsprojtest";
            (SinglePlatform(DotNetFramework FrameworkVersion.V4_Client));
            "net40";
            (Some(DotNetFramework FrameworkVersion.V4_Client))|];
        [|"Empty.fsprojtest";
            (SinglePlatform(DotNetFramework FrameworkVersion.V4));
            "net40-full";
            (Some(DotNetFramework FrameworkVersion.V4))|];
        [|"NewSilverlightClassLibrary.csprojtest";
            (SinglePlatform(Silverlight("v5.0")));
            "sl50";
            (Some(Silverlight "v5.0"))|];
        [|"FSharp.Core.Fluent-3.1.fsprojtest";
            (PortableProfile("Profile259", [ DotNetFramework FrameworkVersion.V4_5; Windows "v4.5"; WindowsPhoneSilverlight "v8.0"; WindowsPhoneApp "v8.1" ]));
            "portable-net45+netcore45+wpa81+wp8+MonoAndroid1+MonoTouch1";
            (Some(DotNetFramework FrameworkVersion.V4_5))|];
    |]

[<Test>]
[<TestCaseSource("TestData")>]
let ``should detect the correct framework on test projects`` projectFile expectedProfile expectedProfileString expectedTargetFramework =
    let p = ProjectFile.TryLoad("./ProjectFile/TestData/" + projectFile).Value
    p.GetTargetProfile() |> shouldEqual expectedProfile
    p.GetTargetProfile().ToString() |> shouldEqual expectedProfileString
    p.GetTargetFramework() |> shouldEqual expectedTargetFramework

