module Paket.IntegrationTests.PaketCoreSpecs

open Fake
open System
open NUnit.Framework
open FsUnit
open System
open System.IO
open System.Diagnostics
open Paket
open Chessie.ErrorHandling
open Paket.Domain

[<Test>]
let ``#1251 full installer demo``() = 
    prepare "i001251-installer-demo"
    let deps = """source https://nuget.org/api/v2
    nuget FAKE
    nuget FSharp.Formatting"""

    let dependenciesFile = DependenciesFile.FromCode(scenarioTempPath "i001251-installer-demo",deps)
    let force = false
    let packagesToInstall = 
        // get from references file
        [GroupName "Main",PackageName "FAKE"
         GroupName "Main",PackageName "FSharp.Formatting"] 

    let lockFile,_,_ = UpdateProcess.SelectiveUpdate(dependenciesFile, PackageResolver.UpdateMode.Install, SemVerUpdateMode.NoRestriction, force)
    let model = Paket.InstallProcess.CreateModel(Path.GetDirectoryName dependenciesFile.FileName, force, dependenciesFile, lockFile, Set.ofSeq packagesToInstall, Map.empty) |> Map.ofArray

    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "FAKE"].Version
    |> shouldBeGreaterThan (SemVer.Parse "4")

[<Test>]
let ``#1251 install FSharp.Collections.ParallelSeq``() = 
    prepare "i001251-installer-demo"
    let deps = """source https://nuget.org/api/v2
    nuget FSharp.Collections.ParallelSeq"""

    let dependenciesFile = DependenciesFile.FromCode(scenarioTempPath "i001251-installer-demo",deps)
    let force = false
    let packagesToInstall = 
        // get from references file
        [GroupName "Main",PackageName "FSharp.Collections.ParallelSeq"] 

    let lockFile,_,_ = UpdateProcess.SelectiveUpdate(dependenciesFile, PackageResolver.UpdateMode.Install, SemVerUpdateMode.NoRestriction, force)

    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "FSharp.Collections.ParallelSeq"].Version
    |> shouldBeGreaterThan (SemVer.Parse "1.0.1")

[<Test>]
let ``#1259 install via script``() = 
    prepare "i001259-install-script"

    Paket.Dependencies
       .Install("""
    source https://nuget.org/api/v2
    nuget FSharp.Data
    nuget Suave
""", path = scenarioTempPath "i001259-install-script")


    let lockFile = LockFile.LoadFrom(Path.Combine(scenarioTempPath "i001259-install-script","paket.lock"))
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Suave"].Version
    |> shouldBeGreaterThan (SemVer.Parse "0.33.0")

[<Test>]
let ``#1341 http dlls``() = 
    prepare "i001341-http-dlls"
    let root = scenarioTempPath "i001341-http-dlls"
    let deps = sprintf """group Files

http file:///%s/library.dll library/library.dll""" (root.Replace("\\","/"))

    File.WriteAllText(Path.Combine(root,"paket.dependencies"),deps)

    directPaket "update -v" "i001341-http-dlls" |> ignore
    
    let newFile = Path.Combine(scenarioTempPath "i001341-http-dlls","HttpDependencyToProjectReference","HttpDependencyToProjectReference.csproj")
    let oldFile = Path.Combine(originalScenarioPath "i001341-http-dlls","HttpDependencyToProjectReference","HttpDependencyToProjectReference.csprojtemplate")
    let s1 = File.ReadAllText oldFile |> normalizeLineEndings
    let s2 = File.ReadAllText newFile |> normalizeLineEndings
    s2 |> shouldEqual s1
[<Test>]
let ``should calculate hash when enabled``() = 
    prepare "hash-calculation-on"

    Paket.Dependencies.Install("""
    hash: on
    source https://nuget.org/api/v2
    nuget Owin 1.0.0
    """, path = scenarioTempPath "hash-calculation-on")

    let lockfile = LockFile.LoadFrom(Path.Combine(scenarioTempPath "hash-calculation-on", "paket.lock"))
    lockfile.Groups.[Constants.MainDependencyGroup].Options.Settings.UseHash |> shouldEqual (Some true)
    // the hash should be set on the package now
    lockfile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Owin"].Settings.Hash |> shouldEqual (Some "k+ipb4ehxvkbz41mz3xetaptoea5qrr4+r9g8mdt8fq=")

[<Test>]
let ``should not calculate hash when disabled``() =
    prepare "hash-calculation-off"
    
    Paket.Dependencies.Install("""
    hash: off
    source https://nuget.org/api/v2
    nuget Owin
    """, path = scenarioTempPath "hash-calculation-off")

    let lockfile = LockFile.LoadFrom(Path.Combine(scenarioTempPath "hash-calculation-off", "paket.lock"))
    lockfile.Groups.[Constants.MainDependencyGroup].Options.Settings.UseHash |> shouldEqual (Some false)
    // the hash should be set on the package now
    lockfile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Owin"].Settings.Hash |> shouldEqual None

[<Test>]
let ``should not calculate hash when hash directive missing``() =
    prepare "hash-calculation-missing"

    Paket.Dependencies.Install("""
    hash: off
    source https://nuget.org/api/v2
    nuget Owin
    """, path = scenarioTempPath "hash-calculation-missing")

    let lockfile = LockFile.LoadFrom(Path.Combine(scenarioTempPath "hash-calculation-missing", "paket.lock"))
    lockfile.Groups.[Constants.MainDependencyGroup].Options.Settings.UseHash |> shouldEqual (Some false)
    // the hash should be set on the package now
    lockfile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Owin"].Settings.Hash |> shouldEqual None
