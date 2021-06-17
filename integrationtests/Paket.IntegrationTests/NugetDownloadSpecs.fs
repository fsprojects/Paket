module Paket.IntegrationTests.NugetDownloadSpecs

open System
open NUnit.Framework
open FsUnit
open System
open System.IO
open System.Diagnostics

let rec filesInDir (dir: DirectoryInfo) = seq {
    for info in dir.EnumerateFileSystemInfos() do
     match info with
     | :? DirectoryInfo as dir -> yield! filesInDir dir
     | :? FileInfo as file -> yield file
     | _ -> ()
}

[<Test>]
let ``download extracts with correct permissions``() =
    let scenario = "extracted-package-permissions"
    let tempScenarioDir = scenarioTempPath scenario
    use cleanup = prepare scenario
    let localNugetDir = Path.Combine(tempScenarioDir, ".nuget")
    let _ = directPaketInPathExWithEnv "restore" (scenarioTempPath "extracted-package-permissions") [Paket.Constants.GlobalPackagesFolderEnvironmentKey, localNugetDir]
    // after this install, the package should have been extracted. we 
    let dirWithBinaries = Path.Combine(localNugetDir, "microsoft.playwright", "1.12.1", "Drivers", "node") |> DirectoryInfo
    if not dirWithBinaries.Exists then Assert.Fail($"Expected the per-test nuget cache to exist.")
    for executable in filesInDir dirWithBinaries do
        let hasFlag = executable.Attributes.HasFlag(FileAttributes.ReadOnly)
        Assert.False(hasFlag, $"File {executable.FullName} was expected to extract with the write flag set, but it is read-only")
