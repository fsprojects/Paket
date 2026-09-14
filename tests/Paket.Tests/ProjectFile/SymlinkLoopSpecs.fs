module Paket.ProjectFile.SymlinkLoopSpecs

open System
open System.IO
open Paket
open NUnit.Framework
open FsUnit

let private withTempRoot f =
    if isWindows then Assert.Ignore "directory symlinks need elevation on Windows"
    let root = Path.Combine(Path.GetTempPath(), "paket-symlink-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory root |> ignore
    try f root
    finally Directory.Delete(root, true)

// Regression: a cyclic directory symlink (e.g. inside a Nix `.devenv` profile)
// used to recurse project discovery forever.
[<Test>]
[<Timeout(120000)>]
let ``FindAllProjectFiles terminates on symlink cycles and finds each project once`` () =
    withTempRoot (fun root ->
        let loopLink = Path.Combine(root, "sub", "loop")
        Directory.CreateDirectory(Path.Combine(root, "sub")) |> ignore
        File.WriteAllText(Path.Combine(root, "Real.fsproj"), "<Project />")
        SymlinkUtils.makeDirectoryLink loopLink ".."   // sub/loop -> root
        try
            ProjectFile.FindAllProjectFiles root
            |> Array.filter (fun fi -> fi.Name = "Real.fsproj")
            |> Array.length
            |> shouldEqual 1
        finally
            SymlinkUtils.delete loopLink)   // so the recursive delete cannot follow it

// Positive control: symlinks are still followed, and the project keeps the path it
// was reached by.
[<Test>]
let ``FindAllProjectFiles follows a symlink to a directory outside the root`` () =
    withTempRoot (fun root ->
        let outside = Path.Combine(root, "outside")
        let searchRoot = Path.Combine(root, "search")
        let link = Path.Combine(searchRoot, "linked")
        Directory.CreateDirectory outside |> ignore
        Directory.CreateDirectory searchRoot |> ignore
        File.WriteAllText(Path.Combine(outside, "Linked.fsproj"), "<Project />")
        SymlinkUtils.makeDirectoryLink link outside
        try
            ProjectFile.FindAllProjectFiles searchRoot
            |> Array.map (fun fi -> fi.FullName)
            |> shouldEqual [| Path.Combine(link, "Linked.fsproj") |]
        finally
            SymlinkUtils.delete link)
