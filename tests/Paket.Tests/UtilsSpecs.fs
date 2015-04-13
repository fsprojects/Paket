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
    |> shouldEqual (AbsolutePath (GetHomeDirectory() + Path.DirectorySeparatorChar.ToString()  +  "data"))
        
[<Test>]
let ``relative local path is returned as is``() =
    "Externals/NugetStore" 
    |> normalizeLocalPath
    |> shouldEqual (RelativePath "Externals/NugetStore")
    
[<Test>]
let ``absolute path with drive letter``() =
    "c:\\Store" 
    |> normalizeLocalPath
    |> shouldEqual (AbsolutePath "c:\\Store")
    
[<Test>]
let ``relative path with drive letter``() =
    "..\\Store" 
    |> normalizeLocalPath
    |> shouldEqual (RelativePath "..\\Store")
    
[<Test>]
let ``SMB path is returned as absolute path``() =
    "\\\\server\\Store" 
    |> normalizeLocalPath
    |> shouldEqual (AbsolutePath "\\\\server\\Store")