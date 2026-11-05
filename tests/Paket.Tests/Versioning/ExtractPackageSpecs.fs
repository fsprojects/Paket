module Paket.ExtractPackageSpecs

open System.IO
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
        NuGetCache.ExtractPackage(fileName,di,PackageName "FSharp.Data",SemVer.Parse("0.1.1"),true)
        |> Async.RunSynchronously
    with 
    | exn -> exn.Message
    |> fun error -> error.Contains("firewall") |> shouldEqual true
[<Test>]
let ``should extract to user folder when another instance left the nupkg next to the package folder``() =
    let packageName = PackageName "Paket.Tests.ConcurrentExtract"
    let version = SemVer.Parse "1.0.0"
    let dir = NuGetCache.GetPackageUserFolderDir(packageName, version, PackageResolver.ResolvedPackageKind.Package)
    let parentDir = Path.GetDirectoryName dir
    if Directory.Exists parentDir then
        Directory.Delete(parentDir, true)
    Directory.CreateDirectory dir |> ignore
    let nupkgName = NuGetCache.GetPackageFileName packageName version
    let nupkg = Path.Combine(dir, nupkgName)
    do
        use zip = Compression.ZipFile.Open(nupkg, Compression.ZipArchiveMode.Create)
        use writer = new StreamWriter(zip.CreateEntry("Paket.Tests.ConcurrentExtract.nuspec").Open())
        writer.Write """<?xml version="1.0" encoding="utf-8"?>
<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
  <metadata>
    <id>Paket.Tests.ConcurrentExtract</id>
    <version>1.0.0</version>
    <authors>Paket</authors>
    <description>Test</description>
  </metadata>
</package>"""
    // the temporary copy of a concurrent extraction of the same package
    File.Copy(nupkg, Path.Combine(parentDir, nupkgName))

    try
        NuGetCache.ExtractPackageToUserFolder(PackageSources.DefaultNuGetSource, nupkg, packageName, version, PackageResolver.ResolvedPackageKind.Package)
        |> Async.RunSynchronously
        |> shouldEqual dir
        File.Exists nupkg |> shouldEqual true
        File.Exists(Path.Combine(dir, "paket.tests.concurrentextract.nuspec")) |> shouldEqual true
        Directory.GetFiles(parentDir, "*.paket_tmp") |> shouldBeEmpty
    finally
        Directory.Delete(parentDir, true)
