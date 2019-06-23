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
open System

[<Test>]
let ``#1166 Should resolve Nancy without timeout``() =
    let cleanup, lockFile = update "i001166-resolve-nancy-fast"
    use __ = cleanup
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Nancy"].Version
    |> shouldBeGreaterThan (SemVer.Parse "1.1")

[<Test>]
let ``#2289 Paket 4.x install command takes hours to complete``() =
    let cleanup, lockFile = install "i002289-resolve-nunit-timeout"
    use __ = cleanup
    let nunitVersion =
        lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "NUnit"].Version

    nunitVersion
    |> shouldBeGreaterThan (SemVer.Parse "2.0")
    nunitVersion
    |> shouldBeSmallerThan (SemVer.Parse "3.0")

[<Test>]
#if NO_UNIT_PLATFORMATTRIBUTE
[<Ignore "PlatformAttribute not supported by netstandard NUnit">]
#else
[<Platform("Net")>]
#endif
let ``#1174 Should find Ninject error``() =
    updateShouldFindPackageConflict "Ninject" "i001174-resolve-fast-conflict"

#if INTERACTIVE
;;

#endif
