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

    let lockFile = UpdateProcess.SelectiveUpdate(dependenciesFile, PackageResolver.UpdateMode.Install, SemVerUpdateMode.NoRestriction, force)
    let model = Paket.InstallProcess.CreateModel(Path.GetDirectoryName dependenciesFile.FileName, force, dependenciesFile, lockFile, Set.ofSeq packagesToInstall) |> Map.ofArray

    // do something with the model
    ()