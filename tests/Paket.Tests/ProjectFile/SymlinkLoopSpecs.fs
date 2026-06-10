module Paket.ProjectFile.SymlinkLoopSpecs

open System
open System.IO
open Paket
open NUnit.Framework
open FsUnit

// Regression test: ProjectFile.FindAllProjectFiles walks a directory tree to find
// every *proj* file. A self-referential or cyclic directory symlink (e.g. the macOS
// SDK ncurses symlink loops inside a Nix `.devenv` profile) used to drive that walk
// into unbounded recursion. The walk now resolves directories to their canonical
// (symlink-followed) path and prunes ones it has already visited, so it terminates
// and reports each physical project exactly once.
[<Test>]
[<Timeout(120000)>]
let ``FindAllProjectFiles terminates on symlink cycles and finds each project once`` () =
    if isWindows then
        // Directory-symlink loops of this kind arise on Unix; creating directory
        // symlinks on Windows needs elevation and the scenario does not apply.
        Assert.Ignore "symlink-loop scenario is Unix-only"
    else
        let root =
            Path.Combine(Path.GetTempPath(), "paket-symlink-loop-" + Guid.NewGuid().ToString("N"))

        Directory.CreateDirectory root |> ignore
        let loopLink = Path.Combine(root, "sub", "loop")

        try
            // A real project that must still be discovered — exactly once.
            File.WriteAllText(Path.Combine(root, "Real.fsproj"), "<Project />")

            let sub = Path.Combine(root, "sub")
            Directory.CreateDirectory sub |> ignore

            // `sub/loop` -> `..` points back to `root`: a cycle that, without cycle
            // detection, makes the recursive search descend forever (re-finding the
            // project on every lap until the path outgrows PATH_MAX).
            SymlinkUtils.makeDirectoryLink loopLink ".."

            let found = ProjectFile.FindAllProjectFiles root

            found
            |> Array.filter (fun fi -> fi.Name = "Real.fsproj")
            |> Array.length
            |> shouldEqual 1
        finally
            // Remove the symlink before the recursive delete so it can't be followed.
            (try SymlinkUtils.delete loopLink with _ -> ())
            (try Directory.Delete(root, true) with _ -> ())
