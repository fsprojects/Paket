module Paket.ExtractPackageSpecs

open System.IO
open Paket
open NUnit.Framework
open FsUnit
open System
open System.Net
open Domain

[<Test>]
let ``should report blocked download``() =
    let di = Path.Combine(Path.GetTempPath(),"PaketTests/Extract")
    if Directory.Exists di then
        Directory.Delete(di,true)
    Directory.CreateDirectory(di) |> ignore
    let fileName = Path.Combine(di,"FSharp.Data.nupkg")
    File.Copy("Nuspec/FSharp.Data.nuspec",fileName)
    
    try
        NuGetV2.ExtractPackage(fileName,di,PackageName "FSharp.Data",SemVer.Parse("0.1.1"),true)
        |> Async.RunSynchronously

    with 
    | exn -> exn.Message
    |> fun error -> error.Contains("Package contains text:") |> shouldEqual true