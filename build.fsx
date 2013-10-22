// --------------------------------------------------------------------------------------
// FAKE build script 
// --------------------------------------------------------------------------------------

#r @"packages/FAKE/tools/FakeLib.dll"

open Fake 
open Fake.AssemblyInfoFile
open Fake.Git
open Fake.ReleaseNotesHelper
open System
open System.IO
open System.Text.RegularExpressions

Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

let files includes = 
  { BaseDirectories = [__SOURCE_DIRECTORY__]
    Includes = includes
    Excludes = [] } |> Scan

// Information about the project to be used 
//  - by NuGet
//  - in AssemblyInfo files
//  - in FAKE tasks
let solution  = "FSharp.ProjectScaffold"
let project   = "FSharp.ProjectTemplate"
let authors   = [ "tpetricek"; "pblasucci"; ]
let summary   = "A prototypical F# library (file system layout and tooling), recommended by the F# Foundation."
let description = """
  The F# DataFrame library (FSharp.DataFrame.dll) implements an efficient and robust 
  data frame and series structures for manipulating with structured data. It supports
  handling of missing values, aggregations, grouping, joining, statistical functions and
  more. For frames and series with ordered indices (such as time series), automatic
  alignment is also available. """

let tags = "F# fsharp project template scaffold sample example"

// Read additional information from the release notes document
// Expected format: "0.9.0-beta - Foo bar." or just "0.9.0 - Foo bar."
// (We need to extract just the number for AssemblyInfo & all version for NuGet
//let versionAsm, versionNuGet, releaseNotes = 
//    let lastItem = File.ReadLines "RELEASE_NOTES.md" |> Seq.last
//    let firstDash = lastItem.IndexOf(" - ")
//    let notes = lastItem.Substring(firstDash + 2).Trim()
//    let version = lastItem.Substring(0, firstDash).Trim([|'*'|]).Trim()
//    // Get just numeric version, if it contains dash
//    let versionDash = version.IndexOf('-')
//    if versionDash = -1 then version, version, notes
//    else version.Substring(0, versionDash), version, notes
let { AssemblyVersion = versionAsm
      NugetVersion    = versionNuGet
      Notes           = releaseNotes} = 
      "RELEASE_NOTES.md" |> File.ReadAllLines |> parseReleaseNotes

// --------------------------------------------------------------------------------------
// Generate assembly info files with the right version & up-to-date information

Target "AssemblyInfo" (fun _ ->
  let fileName = "src/" + project + "/AssemblyInfo.fs"
  CreateFSharpAssemblyInfo fileName
      [ Attribute.Title project
        Attribute.Product project
        Attribute.Description summary
        Attribute.Version versionAsm
        Attribute.FileVersion versionAsm ] 
)

// --------------------------------------------------------------------------------------
// Clean build results & restore NuGet packages

Target "RestorePackages" (fun _ ->
    !! "./**/packages.config"
    |> Seq.iter (RestorePackage (fun p -> { p with ToolPath = "./.nuget/NuGet.exe" }))
)

Target "Clean" (fun _ ->
    CleanDirs ["bin"; "temp"]
)

Target "CleanDocs" (fun _ ->
    CleanDirs ["docs/output"]
)

// --------------------------------------------------------------------------------------
// Build library & test project

Target "Build" (fun _ ->
    (files [solution +       ".sln"
            solution + ".Tests.sln"])
    |> MSBuildRelease "" "Rebuild"
    |> ignore
)

// --------------------------------------------------------------------------------------
// Run the unit tests using test runner & kill test runner when complete

//Target "RunTests" (fun _ ->
//    let nunitVersion = GetPackageVersion "packages" "NUnit.Runners"
//    let nunitPath = sprintf "packages/NUnit.Runners.%s/Tools" nunitVersion
//
//    ActivateFinalTarget "CloseTestRunner"
//
//    (files ["tests/*/bin/Debug/FSharp.ProjectScaffold*Tests*.dll"])
//    |> NUnit (fun p ->
//        { p with
//            ToolPath = nunitPath
//            DisableShadowCopy = true
//            TimeOut = TimeSpan.FromMinutes 20.
//            OutputFile = "TestResults.xml" })
//)
//
//FinalTarget "CloseTestRunner" (fun _ ->  
//    ProcessHelper.killProcess "nunit-agent.exe"
//)

// --------------------------------------------------------------------------------------
// Build a NuGet package

Target "NuGet" (fun _ ->
    // Format the description to fit on a single line (remove \r\n and double-spaces)
    let description = description.Replace("\r", "")
                                 .Replace("\n", "")
                                 .Replace("  ", " ")
    let nugetPath = ".nuget/nuget.exe"
    NuGet (fun p -> 
        { p with   
            Authors = authors
            Project = project
            Summary = summary
            Description = description
            Version = versionNuGet
            ReleaseNotes = String.Join(Environment.NewLine,releaseNotes)
            Tags = tags
            OutputPath = "bin"
            ToolPath = nugetPath
            AccessKey = getBuildParamOrDefault "nugetkey" ""
            Publish = hasBuildParam "nugetkey"
            Dependencies = [] })
        ("nuget/" + project + ".nuspec")
)

// --------------------------------------------------------------------------------------
// Generate the documentation

Target "JustGenerateDocs" (fun _ ->
    executeFSI "docs/tools" "generate.fsx" ["define","RELEASE"] |> ignore
)

Target "GenerateDocs" DoNothing

"CleanDocs" 
  ==> "JustGenerateDocs" 
  ==> "GenerateDocs"

// --------------------------------------------------------------------------------------
// Release Scripts

let gitHome = "https://github.com/pblasucci"

Target "ReleaseDocs" (fun _ ->
    let ghPages      = "gh-pages"
    let ghPagesLocal = "temp/gh-pages"
    Repository.clone "" (gitHome + "/fsharp-project-scaffold.git") ghPages
    Branches.checkoutBranch ghPagesLocal ghPages
    CopyRecursive "docs/output" ghPagesLocal true |> printfn "%A"
    CommandHelper.runSimpleGitCommand ghPagesLocal "add ." |> printfn "%s"
    let cmd = sprintf """commit -a -m "Update generated documentation for version %s""" versionNuGet
    CommandHelper.runSimpleGitCommand ghPagesLocal cmd |> printfn "%s"
    Branches.push ghPagesLocal
)

Target "ReleaseBinaries" (fun _ ->
    Repository.clone "" (gitHome + "/fsharp-project-scaffold.git") "release"
    Branches.checkoutBranch "release" "release"
    CopyRecursive "bin" "release/bin" true |> printfn "%A"
    MoveFile "./release/" "./release/bin/FSharp.ProjectScaffold.fsx"
    let cmd = sprintf """commit -a -m "Update binaries for version %s""" versionNuGet
    CommandHelper.runSimpleGitCommand "release" cmd |> printfn "%s"
    Branches.push "release"
)

Target "Release" DoNothing

// --------------------------------------------------------------------------------------
// Run all targets by default. Invoke 'build <Target>' to override

Target "All" DoNothing

"Clean"
  ==> "RestorePackages"
  ==> "AssemblyInfo"
  ==> "Build"
  ==> "GenerateDocs"
//  ==> "RunTests"
  ==> "All"

"All" 
//  ==> "ReleaseDocs"
//  ==> "ReleaseBinaries"
  ==> "NuGet"
  ==> "Release"

RunTargetOrDefault "All"
