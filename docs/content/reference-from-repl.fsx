(**
# Using Paket from F# Interactive

## Configure which paket.dependencies file to use

This page demonstrates how to use `Paket` from the F# Interactive. 
First we have to download and reference the paket tool, open the Paket namespace and to tell Paket which [`paket.dependencies`](dependencies-file.html) file to use.
*)

//------------------------------------------
// Step 0. Boilerplate to get the paket.exe tool

open System
open System.IO
 
Environment.CurrentDirectory <- __SOURCE_DIRECTORY__
 
if not (File.Exists "paket.exe") then
    let url = "http://fsprojects.github.io/Paket/stable"
    use wc = new Net.WebClient() in let tmp = Path.GetTempFileName() in let stable = wc.DownloadString(url) in wc.DownloadFile(stable, tmp); File.Move(tmp,Path.GetFileName stable)
 
// Step 1. Resolve and install the packages 

#r "paket.exe"

open Paket

// locate the paket.dependencies file
let dependencies = Dependencies.Locate(__SOURCE_DIRECTORY__)
// [fsi:found: paket.dependencies]

(**
## Adding and removing NuGet packages

Paket allows to install and uninstall NuGet packages programmatically:
*)

// install a package
dependencies.Add "FSharp.Data"
// [fsi:Adding FSharp.Data to paket.dependencies]
// [fsi:Resolving packages:]
// [fsi:  - fetching versions for FSharp.Data]
// [fsi:    - exploring FSharp.Data 2.1.0]
// [fsi:  - fetching versions for Zlib.Portable]
// [fsi:    - exploring Zlib.Portable 1.10.0]
// [fsi:Locked version resolutions written to paket.lock]
// [fsi:Zlib.Portable 1.10.0 unzipped to packages\Zlib.Portable]
// [fsi:FSharp.Data 2.1.0 unzipped to packages\FSharp.Data]
// [fsi:Dependencies files saved to paket.dependencies]

// check which version is installed
dependencies.GetInstalledVersion "FSharp.Data"
// [fsi:val it : string option = Some "2.1.0"]

// uninstall a package
dependencies.Remove "FSharp.Data"
// [fsi:Removing FSharp.Data from paket.dependencies]
// [fsi:Resolving packages:]
// [fsi:Locked version resolutions written to paket.lock]
// [fsi:Dependencies files saved to paket.dependencies]

// check again which version is installed
dependencies.GetInstalledVersion "FSharp.Data"
// [fsi:val it : string option = None]

(**
## Query the install model

It's possible to do queries against the installed NuGet packages:
*)

// install some packages
dependencies.Add "FSharp.Data"
dependencies.Add "FSharp.Formatting"
dependencies.Add "FsUnit"

// list all installed packages
dependencies.GetInstalledPackages()
// [fsi:val it : (string * string) list =]
// [fsi:  [("FSharp.Compiler.Service", "0.0.67"); ("FSharp.Data", "2.1.0");]
// [fsi:   ("FSharp.Formatting", "2.4.36"); ("FsUnit", "1.3.0.1");]
// [fsi:   ("Microsoft.AspNet.Razor", "2.0.30506.0"); ("NUnit", "2.6.3");]
// [fsi:   ("RazorEngine", "3.3.0"); ("Zlib.Portable", "1.10.0")]]

// list only the direct dependencies
dependencies.GetDirectDependencies()
// [fsi:val it : (string * string) list =]
// [fsi:  [("FSharp.Data", "2.1.0"); ("FSharp.Formatting", "2.4.36");]
// [fsi:   ("FsUnit", "1.3.0.1")]]

// list all usages of a package in Paket
let paketDependencies = Dependencies.Locate(System.IO.Path.Combine(__SOURCE_DIRECTORY__,".."))
// [fsi:found: D:\code\Paket\paket.dependencies]
paketDependencies.FindReferencesFor "UnionArgParser"
// [fsi:val it : string list = ["D:\code\Paket\src\Paket\Paket.fsproj"]]