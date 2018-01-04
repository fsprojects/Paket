module Paket.InstallNuGetContentsTests

open Paket
open NUnit.Framework
open FsUnit
open Paket
open Paket.Domain
open Paket.TestHelpers


[<Test>]
let ``Check that paths from tryFindFolder are correct``() =
    let content =
        { NuGet.NuGetPackageContent.Path = "/c/test/blub";
          NuGet.NuGetPackageContent.Spec = Nuspec.All
          NuGet.NuGetPackageContent.Content =
            [ NuGet.NuGetDirectory("lib", [ NuGet.NuGetFile("file.dll") ]) ] }
    let libFiles = NuGet.tryFindFolder "lib" content  |> fun d -> defaultArg d [] |> Seq.exactlyOne
    libFiles
        |> fun d -> { d with FullPath = d.FullPath.Replace ('\\', '/') }
        |> shouldEqual { NuGetCache.UnparsedPackageFile.FullPath = "/c/test/blub/lib/file.dll"; NuGetCache.UnparsedPackageFile.PathWithinPackage = "lib/file.dll" }


[<Test>]
let ``Check that we can detect _._ and ignore xml``() =
    let content =
        { NuGet.NuGetPackageContent.Path = "/c/test/blub";
          NuGet.NuGetPackageContent.Spec = Nuspec.All
          NuGet.NuGetPackageContent.Content =
            NuGet.ofFiles [
              "lib/net45/_._"
              "lib/net40/System.IO.xml"
              "lib/net40/System.IO.dll"
            ]}
    let model =
        InstallModel.EmptyModel (PackageName "testpackage", SemVer.Parse "1.0.0", InstallModelKind.Package)
        |> InstallModel.addNuGetFiles content

    model.GetCompileReferences (TargetProfile.SinglePlatform (FrameworkIdentifier.DotNetFramework FrameworkVersion.V4_5))
    |> Seq.map (fun f -> f.Path)
    |> Seq.toList
    |> shouldEqual [ ]

    model.GetCompileReferences (TargetProfile.SinglePlatform (FrameworkIdentifier.DotNetFramework FrameworkVersion.V4))
    |> Seq.map (fun f -> f.PathWithinPackage)
    |> Seq.toList
    |> shouldEqual [ "lib/net40/System.IO.dll" ]

    model.GetLegacyReferences (TargetProfile.SinglePlatform (FrameworkIdentifier.DotNetFramework FrameworkVersion.V4))
    |> Seq.map (fun f -> f.PathWithinPackage)
    |> Seq.toList
    |> shouldEqual [ "lib/net40/System.IO.dll" ]

    model.GetLibReferences (TargetProfile.SinglePlatform (FrameworkIdentifier.DotNetFramework FrameworkVersion.V4))
    |> Seq.map (fun f -> f.PathWithinPackage)
    |> Seq.toList
    |> shouldEqual [ "lib/net40/System.IO.dll" ]

[<Test>]
let ``Check that we can detect _._ and ignore xml in ref folder``() =
    let content =
        { NuGet.NuGetPackageContent.Path = "/c/test/blub";
          NuGet.NuGetPackageContent.Spec = Nuspec.All
          NuGet.NuGetPackageContent.Content =
            NuGet.ofFiles [
              "ref/net45/_._"
              "ref/net40/System.IO.xml"
              "ref/net40/System.IO.dll"
            ]}
    let model =
        InstallModel.EmptyModel (PackageName "testpackage", SemVer.Parse "1.0.0", InstallModelKind.Package)
        |> InstallModel.addNuGetFiles content

    model.GetCompileReferences (TargetProfile.SinglePlatform (FrameworkIdentifier.DotNetFramework FrameworkVersion.V4_5))
    |> Seq.map (fun f -> f.Path)
    |> Seq.toList
    |> shouldEqual [ ]

    model.GetCompileReferences (TargetProfile.SinglePlatform (FrameworkIdentifier.DotNetFramework FrameworkVersion.V4))
    |> Seq.map (fun f -> f.PathWithinPackage)
    |> Seq.toList
    |> shouldEqual [ "ref/net40/System.IO.dll" ]
