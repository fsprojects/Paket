module Paket.PackagingProcess.Test

open System.IO
open System.Reflection
open Paket
open FsUnit
open NUnit.Framework

[<Test>]
let ``Loading assembly metadata works``() = 
    let workingDir = Path.GetFullPath(".")
    
    let fileName =
        Path.Combine(workingDir, "..", "..", "Paket.Tests.fsproj")
        |> normalizePath
    
    if File.Exists fileName |> not then
        failwithf "%s does not exist." fileName

    let projFile = 
        fileName
        |> ProjectFile.LoadFromFile
    
    let config = 
        if workingDir.Contains "Debug" then "Debug"
        else "Release"
    
    let assemblyReader,id,versionFromAssembly,fileName = PackageMetaData.readAssemblyFromProjFile config "" projFile    
    id |> shouldEqual "Paket.Tests"
    
    let attribs = PackageMetaData.loadAssemblyAttributes assemblyReader
    PackageMetaData.getVersion versionFromAssembly attribs |> shouldEqual <| Some(SemVer.Parse "1.0.0.0")
    let authors = PackageMetaData.getAuthors attribs
    authors.Value |> shouldEqual ["Two"; "Authors" ]
    PackageMetaData.getDescription attribs |> shouldEqual <| Some("A description")