module Paket.IntegrationTests.HashSpecs

open Fake
open System
open NUnit.Framework
open FsUnit
open System
open System.IO
open System.IO.Compression
open System.Diagnostics
open Paket
open Paket.Domain

//let scenario = "i003014-add-github"

[<Test>]
let ``save hash on install``() =
    let scenario = "save-nupkg_hash-on-install"
    use __ = prepare scenario
    directPaket "install" scenario |> ignore<string>

    let lockFile = LockFile.LoadFrom(Path.Combine(scenarioTempPath scenario,"paket.lock"))
    
    let newtonsoft = lockFile.GetGroup(Constants.MainDependencyGroup).Resolution |> Map.find (PackageName "Newtonsoft.Json")
    newtonsoft.Settings.NupkgHash |> shouldEqual (Some "6934665f0479c58bbe996c44f2bf16d435a72f4d92795f0bc1d40cb0bc1358ff0e660ac20b24eabce01ee6145bd553506178e59fbaabd0f2a94b23bfa5c735f5")
    
[<Test>]
let ``verify bad hash on restore``() =
    let scenario = "verify-bad-nupkg_hash-on-restore"
    use __ = prepare scenario
    let ex =
        try
            directPaket "restore" scenario |> ignore<string>
            None
        with
        | ex -> Some(ex)

    match ex with
    | None -> failwith "restore should have failed due to hash mismatch"
    | Some(ex) ->
        ex.Message
        |> shouldContainText "Error when extracting nuget package Newtonsoft.Json, the hash of 6934665f0479c58bbe996c44f2bf16d435a72f4d92795f0bc1d40cb0bc1358ff0e660ac20b24eabce01ee6145bd553506178e59fbaabd0f2a94b23bfa5c735f5 did not match the pinned hash of 6934665f0479c58bbe996c44f2bf16d435a72f4d92795f0bc1d40cb0bc1358ff0e660ac20b24eabce01ee6145bd553506178e59fbaabd0f2a94b23bfa5c735f4"

[<Test>]
let ``verify good hash on restore``() =
    let scenario = "verify-good-nupkg_hash-on-restore"
    use __ = prepare scenario
    directPaket "restore" scenario |> ignore<string>
    
// this routine is similiar to old logic that was causing the hash
// of nupkg files to change
let fixDatesInArchive fileName =
    use zipToOpen = new FileStream(fileName, FileMode.Open)
    use archive = new ZipArchive(zipToOpen, ZipArchiveMode.Update)
    let maxTime = DateTimeOffset.Now

    for e in archive.Entries do
        e.LastWriteTime <- DateTimeOffset.Parse("2000-01-01T00:00:00.0000000-06:00")
    
[<Test>]
let ``verify old tainted nupkg is removed from cache``() =
    let scenario = "verify-good-nupkg_hash-on-restore"
    let scenarioTempPath = (scenarioTempPath scenario)
    use __ = prepare scenario
    
    let nupkgDir = NuGetCache.GetTargetUserFolder (PackageName "Newtonsoft.Json") (SemVer.Parse "12.0.3")
    
    deleteDir (DirectoryInfo nupkgDir)
    
    directPaket "restore" scenario |> ignore<string>
    
    deleteDir (DirectoryInfo (Path.Combine(scenarioTempPath, "packages")))
    deleteDir (DirectoryInfo (Path.Combine(scenarioTempPath, "paket-files")))
    File.Delete (Path.Combine(nupkgDir, ".paket.metadata"))
    
    let nupkg = NuGetCache.GetTargetUserNupkg (PackageName "Newtonsoft.Json") (SemVer.Parse "12.0.3")
    
    fixDatesInArchive nupkg
    
    let out = directPaket "restore" scenario
    if out.Contains("Removing Newtonsoft.Json 12.0.3 from cache due to nupkg modification") |> not then
        failwith "modified nupkg should have been removed from the cache"
