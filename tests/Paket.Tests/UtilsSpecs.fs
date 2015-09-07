module Paket.UtilsSpecs

open System.IO
open Paket
open NUnit.Framework
open FsUnit

[<Test>]
let ``createRelativePath should handle spaces``() =
    "C:/some file" 
    |> createRelativePath "C:/a/b" 
    |> shouldEqual "..\\some file"
        
[<Test>]
let ``normalize path with home directory``() =
    "~/data" 
    |> Utils.normalizeLocalPath
    |> shouldEqual (AbsolutePath (Path.Combine(GetHomeDirectory(), "data")))
        
[<Test>]
let ``relative local path is returned as is``() =
    "Externals/NugetStore" 
    |> normalizeLocalPath
    |> shouldEqual (RelativePath "Externals/NugetStore")
    
[<Test>]
let ``absolute path with drive letter``() =
    "c:\\Store" 
    |> normalizeLocalPath
    |> match System.Environment.OSVersion.Platform with
        | System.PlatformID.Win32NT -> shouldEqual (AbsolutePath "c:\\Store")
        | _ -> shouldEqual (RelativePath "c:\\Store")
    
[<Test>]
let ``relative path with drive letter``() =
    "..\\Store" 
    |> normalizeLocalPath
    |> shouldEqual (RelativePath "..\\Store")
    
[<Test>]
let ``relative path with local identifier``() =
    ".\\Store" 
    |> normalizeLocalPath
    |> shouldEqual (RelativePath ".\\Store")

[<Test>]
let ``SMB path is returned as absolute path``() =
    "\\\\server\\Store" 
    |> normalizeLocalPath
    |> match System.Environment.OSVersion.Platform with
        | System.PlatformID.Win32NT | System.PlatformID.Win32S -> shouldEqual (AbsolutePath "\\\\server\\Store")
        | _ -> shouldEqual (RelativePath "\\\\server\\Store")
    
[<Test>]
let ``absolute path on unixoid systems``() =
    "/server/Store" 
    |> normalizeLocalPath
    |> shouldEqual (AbsolutePath "/server/Store")
    
[<Test>]
let ``relative path with local identifier on unxoid systems``() =
    "./Store" 
    |> normalizeLocalPath
    |> shouldEqual (RelativePath "./Store")

[<Test>]
[<Platform "Mono">]
let ``mono runtime reported on mono platform``() =
    isMonoRuntime |>
    shouldEqual true

[<Test>]
[<Platform "Net">]
let ``mono runtime not reported on net platform``() =
    isMonoRuntime |>
    shouldEqual false
