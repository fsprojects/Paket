module Paket.PackagingProcess.Test

open System.IO
open System.Reflection
open Paket
open FsUnit
open NUnit.Framework

let assembly = Assembly.GetExecutingAssembly()

[<Test>]
let ``Loading description from assembly works``() = 
    let sut = PackageMetaData.getDescription (assembly.GetCustomAttributes(true)) 
    sut.Value |> shouldEqual "A description"

[<Test>]
let ``Loading version from assembly works``() = 
    let sut = PackageMetaData.getVersion assembly (assembly.GetCustomAttributes(true)) 
    sut.Value |> shouldEqual (SemVer.Parse "1.0.0.0")

[<Test>]
let ``Loading authors from assembly works``() = 
    let sut = PackageMetaData.getAuthors (assembly.GetCustomAttributes(true))
    sut.Value |> shouldEqual [ "Two"; "Authors" ]

[<Test>]
let ``Loading id from assembly works``() = 
    let sut = PackageMetaData.getId assembly ProjectCoreInfo.Empty
    sut.Id.Value |> shouldEqual "Paket.Tests"

[<Test>]
let ``Loading assembly metadata works``() = 
    let workingDir = Path.GetFullPath(".")
    
    let projFile = 
        Path.Combine(workingDir, "..", "..", "Paket.Tests.fsproj")
        |> normalizePath
        |> ProjectFile.Load
    
    let config = 
        if workingDir.Contains "Debug" then "Debug"
        else "Release"

    let assembly,id = PackageMetaData.loadAssemblyId config projFile.Value
    id |> shouldEqual "Paket.Tests"
    
    let attribs = PackageMetaData.loadAssemblyAttributes assembly
    PackageMetaData.getVersion assembly attribs |> shouldEqual <| Some(SemVer.Parse "1.0.0.0")
    PackageMetaData.getAuthors attribs |> shouldEqual <| Some([ "Two"; "Authors" ])
    PackageMetaData.getDescription attribs |> shouldEqual <| Some("A description")