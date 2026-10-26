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
let ``updateSolutions should update the .sln and .slnx solutions and leave them unchanged on a second run``() =
    withTempDir (fun root ->
        Directory.CreateDirectory(Path.Combine(root, "sub")) |> ignore
        let sln, slnx, subSlnx = Path.Combine(root, "App.sln"), Path.Combine(root, "App.slnx"), Path.Combine(root, "sub", "Sub.slnx")
        File.WriteAllText(sln, "Microsoft Visual Studio Solution File, Format Version 12.00\nGlobal\nEndGlobal\n")
        File.WriteAllText(slnx, "<Solution>\n  <Project Path=\"App.csproj\" />\n</Solution>\n")
        File.WriteAllText(subSlnx, "<Solution />\n")
        let files = [ sln; slnx; subSlnx ]
        // found by the *.sln* search, but not solutions
        let otherFiles = [ Path.Combine(root, "App.sln.DotSettings"); Path.Combine(root, "App.slnf") ]
        for otherFile in otherFiles do
            File.WriteAllText(otherFile, "not a solution")

        let solutions = NuGetConvert.updateSolutions (DirectoryInfo root) []
        for solution in solutions do
            solution.Save()

        solutions |> Array.map (fun solution -> solution.FileName) |> Array.sort |> shouldEqual (Array.sort (Array.ofList files))
        otherFiles |> List.map File.ReadAllText |> shouldEqual [ "not a solution"; "not a solution" ]
        StringAssert.Contains("paket.dependencies = paket.dependencies", File.ReadAllText sln)
        StringAssert.Contains("<File Path=\"paket.dependencies\" />", File.ReadAllText slnx)
        StringAssert.Contains("<File Path=\"../paket.dependencies\" />", File.ReadAllText subSlnx)

        let converted = files |> List.map File.ReadAllBytes
        for solution in NuGetConvert.updateSolutions (DirectoryInfo root) [] do
            solution.Save()

        files |> List.map File.ReadAllBytes |> shouldEqual converted)

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
let ``NugetEnv.read should only read the nuget.config of a .nuget folder that is a symbolic link``() =
    withTempDir (fun root ->
        let outside = Path.Combine(root, "outside")
        Directory.CreateDirectory outside |> ignore
        File.WriteAllText(Path.Combine(outside, "NuGet.exe"), "")
        File.WriteAllText(Path.Combine(outside, "NuGet.Config"), configWithSource "outside")
        let repository = Path.Combine(root, "repository")
        Directory.CreateDirectory repository |> ignore
        File.WriteAllText(Path.Combine(repository, "nuget.config"), configWithSource "repository")
        createDirectoryLinkOrIgnore (Path.Combine(repository, ".nuget")) outside

        let nugetEnv, warnings = collectWarnings (fun () -> NuGetConvert.NugetEnv.read (DirectoryInfo repository) |> returnOrFail)

        warnings |> List.exists (fun warning -> warning.Contains(Path.Combine(repository, ".nuget")) && warning.Contains "is a link") |> shouldEqual true
        nugetEnv.NuGetExe |> shouldEqual None
        nugetEnv.NuGetConfigFiles |> List.map (fun fi -> fi.FullName) |> shouldEqual [ Path.Combine(repository, "nuget.config") ]
        nugetEnv.NuGetConfig.PackageSources |> Map.containsKey "outside" |> shouldEqual true
        nugetEnv.NuGetConfig.PackageSources |> Map.containsKey "repository" |> shouldEqual true)

[<Test>]
let ``NugetEnv.read and updateSolutions should skip the files, projects and solutions of the folders that are links``() =
    withTempDir (fun root ->
        let outside = Path.Combine(root, "outside")
        Directory.CreateDirectory outside |> ignore
        File.WriteAllText(Path.Combine(outside, "nuget.config"), configWithSource "outside")
        File.WriteAllText(Path.Combine(outside, "nuget.exe"), "")
        File.WriteAllText(Path.Combine(outside, "Shared.slnx"), "<Solution />")
        File.WriteAllText(Path.Combine(outside, "Lib.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />")
        File.WriteAllText(Path.Combine(outside, "packages.config"), "<packages />")
        let repository = Path.Combine(root, "repository")
        Directory.CreateDirectory repository |> ignore
        createDirectoryLinkOrIgnore (Path.Combine(repository, "shared")) outside

        let nugetEnv, warnings = collectWarnings (fun () -> NuGetConvert.NugetEnv.read (DirectoryInfo repository) |> returnOrFail)
        let solutions = NuGetConvert.updateSolutions (DirectoryInfo repository) []

        nugetEnv.NuGetProjectFiles |> shouldBeEmpty
        // one warning for the folder, none for each of its files
        warnings |> List.filter (fun warning -> warning.Contains(Path.Combine(repository, "shared"))) |> List.length |> shouldEqual 1
        NuGetConvert.NugetEnv.filesToRemove nugetEnv |> shouldBeEmpty
        solutions |> shouldBeEmpty)

[<Test>]
let ``NugetEnv.read should skip the project files that are links``() =
    withTempDir (fun root ->
        let outside = Path.Combine(root, "Outside.csproj")
        File.WriteAllText(outside, "<Project Sdk=\"Microsoft.NET.Sdk\" />")
        let repository = Path.Combine(root, "repository")
        Directory.CreateDirectory repository |> ignore
        File.WriteAllText(Path.Combine(repository, "packages.config"), "<packages />")
        createFileLinkOrIgnore (Path.Combine(repository, "Lib.csproj")) outside

        let nugetEnv, warnings = collectWarnings (fun () -> NuGetConvert.NugetEnv.read (DirectoryInfo repository) |> returnOrFail)

        nugetEnv.NuGetProjectFiles |> shouldBeEmpty
        warnings |> List.exists (fun warning -> warning.Contains(Path.Combine(repository, "Lib.csproj"))) |> shouldEqual true)

[<Test>]
let ``NugetEnv.read should warn once about a folder link that makes a cycle``() =
    withTempDir (fun root ->
        let app = Path.Combine(root, "app")
        Directory.CreateDirectory app |> ignore
        File.WriteAllText(Path.Combine(app, "App.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />")
        createDirectoryLinkOrIgnore (Path.Combine(app, "cycle")) app

        let nugetEnv, warnings = collectWarnings (fun () -> NuGetConvert.NugetEnv.read (DirectoryInfo root) |> returnOrFail)

        nugetEnv.NuGetProjectFiles |> List.map (fun (project, _) -> project.FileName) |> shouldEqual [ Path.Combine(app, "App.csproj") ]
        warnings |> List.filter (fun warning -> warning.Contains(Path.Combine(app, "cycle"))) |> List.length |> shouldEqual 1)

[<TestCase("App.sln", false)>]
[<TestCase("App.sln", true)>]
[<TestCase("App.slnx", false)>]
[<TestCase("App.slnx", true)>]
let ``updateSolutions should remove the entries of the deleted NuGet files only``(solutionName: string, nugetFolderIsLink: bool) =
    let content, entry =
        if solutionName.EndsWith ".slnx" then
            "<Solution>\n  <Folder Name=\"/.nuget/\">\n    <File Path=\".nuget/NuGet.Config\" />\n  </Folder>\n</Solution>\n", "<File Path=\".nuget/NuGet.Config\" />"
        else
            slnWithNuGetFolder [ @".nuget\NuGet.Config" ], @".nuget\NuGet.Config"
    withTempDir (fun root ->
        let nugetFolder = Path.Combine(root, if nugetFolderIsLink then "outside" else Path.Combine("repository", ".nuget"))
        Directory.CreateDirectory nugetFolder |> ignore
        File.WriteAllText(Path.Combine(nugetFolder, "NuGet.Config"), "<configuration />")
        let repository = Path.Combine(root, "repository")
        Directory.CreateDirectory repository |> ignore
        if nugetFolderIsLink then
            createDirectoryLinkOrIgnore (Path.Combine(repository, ".nuget")) nugetFolder
        let solutionFile = Path.Combine(repository, solutionName)
        File.WriteAllText(solutionFile, content)
        let nugetFiles =
            NuGetConvert.NugetEnv.read (DirectoryInfo repository) |> returnOrFail
            |> NuGetConvert.NugetEnv.filesToRemove
            |> List.map (fun fi -> fi.FullName)

        for solution in NuGetConvert.updateSolutions (DirectoryInfo repository) nugetFiles do
            solution.Save()

        // the files of a .nuget folder that is a link are not deleted, so their entries stay
        File.ReadAllText(solutionFile).Contains entry |> shouldEqual nugetFolderIsLink)

[<TestCase("App.sln")>]
[<TestCase("App.slnx")>]
let ``updateSolutions should leave the solutions that are symbolic links``(solutionName: string) =
    let content = if solutionName.EndsWith ".slnx" then "<Solution />" else slnWithNuGetFolder []
    withTempDir (fun root ->
        let outside = Path.Combine(root, "outside")
        File.WriteAllText(outside, content)
        let repository = Path.Combine(root, "repository")
        Directory.CreateDirectory repository |> ignore
        createFileLinkOrIgnore (Path.Combine(repository, solutionName)) outside

        let solutions, warnings = collectWarnings (fun () -> NuGetConvert.updateSolutions (DirectoryInfo repository) [])

        solutions |> shouldBeEmpty
        warnings |> List.exists (fun warning -> warning.Contains(Path.Combine(repository, solutionName))) |> shouldEqual true)

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
let ``updateSolutions should leave the slnx files it cannot read``() =
    withTempDir (fun root ->
        let slnx, broken = Path.Combine(root, "App.slnx"), Path.Combine(root, "broken.slnx")
        File.WriteAllText(slnx, "<Solution />")
        File.WriteAllText(broken, "not xml")

        let solutions, warnings = collectWarnings (fun () -> NuGetConvert.updateSolutions (DirectoryInfo root) [])

        warnings |> List.exists (fun warning -> warning.Contains broken && warning.Contains "leaves it unchanged") |> shouldEqual true
        solutions |> Array.map (fun solution -> solution.FileName) |> shouldEqual [| slnx |])

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

[<Test>]
let ``deleteIfEmpty should keep a folder that is a symbolic link``() =
    withTempDir (fun root ->
        let outside = Path.Combine(root, "outside")
        Directory.CreateDirectory outside |> ignore
        let repository = Path.Combine(root, "repository")
        Directory.CreateDirectory repository |> ignore
        createDirectoryLinkOrIgnore (Path.Combine(repository, ".nuget")) outside

        NuGetConvert.deleteIfEmpty (DirectoryInfo(Path.Combine(repository, ".nuget")))

        Directory.Exists(Path.Combine(repository, ".nuget")) |> shouldEqual true
        Directory.Exists outside |> shouldEqual true)

[<Test>]
let ``updateSolutions should leave the sln files it cannot update``() =
    withTempDir (fun root ->
        let slnx, truncated = Path.Combine(root, "App.slnx"), Path.Combine(root, "Truncated.sln")
        File.WriteAllText(slnx, "<Solution />")
        // ends on the line of the .paket solution folder, without its EndProject: adding paket.dependencies
        // to the folder cannot find its end, which makes the update fail
        File.WriteAllText(truncated, "Microsoft Visual Studio Solution File, Format Version 12.00\nProject(\"{2150E333-8FDC-42A3-9474-1A3956D46DE8}\") = \".paket\", \".paket\", \"{6A9DE8E5-3F35-4C0F-9F6B-0E2C46E3C0A1}\"\n")

        let solutions, warnings = collectWarnings (fun () -> NuGetConvert.updateSolutions (DirectoryInfo root) [])

        warnings |> List.exists (fun warning -> warning.Contains truncated) |> shouldEqual true
        solutions |> Array.map (fun solution -> solution.FileName) |> shouldEqual [| slnx |])
