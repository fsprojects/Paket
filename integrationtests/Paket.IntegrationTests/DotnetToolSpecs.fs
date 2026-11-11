module Paket.IntegrationTests.DotnetToolSpecs

open System
open System.IO
open System.IO.Compression
open NUnit.Framework
open FsUnit

// Paket.Restore.targets has to find the Paket .NET tool on its own: these tests install the
// paket under test as a .NET tool and leave PaketExePath unset.

let scenario = "dotnet-tool-locator"

/// Restores the project of the scenario and gives the output
let restoreOutput env =
    directDotnetEx env false "restore" (scenarioTempPath scenario @@ "project")
    |> Seq.map (fun msg -> msg.Message)
    |> String.concat Environment.NewLine

/// Restores the project of the scenario and checks that the installed tool did the Paket restore
let restoreShouldUseTool version env =
    restoreOutput env |> shouldContainText $"Paket version %s{version}"
    File.Exists(scenarioTempPath scenario @@ "paket-files" @@ "paket.restore.cached") |> shouldEqual true

/// Installs the packed tool into the scenario, at the location the arguments give
let installTool scenario env location =
    directDotnetEx env false $"tool install paket %s{location} --version %s{paketToolPackageVersion} --configfile nuget.config" (scenarioTempPath scenario)
    |> ignore

/// Puts a file that isn't Paket where `dotnet tool install paket --tool-path` would write it, as
/// the paket.exe of Paket 11 left in .paket: the restore fails if it runs
let leaveOlderPaketIn folder =
    let executable = if OperatingSystem.IsWindows() then "paket.exe" else "paket"
    Directory.CreateDirectory folder |> ignore
    File.WriteAllText(folder @@ executable, "not a paket")

[<Test>]
let ``restore finds paket as a local tool of the manifest``() =
    let cleanup, version, env = prepareSdkForTool scenario
    use __ = cleanup
    installTool scenario env "--local"

    restoreShouldUseTool version env

[<Test>]
let ``restore finds paket as a local tool of a dotnet-tools.json manifest``() =
    let cleanup, version, env = prepareSdkForTool scenario
    use __ = cleanup
    installTool scenario env "--local"
    let scenarioPath = scenarioTempPath scenario
    File.Move(scenarioPath @@ ".config" @@ "dotnet-tools.json", scenarioPath @@ "dotnet-tools.json")
    // the closest .config/dotnet-tools.json, without paket: otherwise the one of the Paket repository,
    // which has paket, would be found above the scenario
    File.WriteAllText(scenarioPath @@ ".config" @@ "dotnet-tools.json", """{ "version": 1, "isRoot": false, "tools": {} }""")

    // without the `dotnet paket --version` test, only the manifest tells that paket is a local tool
    restoreShouldUseTool version (("PaketDisableCliTest", "true") :: env)

[<Test>]
let ``restore finds paket installed with --tool-path .paket``() =
    let cleanup, version, env = prepareSdkForTool scenario
    use __ = cleanup
    installTool scenario env "--tool-path .paket"

    restoreShouldUseTool version env

[<Test>]
let ``restore runs a paket installed with --tool-path when the manifest only has a paket command of another tool``() =
    let cleanup, version, env = prepareSdkForTool scenario
    use __ = cleanup
    installTool scenario env "--tool-path .paket"
    File.WriteAllText(
        scenarioTempPath scenario @@ ".config" @@ "dotnet-tools.json",
        """{ "version": 1, "isRoot": true, "tools": { "paket-wrapper": { "version": "1.0.0", "commands": [ "paket" ] } } }""")

    let output = restoreOutput env
    output |> shouldContainText $"Paket version %s{version}"
    output |> shouldNotContainText "PAKET0001"

[<Test>]
let ``restore runs the local tool of the manifest instead of a paket installed with --tool-path``() =
    let cleanup, version, env = prepareSdkForTool scenario
    use __ = cleanup
    installTool scenario env "--local"
    leaveOlderPaketIn (scenarioTempPath scenario @@ ".paket")

    let output = restoreOutput env
    output |> shouldContainText $"Paket version %s{version}"
    output |> shouldContainText "warning PAKET0001"

[<Test>]
let ``restore runs the PaketExePath it is given, without PAKET0001``() =
    let cleanup, version, env = prepareSdkForTool scenario
    use __ = cleanup
    // a local tool that isn't restored: the restore only succeeds if the PaketExePath given runs
    File.WriteAllText(
        scenarioTempPath scenario @@ ".config" @@ "dotnet-tools.json",
        $"""{{ "version": 1, "isRoot": true, "tools": {{ "paket": {{ "version": "%s{paketToolPackageVersion}", "commands": [ "paket" ] }} }} }}""")
    leaveOlderPaketIn (scenarioTempPath scenario @@ ".paket")

    let env = ("PaketExePath", snd paketToolPath) :: List.filter (fun (name, _) -> name <> "PaketExePath") env
    let output = restoreOutput env
    output |> shouldContainText $"Paket version %s{version}"
    output |> shouldNotContainText "PAKET0001"

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

/// Runs the RestorePackages target of paket.targets, as before a build, and gives its output
let runPaketTargetsRestore env =
    directDotnetEx env false "msbuild legacy.csproj /t:RestorePackages" (scenarioTempPath legacyScenario @@ "legacy")
    |> Seq.map (fun msg -> msg.Message)
    |> String.concat Environment.NewLine

/// Turns auto-restore on and installs the paket under test on the PATH like a global tool. Gives a
/// function that runs the RestorePackages target of paket.targets, as before a build, and gives its
/// output
let paketTargetsRestore env =
    let scenarioPath = scenarioTempPath legacyScenario
    directPaket "auto-restore on" legacyScenario |> ignore
    let toolPath = scenarioPath @@ "global-tools"
    installTool legacyScenario env $"--tool-path \"%s{toolPath}\""

    let path = toolPath + string Path.PathSeparator + Environment.GetEnvironmentVariable "PATH"
    fun () -> runPaketTargetsRestore (("PATH", path) :: env)

[<Test>]
let ``paket.targets of auto-restore restores with paket on the PATH like a global tool``() =
    let cleanup, version, env = prepareSdkForTool legacyScenario
    use __ = cleanup
    let restorePackages = paketTargetsRestore env

    restorePackages () |> shouldContainText $"Paket version %s{version}"

[<Test>]
let ``paket.targets of auto-restore runs the local tool of the manifest instead of a paket installed with --tool-path``() =
    let cleanup, version, env = prepareSdkForTool legacyScenario
    use __ = cleanup
    directPaket "auto-restore on" legacyScenario |> ignore
    installTool legacyScenario env "--local"
    leaveOlderPaketIn (scenarioTempPath legacyScenario @@ ".paket")

    let output = runPaketTargetsRestore env
    output |> shouldContainText $"Paket version %s{version}"
    output |> shouldContainText "warning PAKET0001"

[<Test>]
let ``paket.targets of auto-restore skips the restore once paket.lock is restored``() =
    let cleanup, version, env = prepareSdkForTool legacyScenario
    use __ = cleanup
    let restorePackages = paketTargetsRestore env
    directPaket "restore" legacyScenario |> ignore
    // with the "storage: none" of the scenario, the missing packages folder doesn't force the restore
    Directory.Exists(scenarioTempPath legacyScenario @@ "packages") |> shouldEqual false
    restorePackages () |> shouldNotContainText "Paket version"
    // nor does a restore of the project for this paket.lock mark the cache stale
    directPaket "restore --references-file legacy/paket.references" legacyScenario |> ignore
    restorePackages () |> shouldNotContainText "Paket version"

    File.AppendAllText(scenarioTempPath legacyScenario @@ "paket.lock", Environment.NewLine)
    restorePackages () |> shouldContainText $"Paket version %s{version}"

[<Test>]
let ``paket.targets of auto-restore restores after the restore of a project for another paket.lock, until a full restore``() =
    let cleanup, version, env = prepareSdkForTool legacyScenario
    use __ = cleanup
    let restorePackages = paketTargetsRestore env
    directPaket "restore" legacyScenario |> ignore

    let lockFile = scenarioTempPath legacyScenario @@ "paket.lock"
    let restoredLock = File.ReadAllBytes lockFile
    File.AppendAllText(lockFile, Environment.NewLine)
    restorePackages () |> shouldContainText $"Paket version %s{version}"
    // as after a switch back to the branch of the last full restore: the restore of the project,
    // for the other paket.lock, can have changed the packages folder
    File.WriteAllBytes(lockFile, restoredLock)
    restorePackages () |> shouldContainText $"Paket version %s{version}"

    directPaket "restore" legacyScenario |> ignore
    restorePackages () |> shouldNotContainText "Paket version"

[<Test>]
let ``paket.targets of auto-restore restores while paket.local exists, and after it is gone``() =
    let cleanup, version, env = prepareSdkForTool legacyScenario
    use __ = cleanup
    let restorePackages = paketTargetsRestore env
    directPaket "restore" legacyScenario |> ignore

    let localFile = scenarioTempPath legacyScenario @@ "paket.local"
    File.WriteAllText(localFile, "")
    restorePackages () |> shouldContainText $"Paket version %s{version}"
    // the next restore with paket.local finds packagesDownloadedHash cleared already, and still keeps
    // projectsRestoredHash, or the build of SDK projects would restore on every build
    restorePackages () |> shouldContainText $"Paket version %s{version}"
    let cacheFile = scenarioTempPath legacyScenario @@ "paket-files" @@ "paket.restore.cached"
    File.ReadAllText cacheFile |> shouldNotContainText "\"projectsRestoredHash\": \"\""
    // the restore with paket.local can have put other packages in the packages folder
    File.Delete localFile
    restorePackages () |> shouldContainText $"Paket version %s{version}"
    // and now the projects too, so that the build of SDK projects runs a full restore
    File.ReadAllText cacheFile |> shouldContainText "\"projectsRestoredHash\": \"\""

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

[<Test>]
let ``paket.targets of auto-restore restores when the packages folder is missing, until a full restore``() =
    let cleanup, version, env = prepareSdkForTool legacyScenario
    use __ = cleanup
    let scenarioPath = scenarioTempPath legacyScenario
    let packagesFolder = addPackagesFolderGroup scenarioPath
    let restorePackages = paketTargetsRestore env
    directPaketInPathExWithEnv "install" scenarioPath env |> ignore
    Directory.Exists packagesFolder |> shouldEqual true
    restorePackages () |> shouldNotContainText "Paket version"

    Directory.Delete(packagesFolder, true)
    restorePackages () |> shouldContainText $"Paket version %s{version}"
    // as for the next project of the build, which finds the packages folder that the restore of the
    // first one made with only its own packages
    Directory.CreateDirectory packagesFolder |> ignore
    restorePackages () |> shouldContainText $"Paket version %s{version}"

    directPaketInPathExWithEnv "restore" scenarioPath env |> ignore
    restorePackages () |> shouldNotContainText "Paket version"

[<Test>]
let ``paket.targets of auto-restore restores when the packages folder of a package with a storage of its own is missing``() =
    let cleanup, version, env = prepareSdkForTool legacyScenario
    use __ = cleanup
    let scenarioPath = scenarioTempPath legacyScenario
    // all the groups have "storage: none", but not the package
    let packagesFolder =
        addPackagesFolderGroupWith scenarioPath "storage: none\nnuget Paket.IntegrationTests.Empty storage: packages\n"
    let restorePackages = paketTargetsRestore env
    directPaketInPathExWithEnv "install" scenarioPath env |> ignore
    Directory.Exists packagesFolder |> shouldEqual true
    restorePackages () |> shouldNotContainText "Paket version"

    Directory.Delete(packagesFolder, true)
    restorePackages () |> shouldContainText $"Paket version %s{version}"

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
