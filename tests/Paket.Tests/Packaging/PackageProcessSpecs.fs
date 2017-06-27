module Paket.PackagingProcess.Test

open System.IO
open System.Reflection
open Paket
open FsUnit
open NUnit.Framework

[<Test>]
let ``Loading assembly metadata works``() = 
    let workingDir = Path.GetFullPath(".")
    
    let trials =
        [ Path.Combine(workingDir, "Paket.Tests.fsproj")
          Path.Combine(workingDir, "tests", "Paket.Tests", "Paket.Tests.fsproj")
          Path.Combine(workingDir, "..", "..", "Paket.Tests.fsproj") ]
    match trials |> Seq.tryFind File.Exists with
    | None ->
        failwithf "Paket.Tests.fsproj was not found via '%s'. %A" workingDir trials
    | Some testFsProjFile ->

    let projFile = 
        testFsProjFile
        |> ProjectFile.LoadFromFile

    let config = 
        if Assembly.GetExecutingAssembly().Location.Contains "Debug" then "Debug"
        else "Release"
    
    let assemblyReader,id,versionFromAssembly,fileName = PackageMetaData.readAssemblyFromProjFile config "" projFile
    id |> shouldEqual "Paket.Tests"
    
    let attribs = PackageMetaData.loadAssemblyAttributes assemblyReader
    PackageMetaData.getVersion versionFromAssembly attribs |> shouldEqual <| Some(SemVer.Parse "1.0.0.0")
    let authors = PackageMetaData.getAuthors attribs
    authors.Value |> shouldEqual ["Two"; "Authors" ]
    PackageMetaData.getDescription attribs |> shouldEqual <| Some("A description")