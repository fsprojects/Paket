module Paket.NuspecSpecs

open Paket
open NUnit.Framework
open FsUnit
open Nuspec
open Paket.InstallModelSpecs

[<Test>]
let ``can detect explicit references``() = 
    Nuspec.GetReferences "TestFiles/FSharp.Data.nuspec"
    |> shouldEqual (References.Explicit ["FSharp.Data.dll"])

[<Test>]
let ``can detect all references``() = 
    Nuspec.GetReferences "TestFiles/Octokit.nuspec"
    |> shouldEqual References.All

[<Test>]
let ``if nuspec is not found we assume all references``() = 
    Nuspec.GetReferences "TestFiles/blablub.nuspec"
    |> shouldEqual References.All

[<Test>]
let ``can detect explicit references for Fantomas``() = 
    Nuspec.GetReferences "TestFiles/Fantomas.nuspec"
    |> shouldEqual (References.Explicit ["FantomasLib.dll"])
