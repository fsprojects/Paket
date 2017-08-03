module Paket.IntegrationTests.HttpSpecs

open Fake
open System
open NUnit.Framework
open FsUnit
open System
open System.IO
open Pri.LongPath
open System.Diagnostics
open Paket

[<Test>]
let ``#1341 http dlls``() = 
    prepare "i001341-http-dlls"
    let root = scenarioTempPath "i001341-http-dlls"
    let deps = sprintf """group Files

http file:///%s/library.dll library/library.dll""" (root.Replace("\\","/"))
   
    let monoDeps = sprintf """group Files

http file://%s/library.dll library/library.dll""" (root.Replace("\\","/"))

    File.WriteAllText(Path.Combine(root,"paket.dependencies"),if isMonoRuntime then monoDeps else deps)

    directPaket "update" "i001341-http-dlls" |> ignore
    
    let newFile = Path.Combine(scenarioTempPath "i001341-http-dlls","HttpDependencyToProjectReference","HttpDependencyToProjectReference.csproj")
    let oldFile = Path.Combine(originalScenarioPath "i001341-http-dlls","HttpDependencyToProjectReference","HttpDependencyToProjectReference.csprojtemplate")
    let s1 = File.ReadAllText oldFile |> normalizeLineEndings
    let s2 = File.ReadAllText newFile |> normalizeLineEndings
    s2 |> shouldEqual s1

    Directory.Exists(Path.Combine(root,"HttpDependencyToProjectReference","paket-files")) |> shouldEqual false