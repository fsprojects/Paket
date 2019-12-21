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
module Paket.IntegrationTests.BasicResolverSpecs
#endif

open Fake
open Paket
open NUnit.Framework
open FsUnit
open System
open System.IO
open Paket.Domain

[<Test>]
let ``#55 should resolve with pessimistic strategy correctly``() =
    let cleanup, lockFile = update "i000055-resolve-with-pessimistic-strategy"
    use __ = cleanup
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Castle.Windsor"].Version
    |> shouldEqual (SemVer.Parse "3.2.1")

[<Test>]
let ``#71 should ignore trailing zero during resolve``() =
    let cleanup, lockFile = update "i000071-ignore-trailing-zero-during-resolve"
    use __ = cleanup
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Newtonsoft.Json"].Version
    |> shouldEqual (SemVer.Parse "6.0.5.0")

[<Test>]
let ``#108 should resolve jquery case-insensitive``() =
    let cleanup, lockFile = update "i000108-case-insensitive-nuget-packages"
    use __ = cleanup
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "jQuery"].Version
    |> shouldEqual (SemVer.Parse "1.9.0")

[<Test>]
let ``#144 should resolve nunit from fsunit``() =
    let cleanup, lockFile = update "i000144-resolve-nunit"
    use __ = cleanup
    let v = lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "NUnit"].Version
    v |> shouldBeGreaterThan (SemVer.Parse "2.6")
    v |> shouldBeSmallerThan (SemVer.Parse "3")

[<Test>]
let ``#156 should resolve prerelease of logary``() =
    let cleanup, lockFile = update "i000156-resolve-prerelease-logary"
    use __ = cleanup
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "FSharp.Actor-logary"].Version
    |> shouldEqual (SemVer.Parse "2.0.0-alpha5")

[<Test>]
let ``#173 should resolve primary dependency optimistic``() =
    let cleanup, lockFile = update "i000173-resolve-primary-dependency-optimistic"
    use __ = cleanup
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "FSharp.Formatting"].Version
    |> shouldBeGreaterThan (SemVer.Parse "2.12.0")

[<Test>]
let ``#220 should respect the == operator``() =
    let cleanup, lockFile = update "i000220-use-exactly-this-constraint"
    use __ = cleanup
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Microsoft.AspNet.Razor"].Version
    |> shouldEqual (SemVer.Parse "2.0.30506.0")


[<Test>]
let ``#299 should restore package ending in lib``() =
    let cleanup, lockFile = update "i000299-restore-package-that-ends-in-lib"
    use __ = cleanup
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "FunScript.TypeScript.Binding.lib"].Version
    |> shouldBeGreaterThan (SemVer.Parse "0")

    Directory.Exists(Path.Combine(scenarioTempPath "i000299-restore-package-that-ends-in-lib","packages","FunScript.TypeScript.Binding.lib"))
    |> shouldEqual true

[<Test>]
let ``#359 should restore package with nuget in name``() =
    let cleanup, lockFile = update "i000359-packagename-contains-nuget"
    use __ = cleanup
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Nuget.CommandLine"].Version
    |> shouldBeGreaterThan (SemVer.Parse "0")

    Directory.Exists(Path.Combine(scenarioTempPath "i000359-packagename-contains-nuget","packages","NuGet.CommandLine"))
    |> shouldEqual true

[<Test>]
let ``#1177 should resolve with pessimistic strategy correctly``() =
    let cleanup, lockFile = update "i001177-resolve-with-pessimistic-strategy"
    use __ = cleanup
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Castle.Core"].Version
    |> shouldEqual (SemVer.Parse "3.2.0")

[<Test>]
let ``#1189 should allow # in path``() =
    let cleanup, lockFile = update "i001189-allow-#-in-path"
    use __ = cleanup
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "FAKE"].Version
    |> shouldBeGreaterThan (SemVer.Parse "4.7.2")

[<Test>]
let ``#1254 should install unlisted transitive dependencies``() =
    let cleanup, lockFile = update "i001254-unlisted"
    use __ = cleanup
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "WebActivator"].Version
    |> shouldEqual (SemVer.Parse "1.5.3")

[<Test>]
let ``#1450 should resolve with twiddle wakka``() =
    let cleanup, lockFile = update "i001450-twiddle-wakka"
    use __ = cleanup
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "EnterpriseLibrary.SemanticLogging"].Version
    |> shouldBeSmallerThan (SemVer.Parse "3")

[<Test>]
let ``#2640 shouldn't try GetDetails if package only exists locally``() =
    use __ = updateEx true "i002640" |> fst
    ignore __

#if INTERACTIVE
;;

#endif