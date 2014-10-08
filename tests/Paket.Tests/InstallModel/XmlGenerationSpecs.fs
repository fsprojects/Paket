module Paket.InstallModel.XmlGenerationSpecs

open Paket
open NUnit.Framework
open FsUnit
open Paket.Nuspec

[<Test>]
let ``should generate Xml for Fantomas 1.5``() = 
    let model =
        InstallModel.CreateFromLibs("Fantomas", SemVer.parse "1.5.0",        
            [ @"..\Fantomas\lib\FantomasLib.dll" 
              @"..\Fantomas\lib\FSharp.Core.dll" 
              @"..\Fantomas\lib\Fantomas.exe" ],
              References.Explicit ["FantomasLib.dll"])

    model.GetFiles(DotNetFramework(All, Full)) |> shouldBeEmpty
    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V2, Full)) |> shouldContain @"..\Fantomas\lib\FantomasLib.dll" 

    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V3_5, Full)) |> shouldContain @"..\Fantomas\lib\FantomasLib.dll" 

    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4, Full)) |> shouldContain @"..\Fantomas\lib\FantomasLib.dll" 

    model.GetFiles(DotNetFramework(Framework FrameworkVersionNo.V4_5, Full)) |> shouldContain @"..\Fantomas\lib\FantomasLib.dll" 
