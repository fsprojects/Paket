module Paket.WritePackagesConfigSpecs

open Paket
open Paket.PackagesConfigFile
open NUnit.Framework
open FsUnit
open Domain
open System.IO
open Pri.LongPath

[<Test>]
let ``can write xunit.visualstudio.packages.config``() = 
    let fileName = "PackagesConfig/xunit.visualstudio.packages.config"
    let config = Read fileName
    let expected = File.ReadAllText fileName |> normalizeLineEndings
    Serialize config |> normalizeLineEndings |> shouldEqual expected

[<Test>]
let ``can write asp.net.packages.config``() = 
    let fileName = "PackagesConfig/asp.net.packages.config"
    let config = Read fileName
    let expected = File.ReadAllText fileName |> normalizeLineEndings
    Serialize config |> normalizeLineEndings |> shouldEqual expected