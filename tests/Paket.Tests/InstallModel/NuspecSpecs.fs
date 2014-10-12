module Paket.NuspecSpecs

open Paket
open NUnit.Framework
open FsUnit

[<Test>]
let ``can detect explicit references``() = 
    Nuspec.Load("TestFiles/FSharp.Data.nuspec").References
    |> shouldEqual (NuspecReferences.Explicit ["FSharp.Data.dll"])

[<Test>]
let ``can detect all references``() = 
    Nuspec.Load("TestFiles/Octokit.nuspec").References
    |> shouldEqual NuspecReferences.All

[<Test>]
let ``if nuspec is not found we assume all references``() = 
    Nuspec.Load("TestFiles/blablub.nuspec").References
    |> shouldEqual NuspecReferences.All

[<Test>]
let ``can detect explicit references for Fantomas``() = 
    Nuspec.Load("TestFiles/Fantomas.nuspec").References
    |> shouldEqual (NuspecReferences.Explicit ["FantomasLib.dll"])
