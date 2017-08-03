module Paket.ExtractPackageSpecs

open System.IO
open Pri.LongPath
open Paket
open NUnit.Framework
open FsUnit
open System
open System.Net
open Domain
open TestHelpers

[<Test>]
let ``should report blocked download``() =
    ensureDir()
    let di = Path.Combine(Path.GetTempPath(),"PaketTests/Extract")
    if Directory.Exists di then
        Directory.Delete(di,true)
    Directory.CreateDirectory(di) |> ignore
    let fileName = Path.Combine(di,"FSharp.Data.nupkg")
    File.Copy(__SOURCE_DIRECTORY__ + @"/../Nuspec/FSharp.Data.nuspec",fileName)
    
    try
        NuGetCache.ExtractPackage(fileName,di,PackageName "FSharp.Data",SemVer.Parse("0.1.1"),false,true)
        |> Async.RunSynchronously
    with 
    | exn -> exn.Message
    |> fun error -> error.Contains("firewall") |> shouldEqual true