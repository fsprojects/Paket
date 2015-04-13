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
    |> shouldEqual (GetHomeDirectory() + Path.DirectorySeparatorChar.ToString()  +  "data")
