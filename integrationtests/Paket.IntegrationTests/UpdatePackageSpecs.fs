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
module Paket.IntegrationTests.UpdatePackageSpecs
#endif
open Fake
open System
open NUnit.Framework
open FsUnit
open System
open System.IO
open Pri.LongPath
open System.Diagnostics
open Paket
open Paket.Domain
open Paket.Requirements

[<Test>]
let ``#1178 update specific package``() =
    paket "update nuget NUnit" "i001178-update-with-regex" |> ignore
    let lockFile = LockFile.LoadFrom(Path.Combine(scenarioTempPath "i001178-update-with-regex","paket.lock"))
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Castle.Windsor"].Version
    |> shouldEqual (SemVer.Parse "2.5.1")
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "NUnit"].Version
    |> shouldBeGreaterThan (SemVer.Parse "2.6.1")
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Microsoft.AspNet.WebApi.SelfHost"].Version
    |> shouldEqual (SemVer.Parse "5.0.1")

[<Test>]
let ``#1469 update package in main group``() =
    update "i001469-darkseid" |> ignore
    let lockFile = LockFile.LoadFrom(Path.Combine(scenarioTempPath "i001469-darkseid","paket.lock"))
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Darkseid"].Version
    |> shouldBeGreaterThan (SemVer.Parse "0.2.1")

[<Test>]
let ``#1178 update with Mircosoft.* filter``() =
    paket "update nuget Microsoft.* --filter" "i001178-update-with-regex" |> ignore
    let lockFile = LockFile.LoadFrom(Path.Combine(scenarioTempPath "i001178-update-with-regex","paket.lock"))
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Castle.Windsor"].Version
    |> shouldEqual (SemVer.Parse "2.5.1")
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "NUnit"].Version
    |> shouldEqual (SemVer.Parse "2.6.1")
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Microsoft.AspNet.WebApi.SelfHost"].Version
    |> shouldBeGreaterThan (SemVer.Parse "5.0.1")

[<Test>]
let ``#1178 update with [MN].* --filter``() =
    paket "update nuget [MN].* --filter" "i001178-update-with-regex" |> ignore
    let lockFile = LockFile.LoadFrom(Path.Combine(scenarioTempPath "i001178-update-with-regex","paket.lock"))
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Castle.Windsor"].Version
    |> shouldEqual (SemVer.Parse "2.5.1")
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "NUnit"].Version
    |> shouldBeGreaterThan (SemVer.Parse "2.6.1")
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Microsoft.AspNet.WebApi.SelfHost"].Version
    |> shouldBeGreaterThan (SemVer.Parse "5.0.1")

[<Test>]
let ``#1178 update with [MN].* and without filter should fail``() =
    try
        paket "update nuget [MN].*" "i001178-update-with-regex" |> ignore
        failwithf "Paket command expected to fail"
    with
    | exn when exn.Message.Contains "Package [MN].* was not found in paket.dependencies in group Main" -> ()

[<Test>]
let ``#1178 update with NUn.* filter to specific version``() =
    paket "update nuget NUn.* --filter version 2.6.2" "i001178-update-with-regex" |> ignore
    let lockFile = LockFile.LoadFrom(Path.Combine(scenarioTempPath "i001178-update-with-regex","paket.lock"))
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Castle.Windsor"].Version
    |> shouldEqual (SemVer.Parse "2.5.1")
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "NUnit"].Version
    |> shouldEqual (SemVer.Parse "2.6.2")
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Microsoft.AspNet.WebApi.SelfHost"].Version
    |> shouldEqual (SemVer.Parse "5.0.1")


[<Test>]
let ``#1117 can understand portable``() =
    update "i001117-aws" |> ignore
    let lockFile = LockFile.LoadFrom(Path.Combine(scenarioTempPath "i001117-aws","paket.lock"))
    let restrictions = lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "PCLStorage"].Settings.FrameworkRestrictions
    match restrictions with
    | ExplicitRestriction l -> l.ToString() |> shouldEqual ("&& (< net45) (>= portable-net45+win8+wp8+wpa81)")
    | _ -> failwith "wrong"

    let restrictions = lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Microsoft.Bcl.Async"].Settings.FrameworkRestrictions
    match restrictions with
    | ExplicitRestriction l -> l.ToString() |> shouldEqual ("&& (< portable-net45+monoandroid+monotouch+xamarinios+xamarinmac+win8+wp8+wpa81) (>= portable-net45+win8+wp8+wpa81)")
    | _ -> failwith "wrong"

[<Test>]
let ``#1413 doesn't take symbols``() =
    update "i001413-symbols" |> ignore
    let lockFile = LockFile.LoadFrom(Path.Combine(scenarioTempPath "i001413-symbols","paket.lock"))
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Composable.Core"].Version
    |> shouldEqual (SemVer.Parse "3.4.0")

[<Test>]
let ``#1432 update doesn't throw Stackoverflow``() =
    let scenario = "i001432-stackoverflow"

    prepare scenario
    directPaket "pack templatefile paket.A.template version 1.0.0-prerelease output bin" scenario |> ignore
    directPaket "pack templatefile paket.A.template version 1.0.0 output bin" scenario |> ignore
    directPaket "pack templatefile paket.A.template version 1.1.0-prerelease output bin" scenario |> ignore
    directPaket "pack templatefile paket.B.template version 1.0.0 output bin" scenario |> ignore
    directPaket "pack templatefile paket.C.template version 1.0.0-prerelease output bin" scenario |> ignore
    directPaket "pack templatefile paket.D.template version 1.0.0-prerelease output bin" scenario  |> ignore
    directPaket "update" scenario|> ignore

[<Test>]
let ``#1579 update allows unpinned``() =
    let scenario = "i001579-unlisted"

    prepare scenario
    directPaket "pack templatefile paket.A.template version 1.0.0-prerelease output bin" scenario |> ignore
    directPaket "update" scenario|> ignore

[<Test>]
let ``#1501 download succeeds``() =
    update "i001510-download" |> ignore

[<Test>]
let ``#1520 update with pinned dependency succeeds``() =
    update "i001520-pinned-error" |> ignore

[<Test>]
let ``#1703 resolves locally``() =
    update "i001703-local" |> ignore


[<Test>]
let ``#1635 should tell about auth issue``() =
    try
        update "i001635-wrong-pw" |> ignore
        failwith "error expected"
    with
    | exn when exn.Message.Contains("Unable to retrieve package versions for 'Argu'") -> 
        exn.Message.Contains "Request to 'https://www.myget.org/F/paket-test/api/v3/index.json' failed with: 'Unauthorized'"
            |> shouldEqual true
        ()


[<Test>]
let ``#2572 should tell about late resolver issue``() =
    try
        update "i002572-pinned-error" |> ignore
        failwith "error expected"
    with
    | exn when exn.Message.Contains("xunit.core 2.3.0-beta3-build3705 requested package xunit.extensibility.core: 2.3.0-beta3-build3705") ->
        ()


#if INTERACTIVE
;;
let scenario = "i001579-unlisted"

prepare scenario
directPaket "pack templatefile paket.A.template version 1.0.0-prerelease output bin" scenario
directPaket "update" scenario
#endif