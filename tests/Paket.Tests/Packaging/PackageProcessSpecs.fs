module Paket.PackagingProcess.Test

open System.IO
open Pri.LongPath
open System.Reflection
open Paket
open FsUnit
open NUnit.Framework

[<Test>]
let ``Loading assembly metadata works``() =
    // When debugging CurrentDirectory is C:\Windows\system32
    // When running multiple CurrentDirectory is the project directory
    // When running in CI CurrentDirectory is the bin/Release folder?
    let assemblyLocation = Assembly.GetExecutingAssembly().Location
    let workingDir =
        let curDir = Path.GetFullPath(".")
        if curDir.ToLowerInvariant().Contains "system32" then
            Path.GetDirectoryName (assemblyLocation)
        else curDir
    System.Environment.CurrentDirectory <- workingDir

    let fileName =
        if File.Exists(Path.Combine(workingDir, "Paket.Tests.fsproj")) then
            Path.Combine(workingDir, "Paket.Tests.fsproj")
            |> normalizePath
        else
            Path.Combine(workingDir, "..", "..", "Paket.Tests.fsproj")
            |> normalizePath

    if File.Exists fileName |> not then
        failwithf "%s does not exist." fileName

    let projFile = 
        fileName
        |> ProjectFile.LoadFromFile

    let config = 
        if workingDir.Contains "Debug" then "Debug"
        elif assemblyLocation.Contains "Debug" then "Debug"
        else "Release"
    
    let assemblyReader,id,versionFromAssembly,fileName = PackageMetaData.readAssemblyFromProjFile config "" projFile
    id |> shouldEqual "Paket.Tests"
    
    let attribs = PackageMetaData.loadAssemblyAttributes assemblyReader
    PackageMetaData.getVersion versionFromAssembly attribs |> shouldEqual <| Some(SemVer.Parse "1.0.0.0")
    let authors = PackageMetaData.getAuthors attribs
    authors.Value |> shouldEqual ["Two"; "Authors" ]
    PackageMetaData.getDescription attribs |> shouldEqual <| Some("A description")