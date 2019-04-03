module Paket.Simplifier.BasicScenarioSpecs

open Paket

open System
open NUnit.Framework
open FsUnit
open Paket.Domain
open Paket.TestHelpers
open Paket.InstallProcess

let dummyDir = System.IO.DirectoryInfo("C:/")
let dummyProjectFile () = 
    { FileName = ""
      OriginalText = ""
      Document = null
      ProjectNode = null
      Language = ProjectLanguage.Unknown
      DefaultProperties = None
      CalculatedProperties = new System.Collections.Concurrent.ConcurrentDictionary<_,_>() }

let lockFile0 = """
NUGET
  remote: https://www.nuget.org/api/v2
  specs:
    A (1.0)
      B (1.0)
      C (1.0)
      E (1.0)
      F (1.0)
    B (1.0)
    C (1.0)
    D (1.0)
      B (1.0)
      C (1.0)
    E (1.0)
    F (1.0)""" |> (fun x -> LockFile.Parse("", toLines x)) |> Some

let depFile0 = """
source http://www.nuget.org/api/v2

nuget A 1.0
nuget B
nuget C 1.0
nuget D 1.0
nuget E prerelease
nuget F simplify:never""" |> DependenciesFile.FromSource

let projects0 = [
    ReferencesFile.FromLines [|"A";"B";"C";"D";"E";"F"|]
    ReferencesFile.FromLines [|"B";"C copy_local: true"; "D" |] ] |> List.zip [dummyProjectFile(); dummyProjectFile()]

[<Test>]
let ``should not remove dependencies with settings or version restrictions``() =
    let before = PaketEnv.create dummyDir depFile0 lockFile0 projects0

    match Simplifier.simplify false before with
    | Chessie.ErrorHandling.Bad(msgs) ->
        failwith (String.concat Environment.NewLine (msgs |> List.map string))
    | Chessie.ErrorHandling.Ok((_,after),_) ->
        let depFile,refFiles = after.DependenciesFile, after.Projects |> List.map snd
        depFile.Groups.[Constants.MainDependencyGroup].Packages |> List.map (fun p -> p.Name) |> shouldEqual [PackageName"A";PackageName"C";PackageName"D";PackageName"E";PackageName"F"]
        refFiles.Head.Groups.[Constants.MainDependencyGroup].NugetPackages |> shouldEqual [PackageInstallSettings.Default("A"); PackageInstallSettings.Default("D")]
        refFiles.Tail.Head.Groups.[Constants.MainDependencyGroup].NugetPackages |> shouldEqual [{ PackageInstallSettings.Default("C") with Settings = {PackageInstallSettings.Default("C").Settings with CopyLocal = Some true}}; PackageInstallSettings.Default("D")]

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

nuget A
nuget B
nuget C
nuget D """ |> DependenciesFile.FromSource

let projects1 = [
    ReferencesFile.FromLines [|"A";"B";"C";"D"|]
    ReferencesFile.FromLines [|"B";"C"|] ] |> List.zip [dummyProjectFile(); dummyProjectFile()]

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

nuget A
nuget B
nuget C
nuget D
nuget E
nuget F """ |> DependenciesFile.FromSource

let projects2 = [
    ReferencesFile.FromLines [|"A";"B";"C";"D";"F"|]
    ReferencesFile.FromLines [|"C";"D";"E"|] ] |> List.zip [dummyProjectFile(); dummyProjectFile()]

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

nuget A
nuget C"""

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

nuget A
nuget B
nuget C
nuget D
nuget E
nuget F

group Deps2
source http://www.nuget.org/api/v2

nuget A
nuget B
nuget C
nuget D
nuget E
nuget F """ |> DependenciesFile.FromSource

let projects3 = [
    ReferencesFile.FromLines [|"A";"B";"C";"D";"F"|]
    ReferencesFile.FromLines [|"C";"D";"E"|] ] |> List.zip [dummyProjectFile(); dummyProjectFile()]

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

nuget A
nuget C

group Deps2
source http://www.nuget.org/api/v2

nuget A
nuget C"""

        depFile.ToString()
        |> shouldEqual (normalizeLineEndings expected)


let lockFile4 = """

GROUP Foo
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

let depFile4 = """
source http://www.nuget.org/api/v2

group Foo
source http://www.nuget.org/api/v2

nuget A
nuget B
nuget C
nuget D
nuget E
nuget F """ |> DependenciesFile.FromSource

let projects4 = [
    ReferencesFile.FromLines [|"group Foo";"A";"B";"C";"D";"F"|]
    ReferencesFile.FromLines [|"group Foo";"C";"D";"E"|] ] |> List.zip [dummyProjectFile(); dummyProjectFile()]

[<Test>]
let ``should remove all transitive dependencies from dep file and ref file with empty main group and non empty group foo``() =
    let before = PaketEnv.create dummyDir depFile4 lockFile4 projects4
    let fooGroupName = (GroupName "Foo")

    match Simplifier.simplify false before with
    | Chessie.ErrorHandling.Bad(msgs) -> 
        failwith (String.concat Environment.NewLine (msgs |> List.map string))
    | Chessie.ErrorHandling.Ok((_,after),_) ->
        let depFile,refFiles = after.DependenciesFile, after.Projects |> List.map snd
        depFile.Groups.[fooGroupName].Packages |> List.map (fun p -> p.Name) |> shouldEqual [PackageName"A";PackageName"C"]
        refFiles.Head.Groups.[fooGroupName].NugetPackages |>  shouldEqual [PackageInstallSettings.Default("A"); PackageInstallSettings.Default("C")]
        refFiles.Tail.Head.Groups.[fooGroupName].NugetPackages |>  shouldEqual [PackageInstallSettings.Default("C"); PackageInstallSettings.Default("D")]

        let expected = """source http://www.nuget.org/api/v2

group Foo
source http://www.nuget.org/api/v2

nuget A
nuget C"""

        depFile.ToString()
        |> shouldEqual (normalizeLineEndings expected)

        let firstRefFileExpected = """group Foo
A
C"""
        refFiles.Head.ToString()
        |> shouldEqual (normalizeLineEndings firstRefFileExpected)

        let secondRefFileExpected = """group Foo
C
D"""
        refFiles.Tail.Head.ToString()
        |> shouldEqual (normalizeLineEndings secondRefFileExpected)

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


[<Test>]
let ``#2382 paket simplify does not operate on groups in paket.references``() =
    let before = """source https://api.nuget.org/v3/index.json

group Foo
source https://api.nuget.org/v3/index.json
nuget Castle.Core 4.0.0 restriction: >= net452
nuget Castle.Windsor 4.0.0 restriction: >= net452
nuget System.ValueTuple 4.3.0 restriction: >= net452"""
    let beforeRefFile = """group Foo
Castle.Core
Castle.Windsor
System.ValueTuple"""

    let after = """source https://api.nuget.org/v3/index.json

group Foo
source https://api.nuget.org/v3/index.json
nuget Castle.Windsor 4.0.0 restriction: >= net452
nuget System.ValueTuple 4.3.0 restriction: >= net452"""

    let afterRefFile = """group Foo
Castle.Windsor
System.ValueTuple"""

    let originalLockFile = DependenciesFile.FromSource(before)
    originalLockFile.SimplifyFrameworkRestrictions().ToString()
    |> normalizeLineEndings
    |> shouldEqual (normalizeLineEndings before)