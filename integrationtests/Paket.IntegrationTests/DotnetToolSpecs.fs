module Paket.IntegrationTests.DotnetToolSpecs

open System
open System.IO
open System.IO.Compression
open NUnit.Framework
open FsUnit

// Paket.Restore.targets has to find the Paket .NET tool on its own: these tests install the
// paket under test as a .NET tool and leave PaketExePath unset.

let scenario = "dotnet-tool-locator"

/// Restores the project of the scenario and checks that the installed tool did the Paket restore
let restoreShouldUseTool version env =
    let scenarioPath = scenarioTempPath scenario
    let output =
        directDotnetEx env false "restore" (scenarioPath @@ "project")
        |> Seq.map (fun msg -> msg.Message)
        |> String.concat Environment.NewLine
    output |> shouldContainText $"Paket version %s{version}"
    File.Exists(scenarioPath @@ "paket-files" @@ "paket.restore.cached") |> shouldEqual true

/// Installs the packed tool into the scenario, at the location the arguments give
let installTool scenario env location =
    directDotnetEx env false $"tool install paket %s{location} --version %s{paketToolPackageVersion} --configfile nuget.config" (scenarioTempPath scenario)
    |> ignore

[<Test>]
let ``restore finds paket as a local tool of the manifest``() =
    let cleanup, version, env = prepareSdkForTool scenario
    use __ = cleanup
    installTool scenario env "--local"

    restoreShouldUseTool version env

[<Test>]
let ``restore finds paket installed with --tool-path .paket``() =
    let cleanup, version, env = prepareSdkForTool scenario
    use __ = cleanup
    installTool scenario env "--tool-path .paket"

    restoreShouldUseTool version env

[<Test>]
let ``restore finds paket on the PATH like a global tool``() =
    let cleanup, version, env = prepareSdkForTool scenario
    use __ = cleanup
    let toolPath = scenarioTempPath scenario @@ "global-tools"
    installTool scenario env $"--tool-path \"%s{toolPath}\""

    let path = toolPath + string Path.PathSeparator + Environment.GetEnvironmentVariable "PATH"
    restoreShouldUseTool version (("PATH", path) :: env)

// `paket auto-restore on` imports paket.targets into the projects without the SDK, which
// Paket.Restore.targets doesn't cover: it restores before the build of a .NET Framework solution.

let legacyScenario = "auto-restore-legacy-project"

let embeddedPaketTargets = FullName(__SOURCE_DIRECTORY__ + "../../../src/Paket.Core/embedded/paket.targets")

[<Test>]
let ``auto-restore on writes paket.targets and imports it into projects without the SDK``() =
    use __ = paket "auto-restore on" legacyScenario |> fst
    let scenarioPath = scenarioTempPath legacyScenario
    File.ReadAllText(scenarioPath @@ ".paket" @@ "paket.targets") |> shouldEqual (File.ReadAllText embeddedPaketTargets)
    File.ReadAllText(scenarioPath @@ "legacy" @@ "legacy.csproj") |> shouldContainText "..\\.paket\\paket.targets"

    directPaket "auto-restore off" legacyScenario |> ignore
    File.Exists(scenarioPath @@ ".paket" @@ "paket.targets") |> shouldEqual false
    File.ReadAllText(scenarioPath @@ "legacy" @@ "legacy.csproj").Contains "paket.targets" |> shouldEqual false

[<Test>]
let ``paket.targets of auto-restore restores with paket on the PATH like a global tool``() =
    let cleanup, version, env = prepareSdkForTool legacyScenario
    use __ = cleanup
    let scenarioPath = scenarioTempPath legacyScenario
    directPaket "auto-restore on" legacyScenario |> ignore
    let toolPath = scenarioPath @@ "global-tools"
    installTool legacyScenario env $"--tool-path \"%s{toolPath}\""

    let path = toolPath + string Path.PathSeparator + Environment.GetEnvironmentVariable "PATH"
    let output =
        directDotnetEx (("PATH", path) :: env) false "msbuild legacy.csproj /t:RestorePackages" (scenarioPath @@ "legacy")
        |> Seq.map (fun msg -> msg.Message)
        |> String.concat Environment.NewLine
    output |> shouldContainText $"Paket version %s{version}"

/// Packs a package without files into a feed of the scenario, and adds a group with the lines, after
/// its source, that extract it into the packages folder, next to the "storage: none" of the scenario,
/// which doesn't use it. Gives the packages folder.
let addPackagesFolderGroupWith scenarioPath groupLines =
    let feed = scenarioPath @@ "package-feed"
    Directory.CreateDirectory feed |> ignore
    do
        use zip = ZipFile.Open(feed @@ "Paket.IntegrationTests.Empty.1.0.0.nupkg", ZipArchiveMode.Create)
        use writer = new StreamWriter(zip.CreateEntry("Paket.IntegrationTests.Empty.nuspec").Open())
        writer.Write """<?xml version="1.0" encoding="utf-8"?>
<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
  <metadata>
    <id>Paket.IntegrationTests.Empty</id>
    <version>1.0.0</version>
    <authors>Paket</authors>
    <description>A package without files</description>
  </metadata>
</package>
"""
    File.AppendAllText(scenarioPath @@ "paket.dependencies", $"\ngroup Legacy\nsource %s{feed}\n%s{groupLines}")
    scenarioPath @@ "packages"

let addPackagesFolderGroup scenarioPath =
    addPackagesFolderGroupWith scenarioPath "nuget Paket.IntegrationTests.Empty\n"

/// Runs paket in the scenario with addPackagesFolderGroup, with a NuGet packages folder of its own,
/// and gives its output
let paketWithPackagesFolderGroup command =
    let scenarioPath = scenarioTempPath legacyScenario
    directPaketInPathExWithEnv command scenarioPath [ "NUGET_PACKAGES", scenarioPath @@ "nuget-packages" ]
    |> Seq.map (fun msg -> msg.Message)
    |> String.concat Environment.NewLine

[<Test>]
let ``paket restore doesn't skip on the cache after the restore of a project for another paket.lock``() =
    use __ = paket "restore" legacyScenario |> fst
    let lockFile = scenarioTempPath legacyScenario @@ "paket.lock"
    let restoredLock = File.ReadAllBytes lockFile
    File.AppendAllText(lockFile, Environment.NewLine)
    directPaket "restore --references-file legacy/paket.references" legacyScenario |> ignore
    // projectsRestoredHash is cleared too: Paket.Restore.targets only checks that one, so the build
    // of SDK projects runs a full restore
    File.ReadAllText(scenarioTempPath legacyScenario @@ "paket-files" @@ "paket.restore.cached")
    |> shouldContainText "\"projectsRestoredHash\": \"\""

    File.WriteAllBytes(lockFile, restoredLock)
    directPaket "restore" legacyScenario |> shouldContainText "Starting full restore process"

[<Test>]
let ``paket restore doesn't skip on the cache when the packages folder is missing``() =
    use __ = prepare legacyScenario
    let packagesFolder = addPackagesFolderGroup (scenarioTempPath legacyScenario)
    paketWithPackagesFolderGroup "install" |> ignore
    paketWithPackagesFolderGroup "restore" |> shouldContainText "The last full restore is still up to date"

    Directory.Delete(packagesFolder, true)
    paketWithPackagesFolderGroup "restore" |> shouldContainText "Starting full restore process"
    Directory.Exists packagesFolder |> shouldEqual true

[<Test>]
let ``paket restore --force marks the cache stale until it succeeds``() =
    use __ = prepare legacyScenario
    let scenarioPath = scenarioTempPath legacyScenario
    addPackagesFolderGroup scenarioPath |> ignore
    paketWithPackagesFolderGroup "install" |> ignore
    let cacheFile = scenarioPath @@ "paket-files" @@ "paket.restore.cached"
    File.Delete cacheFile
    paketWithPackagesFolderGroup "restore --force" |> ignore
    paketWithPackagesFolderGroup "restore" |> shouldContainText "The last full restore is still up to date"

    // it empties the folders of the packages before it gets them again, here from nowhere
    Directory.Delete(scenarioPath @@ "package-feed", true)
    let failure = Assert.Throws<ProcessFailedWithExitCode>(fun () -> paketWithPackagesFolderGroup "restore --force" |> ignore)
    failure.ToString() |> shouldContainText "Paket.IntegrationTests.Empty"
    File.ReadAllText cacheFile |> shouldContainText "\"packagesDownloadedHash\": \"\""

/// Stands for the paket.targets of Paket 11, which runs paket.exe through mono outside Windows and
/// falls back to the bootstrapper
let olderPaketTargets = """<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003" />"""

/// Turns auto-restore on and restores, so that the restore cache is up to date as after a move to
/// Paket 12 with an unchanged paket.lock, then puts an older paket.targets back, runs the command
/// with the environment and gives its output and the paket.targets it leaves
let runWithOlderPaketTargets env command =
    use __ = paket "auto-restore on" legacyScenario |> fst
    let scenarioPath = scenarioTempPath legacyScenario
    directPaket "restore" legacyScenario |> ignore
    let paketTargets = scenarioPath @@ ".paket" @@ "paket.targets"
    File.WriteAllText(paketTargets, olderPaketTargets)

    let output =
        directPaketInPathExWithEnv command scenarioPath env
        |> Seq.map (fun msg -> msg.Message)
        |> String.concat Environment.NewLine
    output, File.ReadAllText paketTargets

let paketTargetsAfter command = runWithOlderPaketTargets [] command |> snd

[<Test>]
let ``restore refreshes the paket.targets of auto-restore``() =
    let output, paketTargets = runWithOlderPaketTargets [] "restore"
    // the restore stops early, after the refresh
    output |> shouldContainText "The last full restore is still up to date"
    paketTargets |> shouldEqual (File.ReadAllText embeddedPaketTargets)

[<Test>]
let ``restore leaves the paket.targets of auto-restore alone with PAKET_SKIP_RESTORE_TARGETS``() =
    runWithOlderPaketTargets [ "PAKET_SKIP_RESTORE_TARGETS", "true" ] "restore"
    |> snd
    |> shouldEqual olderPaketTargets

[<Test>]
let ``install without SDK projects refreshes the paket.targets of auto-restore``() =
    paketTargetsAfter "install" |> shouldEqual (File.ReadAllText embeddedPaketTargets)

[<Test>]
let ``restore of a references file leaves the paket.targets of auto-restore alone``() =
    // the restore that paket.targets runs during a build
    paketTargetsAfter "restore --references-file legacy/paket.references" |> shouldEqual olderPaketTargets

[<Test>]
let ``restore doesn't write paket.targets without auto-restore``() =
    use __ = paket "restore" legacyScenario |> fst
    File.Exists(scenarioTempPath legacyScenario @@ ".paket" @@ "paket.targets") |> shouldEqual false
