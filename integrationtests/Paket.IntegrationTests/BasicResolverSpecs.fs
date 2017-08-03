#if INTERACTIVE
PRI.LongPath.Directory.SetCurrentDirectory __SOURCE_DIRECTORY__
#r "../../packages/test/NUnit/lib/net45/nunit.framework.dll"
#r "../../packages/build/FAKE/tools/Fakelib.dll"
#r "../../packages/Chessie/lib/net40/Chessie.dll"
#r "../../bin/paket.core.dll"
#load "../../paket-files/test/forki/FsUnit/FsUnit.fs"
#load "TestHelper.fs"
open Paket.IntegrationTests.TestHelpers
#else
module Paket.IntegrationTests.BasicResolverSpecs
#endif

open Fake
open Paket
open System
open NUnit.Framework
open FsUnit
open System
open System.IO
open Pri.LongPath
open Paket.Domain

[<Test>]
let ``#49 windsor should resolve correctly``() =
    let lockFile = update "i000049-resolve-windsor-correctly"
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Castle.Windsor"].Version
    |> shouldEqual (SemVer.Parse "3.2.1")

[<Test>]
let ``#51 should resolve with pessimistic strategy correctly``() =
    let lockFile = update "i000051-resolve-pessimistic"
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Castle.Windsor-log4net"].Version
    |> shouldEqual (SemVer.Parse "3.2.0.1")

[<Test>]
let ``#55 should resolve with pessimistic strategy correctly``() =
    let lockFile = update "i000055-resolve-with-pessimistic-strategy"
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Castle.Windsor"].Version
    |> shouldEqual (SemVer.Parse "3.2.1")

[<Test>]
let ``#71 should ignore trailing zero during resolve``() =
    let lockFile = update "i000071-ignore-trailing-zero-during-resolve"
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Newtonsoft.Json"].Version
    |> shouldEqual (SemVer.Parse "6.0.5.0")

[<Test>]
let ``#83 should resolve jquery``() =
    let lockFile = update "i000083-resolve-jquery"
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "jQuery"].Version
    |> shouldBeGreaterThan (SemVer.Parse "1.9")

[<Test>]
let ``#108 should resolve jquery case-insensitive``() =
    let lockFile = update "i000108-case-insensitive-nuget-packages"
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "jQuery"].Version
    |> shouldEqual (SemVer.Parse "1.9.0")

[<Test>]
let ``#144 should resolve nunit from fsunit``() =
    let lockFile = update "i000144-resolve-nunit"
    let v = lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "NUnit"].Version
    v |> shouldBeGreaterThan (SemVer.Parse "2.6")
    v |> shouldBeSmallerThan (SemVer.Parse "3")

[<Test>]
let ``#156 should resolve prerelease of logary``() =
    let lockFile = update "i000156-resolve-prerelease-logary"
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "FSharp.Actor-logary"].Version
    |> shouldEqual (SemVer.Parse "2.0.0-alpha5")

[<Test>]
let ``#173 should resolve primary dependency optimistic``() =
    let lockFile = update "i000173-resolve-primary-dependency-optimistic"
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "FSharp.Formatting"].Version
    |> shouldBeGreaterThan (SemVer.Parse "2.12.0")

[<Test>]
let ``#220 should respect the == operator``() =
    let lockFile = update "i000220-use-exactly-this-constraint"
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Microsoft.AspNet.Razor"].Version
    |> shouldEqual (SemVer.Parse "2.0.30506.0")


[<Test>]
let ``#299 should restore package ending in lib``() =
    let lockFile = update "i000299-restore-package-that-ends-in-lib"
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "FunScript.TypeScript.Binding.lib"].Version
    |> shouldBeGreaterThan (SemVer.Parse "0")

    Directory.Exists(Path.Combine(scenarioTempPath "i000299-restore-package-that-ends-in-lib","packages","FunScript.TypeScript.Binding.lib"))
    |> shouldEqual true
    
[<Test>]
let ``#359 should restore package with nuget in name``() =
    let lockFile = update "i000359-packagename-contains-nuget"
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Nuget.CommandLine"].Version
    |> shouldBeGreaterThan (SemVer.Parse "0")

    Directory.Exists(Path.Combine(scenarioTempPath "i000359-packagename-contains-nuget","packages","NuGet.CommandLine"))
    |> shouldEqual true

[<Test>]
let ``#1177 should resolve with pessimistic strategy correctly``() =
    let lockFile = update "i001177-resolve-with-pessimistic-strategy"
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Castle.Core"].Version
    |> shouldEqual (SemVer.Parse "3.2.0")

[<Test>]
let ``#1189 should allow # in path``() =
    let lockFile = update "i001189-allow-#-in-path"
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "FAKE"].Version
    |> shouldBeGreaterThan (SemVer.Parse "4.7.2")

[<Test>]
let ``#1247 shouldn't load lockfile in full update``() =
    update "i001247-lockfile-error" |> ignore

[<Test>]
let ``#1247 should report lockfile in parse errror``() =
    try
        paket "update --keep-minor" "i001247-lockfile-error" |> ignore

        failwith "error was expected"
    with
    | exn when exn.Message.Contains "paket.lock" -> ()

[<Test>]
let ``#1254 should install unlisted transitive dependencies``() =
    let lockFile = update "i001254-unlisted"
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "WebActivator"].Version
    |> shouldEqual (SemVer.Parse "1.5.3")

[<Test>]
let ``#1450 should resolve with twiddle wakka``() =
    let lockFile = update "i001450-twiddle-wakka"
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "EnterpriseLibrary.SemanticLogging"].Version
    |> shouldBeSmallerThan (SemVer.Parse "3")


#if INTERACTIVE
;;

#endif