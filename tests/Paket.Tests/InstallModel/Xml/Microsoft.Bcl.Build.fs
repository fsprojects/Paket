﻿module Paket.InstallModel.Xml.MicrosoftBclBuildSpecs

open Paket
open NUnit.Framework
open FsUnit
open Paket.Domain
open Paket.TestHelpers

[<Test>]
let ``should not install targets node for Microsoft.Bcl.Build``() =
    ensureDir()
    let model =
        InstallModel.CreateFromLibs(PackageName "Microsoft.Bcl.Build", SemVer.Parse "1.0.21", [],
            [ ],
            [ @"..\Microsoft.Bcl.Build\build\Microsoft.Bcl.Build.Tasks.dll"; @"..\Microsoft.Bcl.Build\build\Microsoft.Bcl.Build.targets" ]
            |> Paket.Tests.InstallModel.ProcessingSpecs.fromLegacyList @"..\Microsoft.Bcl.Build\",
            [],
              Nuspec.All)

    model.GetTargetsFiles(SinglePlatform (DotNetFramework FrameworkVersion.V4))
        |> Seq.map (fun f -> f.Path) |> shouldContain @"..\Microsoft.Bcl.Build\build\Microsoft.Bcl.Build.targets"

    let ctx = ProjectFile.TryLoad("./ProjectFile/TestData/Empty.fsprojtest").Value.GenerateXml(model, System.Collections.Generic.HashSet<_>(),Map.empty,Some true,false,KnownTargetProfiles.AllProfiles,None)

    ctx.FrameworkSpecificPropsNodes |> Seq.length |> shouldEqual 0
    ctx.FrameworkSpecificTargetsNodes |> Seq.length |> shouldEqual 0
    ctx.GlobalPropsNodes |> Seq.length |> shouldEqual 0
    ctx.GlobalTargetsNodes |> Seq.length |> shouldEqual 0
