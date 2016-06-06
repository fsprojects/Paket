module Paket.ProjectFile.TargetFrameworkSpecs

open Paket
open NUnit.Framework
open FsUnit

let TestData: obj[][] = 
    [|
        // project file name, expected TargetProfile 
        [|"Project2.fsprojtest";
            (SinglePlatform(DotNetFramework FrameworkVersion.V4_Client));
            "net40"|];
        [|"Empty.fsprojtest";
            (SinglePlatform(DotNetFramework FrameworkVersion.V4));
            "net40-full"|];
        [|"NewSilverlightClassLibrary.csprojtest";
            (SinglePlatform(Silverlight("v5.0")));
            "sl50"|];
        [|"FSharp.Core.Fluent-3.1.fsprojtest";
            (PortableProfile("Profile259", [ DotNetFramework FrameworkVersion.V4_5; Windows "v4.5"; WindowsPhoneSilverlight "v8.0"; WindowsPhoneApp "v8.1" ]));
            "portable-net45+netcore45+wpa81+wp8+MonoAndroid1+MonoTouch1"|];
    |]

[<Test>]
[<TestCaseSource("TestData")>]
let ``should detect the correct framework on test projects`` projectFile expectedProfile expectedProfileString =
    let p = ProjectFile.TryLoad("./ProjectFile/TestData/" + projectFile).Value
    p.GetTargetProfile() |> shouldEqual expectedProfile
    p.GetTargetProfile().ToString() |> shouldEqual expectedProfileString

