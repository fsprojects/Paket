module Paket.ProjectFile.FindAllProjectFilesSpecs

open System.IO
open Paket
open NUnit.Framework
open FsUnit

let private findProjects folder =
    ProjectFile.FindAllProjectFiles folder |> Array.map (fun fi -> fi.FullName) |> Array.sort

/// Writes an empty project in the folder, creating it, and returns its full path
let private writeProject folder name =
    Directory.CreateDirectory(folder) |> ignore
    let path = Path.Combine(folder, name)
    File.WriteAllText(path, "<Project />")
    path

// Regression: a cyclic directory link (e.g. inside a Nix .devenv profile) used to recurse project discovery forever
[<Test>]
[<Timeout 60000>]
let ``FindAllProjectFiles should terminate on a link cycle behind a link``() =
    TestHelpers.withTempDir (fun root ->
        let outside = Path.Combine(root, "outside")
        let search = Path.Combine(root, "search")
        writeProject outside "Outside.fsproj" |> ignore
        Directory.CreateDirectory(Path.Combine(outside, "sub")) |> ignore
        let project = writeProject search "Real.fsproj"
        TestHelpers.createDirectoryLinkOrIgnore (Path.Combine(search, "linked")) outside
        TestHelpers.createDirectoryLinkOrIgnore (Path.Combine(outside, "sub", "loop")) ".."

        findProjects search |> shouldEqual [| project |])

[<Test>]
let ``FindAllProjectFiles should not descend into the folders that are links``() =
    TestHelpers.withTempDir (fun root ->
        let outside = Path.Combine(root, "outside")
        let search = Directory.CreateDirectory(Path.Combine(root, "search")).FullName
        writeProject outside "Linked.fsproj" |> ignore
        TestHelpers.createDirectoryLinkOrIgnore (Path.Combine(search, "linked")) outside

        findProjects search |> shouldEqual [||])

[<Test>]
let ``FindAllProjectFiles should not descend into dot folders``() =
    TestHelpers.withTempDir (fun root ->
        writeProject (Path.Combine(root, ".devenv")) "Hidden.fsproj" |> ignore
        let project = writeProject (Path.Combine(root, "src")) "Visible.fsproj"

        findProjects root |> shouldEqual [| project |])
