module Paket.UtilsSpecs

open Paket
open NUnit.Framework
open FsUnit

[<Test>]
let ``createRelativePath should handle spaces``() =
    "C:/some file" 
    |> createRelativePath "C:/a/b" 
    |> shouldEqual "..\\some file"

    
[<Test>]
let ``relative local path is returned as is``() =
    "Externals/NugetStore" 
    |> normalizeLocalPath
    |> shouldEqual (RelativePath "Externals/NugetStore")
    
[<Test>]
let ``absolute path with drive letter``() =
    "c:\Store" 
    |> normalizeLocalPath
    |> shouldEqual (AbsolutePath "c:\Store")
    
[<Test>]
let ``SMB path is returned as absolute path``() =
    "\\\\server\\Store" 
    |> normalizeLocalPath
    |> shouldEqual (AbsolutePath "\\\\server\\Store")