module Paket.ProjectFile.SymlinkLoopSpecs

open System
open System.IO
open Paket
open NUnit.Framework
open FsUnit

// Regression: a cyclic directory symlink (e.g. inside a Nix `.devenv` profile)
// used to recurse project discovery forever.
[<Test>]
[<Timeout(120000)>]
let ``FindAllProjectFiles terminates on symlink cycles and finds each project once`` () =
    if isWindows then
        Assert.Ignore "directory symlinks need elevation on Windows"
    else
        let root = Path.Combine(Path.GetTempPath(), "paket-symlink-loop-" + Guid.NewGuid().ToString("N"))
        let loopLink = Path.Combine(root, "sub", "loop")
        try
            Directory.CreateDirectory(Path.Combine(root, "sub")) |> ignore
            File.WriteAllText(Path.Combine(root, "Real.fsproj"), "<Project />")
            SymlinkUtils.makeDirectoryLink loopLink ".."   // sub/loop -> root

            ProjectFile.FindAllProjectFiles root
            |> Array.filter (fun fi -> fi.Name = "Real.fsproj")
            |> Array.length
            |> shouldEqual 1
        finally
            // drop the link first so the recursive delete cannot follow it
            (try SymlinkUtils.delete loopLink with _ -> ())
            (try Directory.Delete(root, true) with _ -> ())
