module Paket.NugetConvertSpecs

open System.IO
open Paket
open NUnit.Framework
open FsUnit
open Chessie.ErrorHandling
open Paket.TestHelpers

let private configWithSource key =
    sprintf """<configuration>
  <packageSources>
    <add key="%s" value="https://%s.example/nuget/v3/index.json" />
  </packageSources>
</configuration>""" key key

[<Test>]
let ``NugetEnv.read should find the files of the .nuget folder and list the files to remove``() =
    withTempDir (fun root ->
        let nugetFolder = Path.Combine(root, ".nuget")
        Directory.CreateDirectory nugetFolder |> ignore
        File.WriteAllText(Path.Combine(nugetFolder, "NuGet.Config"), "<configuration />")
        File.WriteAllText(Path.Combine(nugetFolder, "NuGet.targets"), "<Project />")
        File.WriteAllText(Path.Combine(nugetFolder, "NuGet.exe"), "")
        let projectFolder = Path.Combine(root, "App")
        Directory.CreateDirectory projectFolder |> ignore
        File.WriteAllText(Path.Combine(projectFolder, "App.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />")
        File.WriteAllText(Path.Combine(projectFolder, "packages.config"), "<packages />")

        let nugetEnv = NuGetConvert.NugetEnv.read (DirectoryInfo root) |> returnOrFail

        // on a case-insensitive file system the nuget.config is named as NuGet looks for it
        nugetEnv.NuGetConfigFiles |> List.map (fun fi -> fi.Name.ToLowerInvariant()) |> shouldEqual [ "nuget.config" ]
        nugetEnv.NuGetTargets |> Option.map (fun fi -> fi.Name) |> shouldEqual (Some "NuGet.targets")
        nugetEnv.NuGetExe |> Option.map (fun fi -> fi.Name) |> shouldEqual (Some "NuGet.exe")
        NuGetConvert.NugetEnv.filesToRemove nugetEnv
        |> List.map (fun fi -> fi.FullName.ToLowerInvariant())
        |> shouldEqual
            ([ Path.Combine(nugetFolder, "NuGet.Config")
               Path.Combine(projectFolder, "packages.config")
               Path.Combine(nugetFolder, "NuGet.targets")
               Path.Combine(nugetFolder, "NuGet.exe") ]
             |> List.map (fun path -> path.ToLowerInvariant())))

[<Test>]
let ``NugetEnv.read should read the sources of .nuget/NuGet.Config``() =
    withTempDir (fun root ->
        let nugetFolder = Path.Combine(root, ".nuget")
        Directory.CreateDirectory nugetFolder |> ignore
        File.WriteAllText(Path.Combine(nugetFolder, "NuGet.Config"), configWithSource "corp")

        let nugetEnv = NuGetConvert.NugetEnv.read (DirectoryInfo root) |> returnOrFail

        nugetEnv.NuGetConfig.PackageSources |> Map.containsKey "corp" |> shouldEqual true)

[<Test>]
let ``NugetEnv.read should prefer the files of the .nuget folder``() =
    withTempDir (fun root ->
        let nugetFolder = Path.Combine(root, ".nuget")
        let toolsFolder = Path.Combine(root, "packages", "NuGet.CommandLine", "tools")
        Directory.CreateDirectory nugetFolder |> ignore
        Directory.CreateDirectory toolsFolder |> ignore
        File.WriteAllText(Path.Combine(nugetFolder, "NuGet.exe"), "")
        File.WriteAllText(Path.Combine(toolsFolder, "nuget.exe"), "")
        File.WriteAllText(Path.Combine(nugetFolder, "NuGet.Config"), "<configuration />")
        File.WriteAllText(Path.Combine(root, "nuget.config"), "<configuration />")

        let nugetEnv = NuGetConvert.NugetEnv.read (DirectoryInfo root) |> returnOrFail

        nugetEnv.NuGetExe |> Option.map (fun fi -> fi.FullName) |> shouldEqual (Some (Path.Combine(nugetFolder, "NuGet.exe")))
        // on a case-insensitive file system the names are the ones NuGet looks for
        nugetEnv.NuGetConfigFiles
        |> List.map (fun fi -> fi.FullName.ToLowerInvariant())
        |> shouldEqual [ Path.Combine(nugetFolder, "nuget.config").ToLowerInvariant(); Path.Combine(root, "nuget.config").ToLowerInvariant() ])

[<Test>]
let ``NugetEnv.read should read and remove the NuGet.Config of the root``() =
    withTempDir (fun root ->
        File.WriteAllText(Path.Combine(root, "NuGet.Config"), configWithSource "corp")

        let nugetEnv = NuGetConvert.NugetEnv.read (DirectoryInfo root) |> returnOrFail

        nugetEnv.NuGetConfig.PackageSources |> Map.containsKey "corp" |> shouldEqual true
        nugetEnv.NuGetConfigFiles
        |> List.map (fun fi -> fi.FullName.ToLowerInvariant())
        |> shouldEqual [ Path.Combine(root, "nuget.config").ToLowerInvariant() ])

[<Test>]
let ``NugetEnv.read should only read and remove the nuget.config that NuGet reads``() =
    withTempDir (fun root ->
        let nugetFolder = Path.Combine(root, ".nuget")
        Directory.CreateDirectory nugetFolder |> ignore
        File.WriteAllText(Path.Combine(nugetFolder, "nuget.config"), configWithSource "lower")
        File.WriteAllText(Path.Combine(nugetFolder, "NuGet.Config"), configWithSource "upper")
        if Directory.GetFiles(nugetFolder).Length < 2 then
            Assert.Ignore "The file system ignores the case of the file names"

        let nugetEnv = NuGetConvert.NugetEnv.read (DirectoryInfo root) |> returnOrFail

        nugetEnv.NuGetConfig.PackageSources |> Map.containsKey "lower" |> shouldEqual true
        nugetEnv.NuGetConfig.PackageSources |> Map.containsKey "upper" |> shouldEqual false
        nugetEnv.NuGetConfigFiles |> List.map (fun fi -> fi.FullName) |> shouldEqual [ Path.Combine(nugetFolder, "nuget.config") ])

[<Test>]
let ``deleteIfEmpty should delete an empty folder``() =
    withTempDir (fun root ->
        Directory.CreateDirectory(Path.Combine(root, ".nuget")) |> ignore

        NuGetConvert.deleteIfEmpty (DirectoryInfo(Path.Combine(root, ".nuget")))

        Directory.Exists(Path.Combine(root, ".nuget")) |> shouldEqual false)

[<Test>]
let ``deleteIfEmpty should keep a folder that is not empty``() =
    withTempDir (fun root ->
        Directory.CreateDirectory(Path.Combine(root, ".nuget")) |> ignore
        File.WriteAllText(Path.Combine(root, ".nuget", "packages.config"), "<packages />")

        NuGetConvert.deleteIfEmpty (DirectoryInfo(Path.Combine(root, ".nuget")))

        File.Exists(Path.Combine(root, ".nuget", "packages.config")) |> shouldEqual true)
