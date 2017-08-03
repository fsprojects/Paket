module Paket.Simplifier.BasicScenarioSpecs

open Paket

open System
open System.IO
open Pri.LongPath
open NUnit.Framework
open FsUnit
open Paket.Domain
open Paket.TestHelpers
open Paket.InstallProcess

let dummyDir = DirectoryInfo("C:/")
let dummyProjectFile = 
    { FileName = ""
      OriginalText = ""
      Document = null
      ProjectNode = null
      Language = ProjectLanguage.Unknown }

let lockFile1 = """
NUGET
  remote: https://www.nuget.org/api/v2
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
source http://www.nuget.org/api/v2

nuget A 3.3.0
nuget B 3.3.1
nuget C 1.0
nuget D 2.1""" |> DependenciesFile.FromSource

let projects1 = [
    ReferencesFile.FromLines [|"A";"B";"C";"D"|]
    ReferencesFile.FromLines [|"B";"C"|] ] |> List.zip [dummyProjectFile; dummyProjectFile]

[<Test>]
let ``should remove one level deep transitive dependencies from dep and ref files``() = 
    let before = PaketEnv.create dummyDir depFile1 lockFile1 projects1
    
    match Simplifier.simplify false before with
    | Chessie.ErrorHandling.Bad(msgs) -> 
        failwith (String.concat Environment.NewLine (msgs |> List.map string))
    | Chessie.ErrorHandling.Ok((_,after),_) ->
        let depFile,refFiles = after.DependenciesFile, after.Projects |> List.map snd
        depFile.Groups.[Constants.MainDependencyGroup].Packages |> List.map (fun p -> p.Name) |> shouldEqual [PackageName"A";PackageName"D"]
        refFiles.Head.Groups.[Constants.MainDependencyGroup].NugetPackages |> shouldEqual [PackageInstallSettings.Default("A"); PackageInstallSettings.Default("D")]
        refFiles.Tail.Head.Groups.[Constants.MainDependencyGroup].NugetPackages |> shouldEqual [PackageInstallSettings.Default("B"); PackageInstallSettings.Default("C")]

let lockFile2 = """
NUGET
  remote: https://www.nuget.org/api/v2
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
source http://www.nuget.org/api/v2

nuget A 1.0
nuget B 1.0
nuget C 1.0
nuget D 1.0
nuget E 1.0
nuget F 1.0""" |> DependenciesFile.FromSource

let projects2 = [
    ReferencesFile.FromLines [|"A";"B";"C";"D";"F"|]
    ReferencesFile.FromLines [|"C";"D";"E"|] ] |> List.zip [dummyProjectFile; dummyProjectFile]

[<Test>]
let ``should remove all transitive dependencies from dep file recursively``() =
    let before = PaketEnv.create dummyDir depFile2 lockFile2 projects2
    
    match Simplifier.simplify false before with
    | Chessie.ErrorHandling.Bad(msgs) -> 
        failwith (String.concat Environment.NewLine (msgs |> List.map string))
    | Chessie.ErrorHandling.Ok((_,after),_) ->
        let depFile,refFiles = after.DependenciesFile, after.Projects |> List.map snd
        depFile.Groups.[Constants.MainDependencyGroup].Packages |> List.map (fun p -> p.Name) |> shouldEqual [PackageName"A";PackageName"C"]
        refFiles.Head.Groups.[Constants.MainDependencyGroup].NugetPackages |>  shouldEqual [PackageInstallSettings.Default("A"); PackageInstallSettings.Default("C")]
        refFiles.Tail.Head.Groups.[Constants.MainDependencyGroup].NugetPackages |>  shouldEqual [PackageInstallSettings.Default("C"); PackageInstallSettings.Default("D")]

        let expected = """source http://www.nuget.org/api/v2

nuget A 1.0
nuget C 1.0"""

        depFile.ToString()
        |> shouldEqual (normalizeLineEndings expected)


let lockFile3 = """
NUGET
  remote: https://www.nuget.org/api/v2
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
    F (1.0)

GROUP Deps2
NUGET
  remote: https://www.nuget.org/api/v2
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

let depFile3 = """
source http://www.nuget.org/api/v2

nuget A 1.0
nuget B 1.0
nuget C 1.0
nuget D 1.0
nuget E 1.0
nuget F 1.0

group Deps2
source http://www.nuget.org/api/v2

nuget A 1.0
nuget B 1.0
nuget C 1.0
nuget D 1.0
nuget E 1.0
nuget F 1.0""" |> DependenciesFile.FromSource

let projects3 = [
    ReferencesFile.FromLines [|"A";"B";"C";"D";"F"|]
    ReferencesFile.FromLines [|"C";"D";"E"|] ] |> List.zip [dummyProjectFile; dummyProjectFile]

[<Test>]
let ``should remove all transitive dependencies from dep file with multiple groups``() =
    let before = PaketEnv.create dummyDir depFile3 lockFile3 projects3
    
    match Simplifier.simplify false before with
    | Chessie.ErrorHandling.Bad(msgs) -> 
        failwith (String.concat Environment.NewLine (msgs |> List.map string))
    | Chessie.ErrorHandling.Ok((_,after),_) ->
        let depFile,refFiles = after.DependenciesFile, after.Projects |> List.map snd
        depFile.Groups.[Constants.MainDependencyGroup].Packages |> List.map (fun p -> p.Name) |> shouldEqual [PackageName"A";PackageName"C"]
        refFiles.Head.Groups.[Constants.MainDependencyGroup].NugetPackages |>  shouldEqual [PackageInstallSettings.Default("A"); PackageInstallSettings.Default("C")]
        refFiles.Tail.Head.Groups.[Constants.MainDependencyGroup].NugetPackages |>  shouldEqual [PackageInstallSettings.Default("C"); PackageInstallSettings.Default("D")]

        let expected = """source http://www.nuget.org/api/v2

nuget A 1.0
nuget C 1.0

group Deps2
source http://www.nuget.org/api/v2

nuget A 1.0
nuget C 1.0"""

        depFile.ToString()
        |> shouldEqual (normalizeLineEndings expected)

[<Test>]
[<Ignore "Simplifier is currently not working with the new restriction system, please fix and activate me">]
let ``should simplify framework restrictions in main group``() =
    let before = """source https://www.nuget.org/api/v2/

nuget angularjs 1.4.3 framework: >= net45
nuget AngularTemplates.Compile 1.0.0 framework: >= net45
nuget Antlr 3.4.1.9004 framework: >= net45
nuget Autofac 3.5.0 framework: >= net45
nuget Autofac.Owin 3.1.0 framework: >= net45
nuget Autofac.WebApi 3.1.0 framework: >= net45
nuget Autofac.WebApi2 3.4.0 framework: >= net45
nuget Autofac.WebApi2.Owin 3.2.0 framework: >= net45"""

    let expected = """source https://www.nuget.org/api/v2/
restriction: >= net45

nuget angularjs 1.4.3
nuget AngularTemplates.Compile 1.0.0
nuget Antlr 3.4.1.9004
nuget Autofac 3.5.0
nuget Autofac.Owin 3.1.0
nuget Autofac.WebApi 3.1.0
nuget Autofac.WebApi2 3.4.0
nuget Autofac.WebApi2.Owin 3.2.0"""


    let originalLockFile = DependenciesFile.FromSource(before)
    originalLockFile.SimplifyFrameworkRestrictions().ToString() 
    |> normalizeLineEndings
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should not simplify framework restrictions when not equal``() =
    let before = """source https://www.nuget.org/api/v2/

nuget angularjs 1.4.3 framework: >= net45
nuget AngularTemplates.Compile 1.0.0 framework: >= net45
nuget Antlr 3.4.1.9004 framework: >= net45
nuget Autofac 3.5.0 framework: >= net45
nuget Autofac.Owin 3.1.0 framework: >= net40
nuget Autofac.WebApi 3.1.0 framework: >= net45
nuget Autofac.WebApi2 3.4.0 framework: >= net45
nuget Autofac.WebApi2.Owin 3.2.0 framework: >= net45"""
    let after = """source https://www.nuget.org/api/v2/

nuget angularjs 1.4.3 restriction: >= net45
nuget AngularTemplates.Compile 1.0.0 restriction: >= net45
nuget Antlr 3.4.1.9004 restriction: >= net45
nuget Autofac 3.5.0 restriction: >= net45
nuget Autofac.Owin 3.1.0 restriction: >= net40
nuget Autofac.WebApi 3.1.0 restriction: >= net45
nuget Autofac.WebApi2 3.4.0 restriction: >= net45
nuget Autofac.WebApi2.Owin 3.2.0 restriction: >= net45"""

    let originalLockFile = DependenciesFile.FromSource(before)
    originalLockFile.SimplifyFrameworkRestrictions().ToString()
    |> normalizeLineEndings
    |> shouldEqual (normalizeLineEndings before)

[<Test>]
[<Ignore "Simplifier is currently not working with the new restriction system, please fix and activate me">]
let ``should simplify framework restrictions in every group``() =
    let before = """source https://www.nuget.org/api/v2/

nuget angularjs 1.4.3 framework: >= net45
nuget AngularTemplates.Compile 1.0.0 framework: >= net45
nuget Antlr 3.4.1.9004 framework: >= net45
nuget Autofac 3.5.0 framework: >= net45

group Build
source https://www.nuget.org/api/v2/
nuget Autofac.Owin 3.1.0 framework: >= net40
nuget Autofac.WebApi 3.1.0 framework: >= net40
nuget Autofac.WebApi2 3.4.0 framework: >= net40
nuget Autofac.WebApi2.Owin 3.2.0 framework: >= net40"""

    let expected = """source https://www.nuget.org/api/v2/
restriction: >= net45

nuget angularjs 1.4.3
nuget AngularTemplates.Compile 1.0.0
nuget Antlr 3.4.1.9004
nuget Autofac 3.5.0

group Build
source https://www.nuget.org/api/v2/
restriction: >= net40
nuget Autofac.Owin 3.1.0
nuget Autofac.WebApi 3.1.0
nuget Autofac.WebApi2 3.4.0
nuget Autofac.WebApi2.Owin 3.2.0"""


    let originalLockFile = DependenciesFile.FromSource(before)
    originalLockFile.SimplifyFrameworkRestrictions().ToString() 
    |> normalizeLineEndings
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``should not simplify framework restrictions in empty file``() =
    let before = ""
    
    let originalLockFile = DependenciesFile.FromSource(before)
    originalLockFile.SimplifyFrameworkRestrictions().ToString() 
    |> normalizeLineEndings
    |> shouldEqual (normalizeLineEndings before)
    
[<Test>]
[<Ignore "Simplifier is currently not working with the new restriction system, please fix and activate me">]
let ``should simplify multiple framework restrictions in every group``() =
    let before = """source https://www.nuget.org/api/v2/

nuget angularjs 1.4.3 framework: net40, net45
nuget AngularTemplates.Compile 1.0.0 framework: net40, net45
nuget Antlr 3.4.1.9004 framework: net40, net45
nuget Autofac 3.5.0 framework: net40, net45

group Build
source https://www.nuget.org/api/v2/
nuget Autofac.Owin 3.1.0 framework: sl4, sl5
nuget Autofac.WebApi 3.1.0 framework: sl5, sl4
nuget Autofac.WebApi2 3.4.0 framework: sl5, sl4
nuget Autofac.WebApi2.Owin 3.2.0 framework: sl4, sl5"""

    let expected = """source https://www.nuget.org/api/v2/
restriction: || (net40) (net45)

nuget angularjs 1.4.3
nuget AngularTemplates.Compile 1.0.0
nuget Antlr 3.4.1.9004
nuget Autofac 3.5.0

group Build
source https://www.nuget.org/api/v2/
restriction: || (sl40) (sl50)

nuget Autofac.Owin 3.1.0
nuget Autofac.WebApi 3.1.0
nuget Autofac.WebApi2 3.4.0
nuget Autofac.WebApi2.Owin 3.2.0"""


    let originalLockFile = DependenciesFile.FromSource(before)
    originalLockFile.SimplifyFrameworkRestrictions().ToString() 
    |> normalizeLineEndings
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
[<Ignore "Simplifier is currently not working with the new restriction system, please fix and activate me">]
let ``should simplify subset of framework restrictions in every group``() =
    let before = """source https://www.nuget.org/api/v2/

nuget angularjs 1.4.3 framework: net40, net45, net20
nuget AngularTemplates.Compile 1.0.0 framework: net40, net45
nuget Antlr 3.4.1.9004 framework: net40, net45
nuget Autofac 3.5.0 framework: net40, net45

group Build
source https://www.nuget.org/api/v2/
nuget Autofac.Owin 3.1.0 framework: sl4, sl5
nuget Autofac.WebApi 3.1.0 framework: sl5, sl4
nuget Autofac.WebApi2 3.4.0 framework: sl5, sl4, >= net45
nuget Autofac.WebApi2.Owin 3.2.0 framework: sl4, sl5"""

    let expected = """source https://www.nuget.org/api/v2/
restriction: || (net40) (net45)

nuget angularjs 1.4.3 restriction: net20
nuget AngularTemplates.Compile 1.0.0
nuget Antlr 3.4.1.9004
nuget Autofac 3.5.0

group Build
source https://www.nuget.org/api/v2/
restriction: || (sl4) (sl5)
nuget Autofac.Owin 3.1.0
nuget Autofac.WebApi 3.1.0
nuget Autofac.WebApi2 3.4.0 restriction: >= net45
nuget Autofac.WebApi2.Owin 3.2.0"""


    let originalLockFile = DependenciesFile.FromSource(before)
    originalLockFile.SimplifyFrameworkRestrictions().ToString() 
    |> normalizeLineEndings
    |> shouldEqual (normalizeLineEndings expected)