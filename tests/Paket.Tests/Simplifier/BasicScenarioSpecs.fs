module Paket.Simplifier.BasicScenarioSpecs

open Paket

open System
open NUnit.Framework
open FsUnit
open Paket.Domain
open Paket.TestHelpers

let dummyDir = System.IO.DirectoryInfo("C:/")
let dummyProjectFile = 
    { FileName = ""
      OriginalText = ""
      Document = null
      ProjectNode = null }

let lockFile1 = """
NUGET
  remote: https://nuget.org/api/v2
  specs:
    A (1.0)
      B (1.0)
      C (1.0)
    B (1.0)
    C (1.0)
    D (1.0)
      B (1.0)
      C (1.0)""" |> (fun x -> LockFile.Parse("", toLines x)) |> Some

let depFile1 = """
source http://nuget.org/api/v2

nuget A 3.3.0
nuget B 3.3.1
nuget C 1.0
nuget D 2.1""" |> DependenciesFile.FromCode

let projects1 = [
    ReferencesFile.FromLines [|"A";"B";"C";"D"|]
    ReferencesFile.FromLines [|"B";"C"|] ] |> List.zip [dummyProjectFile; dummyProjectFile]

[<Test>]
let ``should remove one level deep transitive dependencies from dep and ref files``() = 
    let before = PaketEnv.create dummyDir depFile1 lockFile1 projects1
    
    match Simplifier.simplify false before with
    | Chessie.ErrorHandling.Fail(msgs) -> 
        failwith (String.concat Environment.NewLine (msgs |> List.map string))
    | Chessie.ErrorHandling.Ok((_,after),_) ->
        let depFile,refFiles = after.DependenciesFile, after.Projects |> List.map snd
        depFile.Packages |> List.map (fun p -> p.Name) |> shouldEqual [PackageName"A";PackageName"D"]
        refFiles.Head.NugetPackages |> shouldEqual [PackageInstallSettings.Default("A"); PackageInstallSettings.Default("D")]
        refFiles.Tail.Head.NugetPackages |> shouldEqual [PackageInstallSettings.Default("B"); PackageInstallSettings.Default("C")]

let lockFile2 = """
NUGET
  remote: https://nuget.org/api/v2
  specs:
    A (1.0)
      B (1.0)
    B (1.0)
      D (1.0)
    C (1.0)
      E (1.0)
    D (1.0)
      E (1.0)
    E (1.0)
      F (1.0)
    F (1.0)""" |> (fun x -> LockFile.Parse("", toLines x)) |> Some

let depFile2 = """
source http://nuget.org/api/v2

nuget A 1.0
nuget B 1.0
nuget C 1.0
nuget D 1.0
nuget E 1.0
nuget F 1.0""" |> DependenciesFile.FromCode

let projects2 = [
    ReferencesFile.FromLines [|"A";"B";"C";"D";"F"|]
    ReferencesFile.FromLines [|"C";"D";"E"|] ] |> List.zip [dummyProjectFile; dummyProjectFile]

[<Test>]
let ``should remove all transitive dependencies from dep file recursively``() =
    let before = PaketEnv.create dummyDir depFile2 lockFile2 projects2
    
    match Simplifier.simplify false before with
    | Chessie.ErrorHandling.Fail(msgs) -> 
        failwith (String.concat Environment.NewLine (msgs |> List.map string))
    | Chessie.ErrorHandling.Ok((_,after),_) ->
        let depFile,refFiles = after.DependenciesFile, after.Projects |> List.map snd
        depFile.Packages |> List.map (fun p -> p.Name) |> shouldEqual [PackageName"A";PackageName"C"]
        refFiles.Head.NugetPackages |>  shouldEqual [PackageInstallSettings.Default("A"); PackageInstallSettings.Default("C")]
        refFiles.Tail.Head.NugetPackages |>  shouldEqual [PackageInstallSettings.Default("C"); PackageInstallSettings.Default("D")]
