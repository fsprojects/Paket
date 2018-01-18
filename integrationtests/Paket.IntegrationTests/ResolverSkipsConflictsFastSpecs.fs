#if INTERACTIVE
System.IO.Directory.SetCurrentDirectory __SOURCE_DIRECTORY__
#r "../../packages/test/NUnit/lib/net45/nunit.framework.dll"
#r "../../packages/build/FAKE/tools/Fakelib.dll"
#r "../../packages/Chessie/lib/net40/Chessie.dll"
#r "../../bin/paket.core.dll"
#load "../../paket-files/test/forki/FsUnit/FsUnit.fs"
#load "TestHelper.fs"
open Paket.IntegrationTests.TestHelpers
#else
module Paket.IntegrationTests.ResolverSkipsConflictsFastSpecs
#endif

open Fake
open Paket
open System
open NUnit.Framework
open FsUnit
open System
open System.IO
open Paket.Domain

[<Test>]
let ``#1166 Should resolve Nancy without timeout``() =
    let lockFile = update "i001166-resolve-nancy-fast"
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Nancy"].Version
    |> shouldBeGreaterThan (SemVer.Parse "1.1")

[<Test>]
let ``#2289 Paket 4.x install command takes hours to complete``() =
    let lockFile = installEx true "i002289-resolve-nunit-timeout"
    let nunitVersion =
        lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "NUnit"].Version
    nunitVersion
    |> shouldBeGreaterThan (SemVer.Parse "2.0")
    nunitVersion
    |> shouldBeSmallerThan (SemVer.Parse "3.0")

[<Test>]
let ``#2294 Cannot pin NETStandard.Library = 1.6.0``() =
    let lockFile = update "i002294-pin-netstd16"
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "NETStandard.Library"].Version
    |> shouldEqual (SemVer.Parse "1.6")

[<Test; Flaky>]
let ``#2294 pin NETStandard.Library = 1.6.0 Strategy Workaround``() =
    let lockFile = update "i002294-withstrategy"
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "NETStandard.Library"].Version
    |> shouldEqual (SemVer.Parse "1.6")

[<Test>]
let ``#2922 paket can jump out of loop of doom``() =
    try
        install "i002922-loopofdoom" |> ignore
        failwith "error expected"
    with
    | exn when exn.Message.Contains("Dependencies file requested package MySqlConnector: < 0.30") -> ()

[<Test>]
#if NETCOREAPP2_0
[<Ignore "PlatformAttribute not supported by netstandard NUnit">]
#else
[<Platform("Net")>]
#endif
let ``#1174 Should find Ninject error``() =
    updateShouldFindPackageConflict "Ninject" "i001174-resolve-fast-conflict"

#if INTERACTIVE
;;

#endif
