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
let ``#1928 reference via script with casing``() = 
    install "i001928-casing" |> ignore

    let dependenciesFile = Dependencies.Locate(Path.Combine(scenarioTempPath "i001928-casing"))
    let model = dependenciesFile.GetInstalledPackageModel(None, "paket.core")
    model.GetReferenceFolders() |> Seq.length |> shouldBeGreaterThan 0

[<Test>]
let ``#1918 uncached deps``() = 
    install "i001918-core" |> ignore

    let deps = """source https://nuget.org/api/v2
    nuget Http.fs"""

    paket "clear-cache" "i001918-core" |> ignore
    let dependencies = Paket.Dependencies.Locate(scenarioTempPath "i001918-core")
    dependencies.Install(false)