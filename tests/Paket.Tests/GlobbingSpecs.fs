module Paket.GlobbingSpecs

open System.IO
open Paket
open NUnit.Framework
open FsUnit

let private withFiles files f =
    let folder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())
    for file : string in files do
        let path = Path.Combine(folder, file)
        Directory.CreateDirectory(Path.GetDirectoryName path) |> ignore
        File.WriteAllText(path, "")
    try
        f folder
    finally
        Directory.Delete(folder, true)

let private files = [ "root/a.dll"; "root/sub/b.dll"; "root/sub/c.cs"; "other/d.cs" ]

let private fullPaths (paths : string list) = paths |> List.map Path.GetFullPath |> List.sort

let private under folder (paths : string list) = paths |> List.map (fun p -> Path.Combine(folder, p) |> Path.GetFullPath) |> List.sort

[<Test>]
let ``search finds the files that match a pattern``() =
    withFiles files (fun folder ->
        let root = Path.Combine(folder, "root")
        Globbing.search root "*.dll" |> fullPaths |> shouldEqual (under folder [ "root/a.dll" ])
        Globbing.search root "**/*.dll" |> fullPaths |> shouldEqual (under folder [ "root/a.dll"; "root/sub/b.dll" ])
        Globbing.search root "../other/**/*.cs" |> fullPaths |> shouldEqual (under folder [ "other/d.cs" ]))

[<Test>]
let ``search finds a file named without wildcards only if it exists``() =
    withFiles files (fun folder ->
        let root = Path.Combine(folder, "root")
        Globbing.search root "sub/c.cs" |> fullPaths |> shouldEqual (under folder [ "root/sub/c.cs" ])
        Globbing.search root "sub/missing.cs" |> shouldEqual [])

[<Test>]
let ``template files that start with a separator are relative to the template folder``() =
    withFiles files (fun folder ->
        let root = Path.Combine(folder, "root")
        Globbing.searchTemplateFiles root "/sub/*.cs" |> fullPaths |> shouldEqual (under folder [ "root/sub/c.cs" ])
        Globbing.searchTemplateFiles root (Path.Combine(root, "sub", "*.cs")) |> fullPaths |> shouldEqual (under folder [ "root/sub/c.cs" ]))

[<Test>]
let ``a template can name its own folder by its absolute path``() =
    withFiles files (fun folder ->
        let root = Path.Combine(folder, "root")
        Globbing.searchTemplateFiles root root |> fullPaths |> shouldEqual (under folder [ "root" ]))

[<Test>]
let ``search with ** at the end finds the folder and everything under it``() =
    withFiles files (fun folder ->
        let root = Path.Combine(folder, "root")
        Globbing.search root "sub/**" |> fullPaths
        |> shouldEqual (under folder [ "root/sub"; "root/sub/b.dll"; "root/sub/c.cs" ]))

[<Test>]
let ``search from the current folder keeps the dots of the pattern``() =
    withFiles [ "feed/My.Package.1.0.0.nupkg"; "feed/MyPackage.1.0.0.nupkg" ] (fun folder ->
        use _ = TestHelpers.changeWorkingDir folder
        Globbing.search "." "**/*My.Package*" |> fullPaths |> shouldEqual (under folder [ "feed/My.Package.1.0.0.nupkg" ]))

[<Test>]
let ``isMatch matches a path against a pattern``() =
    let root = Path.Combine(Path.GetTempPath(), "root")
    Globbing.isMatch (Path.Combine(root, "nupkg", "*")) (Path.Combine(root, "nupkg", "a.nupkg")) |> shouldEqual true
    Globbing.isMatch (Path.Combine(root, "**", "*.cs")) (Path.Combine(root, "sub", "c.cs")) |> shouldEqual true
    Globbing.isMatch (Path.Combine(root, "nupkg", "*")) (Path.Combine(root, "sub", "c.cs")) |> shouldEqual false
