(**
# Using Paket from F# Interactive

This page demonstrates how to use Paket from the F# Interactive.

## Download latest `paket.exe` (optional)

As first step we need to download and reference the latest Paket executable.
This boilerplate code allows F# scripts to work self-contained without an
installed `paket.exe`. Alternativly you can just reference any paket.exe that
you have on your system.
*)

open System
open System.IO

#r "../../packages/build/Pri.LongPath/lib/net45/Pri.LongPath.dll"

open Pri.LongPath

Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

if not (File.Exists "paket.exe") then
    let url = "http://fsprojects.github.io/Paket/stable"
    use wc = new Net.WebClient()
    let tmp = Path.GetTempFileName()
    let stable = wc.DownloadString(url)
    wc.DownloadFile(stable, tmp)
    File.Move(tmp,Path.GetFileName stable)

#r "paket.exe"

(**
## Configure which `paket.dependencies` file to use

Now we need open the `Paket` namespace and to tell Paket which
[`paket.dependencies`](dependencies-file.html) file to use.
*)

open Paket

// Locate the paket.dependencies file.
let dependencies = Dependencies.Locate(__SOURCE_DIRECTORY__)
// [fsi:found: paket.dependencies]

(**
## Adding and removing NuGet packages

Paket allows to install and uninstall NuGet packages programmatically:
*)

// Install a package.
dependencies.Add "FSharp.Data"

// Check which version is installed.
dependencies.GetInstalledVersion "FSharp.Data"
// [fsi:val it : string option = Some "2.1.0"]

// Remove a package.
dependencies.Remove "FSharp.Data"

// Check again which version is installed.
dependencies.GetInstalledVersion "FSharp.Data"
// [fsi:val it : string option = None]

(**
## Query the install model

It's possible to do queries against the installed NuGet packages:
*)

// Install some packages.
dependencies.Add "FSharp.Data"
dependencies.Add "FSharp.Formatting"
dependencies.Add "FsUnit"

// List all installed packages.
dependencies.GetInstalledPackages()
// [fsi:val it : (string * string * string) list =]
// [fsi:  [("Main", "FSharp.Compiler.Service", "0.0.67"); ("Main", "FSharp.Data", "2.1.0");]
// [fsi:   ("Main", "FSharp.Formatting", "2.4.36"); ("Main", "FsUnit", "1.3.0.1");]
// [fsi:   ("Main", "Microsoft.AspNet.Razor", "2.0.30506.0"); ("Main", "NUnit", "2.6.3");]
// [fsi:   ("Main", "RazorEngine", "3.3.0"); ("Main", "Zlib.Portable", "1.10.0")]]

// List only the direct dependencies.
dependencies.GetDirectDependencies()
// [fsi:val it : (string * string * string) list =]
// [fsi:  [("Main", "FSharp.Data", "2.1.0"); ("Main", "FSharp.Formatting", "2.4.36");]
// [fsi:   ("Main", "FsUnit", "1.3.0.1")]]

// List direct dependencies for the given package.
dependencies.GetDirectDependenciesForPackage("Main", "FSharp.Compiler.Service")
// [fsi:val it : (string * string * string) list =]
// [fsi:  ...]

// List all usages of a package in Paket.
let paketDependencies = Dependencies.Locate(Path.Combine(__SOURCE_DIRECTORY__,".."))
// [fsi:found: D:\code\Paket\paket.dependencies]
paketDependencies.FindReferencesFor "UnionArgParser"
// [fsi:val it : string list = ["D:\code\Paket\src\Paket\Paket.fsproj"]]
