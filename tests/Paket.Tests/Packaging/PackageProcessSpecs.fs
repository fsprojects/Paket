module Paket.PackagingProcess.Test

open System.IO
open System.Reflection
open Paket
open FsUnit
open NUnit.Framework

let rec tryFindFileInHeirarchy (startDir: DirectoryInfo) (targetFileName: string): string option =
    if isNull startDir then None
    else
        let testPath = Path.Combine(startDir.FullName, targetFileName)
        if File.Exists testPath then Some (normalizePath testPath)
        else tryFindFileInHeirarchy startDir.Parent targetFileName

[<Test>]
let ``Loading assembly metadata works``() =
    // When debugging CurrentDirectory is C:\Windows\system32
    // When running multiple CurrentDirectory is the project directory
    // When running in CI CurrentDirectory is the bin/Release folder?
    let assemblyLocation = Assembly.GetExecutingAssembly().Location
    let workingDir =
        let curDir = Path.GetFullPath(".")
        if curDir.ToLowerInvariant().Contains "system32" then
            Path.GetDirectoryName assemblyLocation
        else curDir

    use _cd = TestHelpers.changeWorkingDir workingDir
    let workingDir = DirectoryInfo workingDir
    let projectFilePath =
        tryFindFileInHeirarchy workingDir "Paket.Tests.fsproj"
        |> Option.defaultWith (fun _ -> failwithf "%s does not exist in the directory heirarchy of %s." "Paket.Tests.fsproj" workingDir.FullName)

    let projFile =
        projectFilePath
        |> ProjectFile.LoadFromFile

    let config =
        if workingDir.FullName.Contains "Debug" then "Debug"
        elif assemblyLocation.Contains "Debug" then "Debug"
        else "Release"

    let paketReleaseNotesVersion =
        tryFindFileInHeirarchy workingDir "RELEASE_NOTES.md"
        |> Option.map (File.ReadLines >> Seq.head)
        |> Option.map (fun line -> line.Split(' ').[1]) // format is ### <VERSION> - <DATE>, so taking second element of array is the version
        |> Option.defaultWith (fun _ -> failwithf "unable to parse current version from RELEASE_NOTES.md in the directory heirarchy of %s" workingDir.FullName)

    let assemblyReader, id, versionFromAssembly, _fileName = PackageMetaData.readAssemblyFromProjFile config "" projFile
    id |> shouldEqual "Paket.Tests"

    let attribs = PackageMetaData.loadAssemblyAttributes assemblyReader
    PackageMetaData.getVersion versionFromAssembly attribs |> shouldEqual <| Some(SemVer.Parse paketReleaseNotesVersion)
    let authors = PackageMetaData.getAuthors attribs
    authors.Value |> shouldEqual ["Two"; "Authors" ]
    PackageMetaData.getDescription attribs |> shouldEqual <| Some("A description")