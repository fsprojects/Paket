module Paket.IntegrationTests.PaketCoreSpecs

open Fake
open System
open NUnit.Framework
open FsUnit
open System
open System.IO
open Pri.LongPath
open System.Diagnostics
open Paket
open Chessie.ErrorHandling
open Paket.Domain 

let alternativeProjectRoot = None

[<Test>]
let ``#1251 full installer demo``() = 
    prepare "i001251-installer-demo"
    let deps = """source https://nuget.org/api/v2
    nuget FAKE
    nuget FSharp.Formatting"""

    let dependenciesFile = DependenciesFile.FromSource(scenarioTempPath "i001251-installer-demo",deps)
    let force = false
    let packagesToInstall = 
        // get from references file
        [GroupName "Main",PackageName "FAKE"
         GroupName "Main",PackageName "FSharp.Formatting"]
    let lockFile,_,_ = UpdateProcess.SelectiveUpdate(dependenciesFile, alternativeProjectRoot, PackageResolver.UpdateMode.Install, SemVerUpdateMode.NoRestriction, force)
    let model = Paket.InstallProcess.CreateModel(alternativeProjectRoot, Path.GetDirectoryName dependenciesFile.FileName, force, dependenciesFile, lockFile, Set.ofSeq packagesToInstall, Map.empty) |> Map.ofArray

    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "FAKE"].Version
    |> shouldBeGreaterThan (SemVer.Parse "4")

[<Test>]
let ``#1251 install FSharp.Collections.ParallelSeq``() = 
    prepare "i001251-installer-demo"
    let deps = """source https://nuget.org/api/v2
    nuget FSharp.Collections.ParallelSeq"""

    let dependenciesFile = DependenciesFile.FromSource(scenarioTempPath "i001251-installer-demo",deps)
    let force = false
    let packagesToInstall = 
        // get from references file
        [GroupName "Main",PackageName "FSharp.Collections.ParallelSeq"] 

    let lockFile,_,_ = UpdateProcess.SelectiveUpdate(dependenciesFile, alternativeProjectRoot, PackageResolver.UpdateMode.Install, SemVerUpdateMode.NoRestriction, force)

    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "FSharp.Collections.ParallelSeq"].Version
    |> shouldBeGreaterThan (SemVer.Parse "1.0.1")

[<Test>]
let ``#1259 install via script``() =
    Environment.SetEnvironmentVariable ("PAKET_DISABLE_RUNTIME_RESOLUTION", "true")
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