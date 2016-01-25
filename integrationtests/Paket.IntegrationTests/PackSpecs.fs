module Paket.IntegrationTests.PackSpecs

open Fake
open System
open NUnit.Framework
open FsUnit
open System
open System.IO
open System.Diagnostics
open System.IO.Compression
open Paket.Domain
open Paket

[<Test>]
let ``#1234 empty assembly name``() = 
    let outPath = Path.Combine(scenarioTempPath "i001234-missing-assemblyname","out")
    try
        paket ("pack -v output \"" + outPath + "\"") "i001234-missing-assemblyname" |> ignore
        failwith "Expected an exeption"
    with
    | exn when exn.Message.Contains("PaketBug.dll") -> ()

    File.Delete(Path.Combine(scenarioTempPath "i001234-missing-assemblyname","PaketBug","paket.template"))

[<Test>]
let ``#1348 npm type folder names`` () =
    let rootPath = scenarioTempPath "i001348-packaging-npm-type-folders"
    let outPath = Path.Combine(rootPath,"out")
    let package = Path.Combine(outPath, "Paket.Integrations.Npm.1.0.0.nupkg")
    
    paket ("pack -v output \"" + outPath + "\"") "i001348-packaging-npm-type-folders" |> ignore 
    ZipFile.ExtractToDirectory(package, outPath)

    let desiredFolderName = "font-awesome@4.5.0"
    
    let extractedFolder = 
        Path.Combine(outPath,"jspm_packages", "npm") 
        |> directoryInfo 
        |> subDirectories 
        |> Array.head
    
    extractedFolder.Name |> shouldEqual desiredFolderName

    CleanDir rootPath

[<Test>]
let ``#1375 pack specific dependency``() = 
    let outPath = Path.Combine(scenarioTempPath "i001375-pack-specific","out")
    paket ("pack -v output \"" + outPath + "\"") "i001375-pack-specific" |> ignore

    File.Delete(Path.Combine(scenarioTempPath "i001375-pack-specific","PaketBug","paket.template"))

[<Test>]
let ``#1375 pack with projectUrl commandline``() = 
    let outPath = Path.Combine(scenarioTempPath "i001375-pack-specific","out")
    paket ("pack -v output \"" + outPath + "\" project-url \"http://localhost\"") "i001375-pack-specific" |> ignore

    File.Delete(Path.Combine(scenarioTempPath "i001375-pack-specific","PaketBug","paket.template"))

[<Test>]
let ``#1376 fail template``() = 
    let outPath = Path.Combine(scenarioTempPath "i001376-pack-template","out")
    let templatePath = Path.Combine(scenarioTempPath "i001376-pack-template","PaketBug", "paket.template")
    paket ("pack -v output \"" + outPath + "\" templatefile " + templatePath) "i001376-pack-template" |> ignore
    let fileInfo = FileInfo(Path.Combine(outPath, "PaketBug.1.0.0.0.nupkg"))
    let (expectedFileSize: int64) = int64(1542)
    fileInfo.Length |> shouldBeGreaterThan expectedFileSize

    File.Delete(Path.Combine(scenarioTempPath "i001376-pack-template","PaketBug","paket.template"))

[<Test>]
let ``#1429 pack deps from template``() = 
    let outPath = Path.Combine(scenarioTempPath "i001429-pack-deps","out")
    let templatePath = Path.Combine(scenarioTempPath "i001429-pack-deps","PaketBug", "paket.template")
    paket ("pack -v output \"" + outPath + "\" templatefile " + templatePath) "i001429-pack-deps" |> ignore

    let details = 
        NuGetV2.getDetailsFromLocalNuGetPackage outPath "" (PackageName "PaketBug") (SemVer.Parse "1.0.0.0")
        |> Async.RunSynchronously

    details.Dependencies |> List.map (fun (x,_,_) -> x) |> shouldContain (PackageName "MySql.Data")
    details.Dependencies |> List.map (fun (x,_,_) -> x) |> shouldNotContain (PackageName "PaketBug2") // it's not packed in same round
    details.Dependencies |> List.map (fun (x,_,_) -> x) |> shouldNotContain (PackageName "PaketBug")

    File.Delete(Path.Combine(scenarioTempPath "i001429-pack-deps","PaketBug","paket.template"))

[<Test>]
let ``#1429 pack deps``() = 
    let outPath = Path.Combine(scenarioTempPath "i001429-pack-deps","out")
    let templatePath = Path.Combine(scenarioTempPath "i001429-pack-deps","PaketBug", "paket.template")
    paket ("pack -v output \"" + outPath + "\"") "i001429-pack-deps" |> ignore

    let details = 
        NuGetV2.getDetailsFromLocalNuGetPackage outPath "" (PackageName "PaketBug") (SemVer.Parse "1.0.0.0")
        |> Async.RunSynchronously

    details.Dependencies |> List.map (fun (x,_,_) -> x) |> shouldContain (PackageName "MySql.Data")
    details.Dependencies |> List.map (fun (x,_,_) -> x) |> shouldContain (PackageName "PaketBug2")
    details.Dependencies |> List.map (fun (x,_,_) -> x) |> shouldNotContain (PackageName "PaketBug")

    File.Delete(Path.Combine(scenarioTempPath "i001429-pack-deps","PaketBug","paket.template"))