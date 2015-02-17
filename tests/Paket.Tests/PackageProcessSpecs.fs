module Paket.PackagingProcess.Test

open System.IO
open System.Reflection
open Paket
open FsUnit
open NUnit.Framework

let ass = Assembly.GetExecutingAssembly()

[<Test>]
let ``Loading description from assembly works`` () =
    let sut =
        PackageProcess.getDescription (ass.GetCustomAttributes(true)) PackageProcess.emptyMetadata
    sut.Description.Value |> shouldEqual "A description"

[<Test>]
let ``Loading version from assembly works`` () =
    let sut =
        PackageProcess.getVersion ass (ass.GetCustomAttributes(true)) PackageProcess.emptyMetadata
    sut.Version.Value |> shouldEqual (SemVer.Parse "1.0.0.0")

[<Test>]
let ``Loading authors from assembly works`` () =
    let sut =
        PackageProcess.getAuthors (ass.GetCustomAttributes(true)) PackageProcess.emptyMetadata
    sut.Authors.Value |> shouldEqual ["Two";"Authors"]

[<Test>]
let ``Loading id from assembly works`` () =
    let sut =
        PackageProcess.getId ass PackageProcess.emptyMetadata
    sut.Id.Value |> shouldEqual "Paket.Tests"

[<Test>]
let ``Loading assembly metadata works`` () =
    let workingDir = Path.GetFullPath(".")
    let projFile =
        Path.Combine(workingDir, "..", "..", "Paket.Tests.fsproj")
        |> normalizePath
        |> ProjectFile.Load
    let config = if workingDir.Contains "Debug" then "Debug" else "Release"
    let sut =
         PackageProcess.loadAssemblyMetadata config projFile.Value
    sut |> shouldEqual { Id = Some "Paket.Tests" 
                         Version = SemVer.Parse "1.0.0.0" |> Some
                         Authors = Some ["Two";"Authors"]
                         Description = Some "A description" }