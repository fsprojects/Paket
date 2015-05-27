module Paket.InstallModel.Xml.MicrosoftBclBuildSpecs

open Paket
open NUnit.Framework
open FsUnit
open Paket.Domain

[<Test>]
let ``should not install targets node for Microsoft.Bcl.Build``() = 
    let model =
        InstallModel.CreateFromLibs(PackageName "Microsoft.Bcl.Build", SemVer.Parse "1.0.21", [],
            [ ],
            [ @"..\Microsoft.Bcl.Build\build\Microsoft.Bcl.Build.Tasks.dll"; @"..\Microsoft.Bcl.Build\build\Microsoft.Bcl.Build.targets" ],
              Nuspec.All)
    
    model.GetTargetsFiles(SinglePlatform (DotNetFramework FrameworkVersion.V4)) |> shouldNotContain @"..\Microsoft.Bcl.Build\build\Microsoft.Bcl.Build.targets"

    let propertyNodes,_,_ = ProjectFile.Load("./ProjectFile/TestData/Empty.fsprojtest").Value.GenerateXml(model,true,true)

    propertyNodes |> Seq.length |> shouldEqual 0