// --------------------------------------------------------------------------------------
// FAKE build script
// --------------------------------------------------------------------------------------

#r @"packages/FAKE/tools/FakeLib.dll"

open Fake
open Fake.Git
open Fake.AssemblyInfoFile
open Fake.ReleaseNotesHelper
open System
open System.IO

// --------------------------------------------------------------------------------------
// START TODO: Provide project-specific details below
// --------------------------------------------------------------------------------------

// Information about the project are used
//  - for version and project name in generated AssemblyInfo file
//  - by the generated NuGet package
//  - to run tests and to publish documentation on GitHub gh-pages
//  - for documentation, you also need to edit info in "docs/tools/generate.fsx"

// The name of the project
// (used by attributes in AssemblyInfo, name of a NuGet package and directory in 'src')
let project = "Paket"

// Short summary of the project
// (used as description in AssemblyInfo and as a short summary for NuGet package)
let summary = "A package dependency manager for .NET with support for NuGet packages and GitHub repositories."

// Longer description of the project
// (used as a description for NuGet package; line breaks are automatically cleaned up)
let description = "A package dependency manager for .NET with support for NuGet packages and GitHub repositories."

// List of author names (for NuGet package)
let authors = [ "Paket team" ]

// Tags for your project (for NuGet package)
let tags = "nuget, bundler, F#"

// File system information
let solutionFile  = "Paket.sln"

// Pattern specifying assemblies to be tested using NUnit
let testAssemblies = "tests/**/bin/Release/*Tests*.dll"

// Git configuration (used for publishing documentation in gh-pages branch)
// The profile where the project is posted
let gitOwner = "fsprojects"
let gitHome = "https://github.com/" + gitOwner

// The name of the project on GitHub
let gitName = "Paket"

// The url for the raw files hosted
let gitRaw = environVarOrDefault "gitRaw" "https://raw.github.com/fsprojects"

// --------------------------------------------------------------------------------------
// END TODO: The rest of the file includes standard build steps
// --------------------------------------------------------------------------------------

let buildDir = "bin"
let buildMergedDir = buildDir @@ "merged"


// Read additional information from the release notes document
let release = LoadReleaseNotes "RELEASE_NOTES.md"

let genFSAssemblyInfo (projectPath) =
    let projectName = System.IO.Path.GetFileNameWithoutExtension(projectPath)
    let basePath = "src/" + projectName
    let fileName = basePath + "/AssemblyInfo.fs"
    CreateFSharpAssemblyInfo fileName
      [ Attribute.Title (projectName)
        Attribute.Product project
        Attribute.Description summary
        Attribute.Version release.AssemblyVersion
        Attribute.FileVersion release.AssemblyVersion ]

let genCSAssemblyInfo (projectPath) =
    let projectName = System.IO.Path.GetFileNameWithoutExtension(projectPath)
    let basePath = "src/" + projectName + "/Properties"
    let fileName = basePath + "/AssemblyInfo.cs"
    CreateCSharpAssemblyInfo fileName
      [ Attribute.Title (projectName)
        Attribute.Product project
        Attribute.Description summary
        Attribute.Version release.AssemblyVersion
        Attribute.FileVersion release.AssemblyVersion ]

// Generate assembly info files with the right version & up-to-date information
Target "AssemblyInfo" (fun _ ->
  let fsProjs =  !! "src/**/*.fsproj"
  let csProjs = !! "src/**/*.csproj"
  fsProjs |> Seq.iter genFSAssemblyInfo
  csProjs |> Seq.iter genCSAssemblyInfo
)

// --------------------------------------------------------------------------------------
// Clean build results

Target "Clean" (fun _ ->
    CleanDirs [buildDir; "temp"]
)

Target "CleanDocs" (fun _ ->
    CleanDirs ["docs/output"]
)

// --------------------------------------------------------------------------------------
// Build library & test project

Target "Build" (fun _ ->
    !! solutionFile
    |> MSBuildRelease "" "Rebuild"
    |> ignore
)

// --------------------------------------------------------------------------------------
// Run the unit tests using test runner

Target "RunTests" (fun _ ->
    !! testAssemblies
    |> NUnit (fun p ->
        { p with
            DisableShadowCopy = true
            TimeOut = TimeSpan.FromMinutes 20.
            OutputFile = "TestResults.xml" })
)

// --------------------------------------------------------------------------------------
// Build a NuGet package

Target "MergePaketTool" (fun _ ->
    CreateDir buildMergedDir

    let toPack =
        ["paket.exe"; "Paket.Core.dll"; "FSharp.Core.dll"; "Ionic.Zip.dll"; "Newtonsoft.Json.dll"; "UnionArgParser.dll"]
        |> List.map (fun l -> buildDir @@ l)
        |> separated " "

    let result =
        ExecProcess (fun info ->
            info.FileName <- currentDirectory @@ "tools" @@ "ILRepack" @@ "ILRepack.exe"
            info.Arguments <- sprintf "/internalize /verbose /lib:%s /ver:%s /out:%s %s" buildDir release.AssemblyVersion (buildMergedDir @@ "paket.exe") toPack
            ) (TimeSpan.FromMinutes 5.)

    if result <> 0 then failwithf "Error during ILRepack execution."
)

Target "SignAssemblies" (fun _ ->
    let pfx = "code-sign.pfx"
    if not <| fileExists pfx then
        traceImportant (sprintf "%s not found, skipped signing assemblies" pfx)
    else

    let filesToSign = 
        !! "bin/**/*.exe"
        ++ "bin/**/Paket.Core.dll"

    filesToSign
        |> Seq.iter (fun executable ->
            let signtool = currentDirectory @@ "tools" @@ "SignTool" @@ "signtool.exe"
            let args = sprintf "sign /f %s /t http://timestamp.comodoca.com/authenticode %s" pfx executable
            let result =
                ExecProcess (fun info ->
                    info.FileName <- signtool
                    info.Arguments <- args) System.TimeSpan.MaxValue
            if result <> 0 then failwithf "Error during signing %s with %s" executable pfx)
)

Target "NuGet" (fun _ ->

    NuGet (fun p -> 
        { p with Authors = authors
                 Project = "Paket.Core"
                 Summary = summary
                 Description = description
                 Version = release.NugetVersion
                 ReleaseNotes = toLines release.Notes
                 Tags = tags
                 OutputPath = "bin"
                 Dependencies = 
                    ["Newtonsoft.Json", GetPackageVersion "packages" "Newtonsoft.Json" 
                     "DotNetZip", GetPackageVersion "packages" "DotNetZip" 
                     "FSharp.Core", GetPackageVersion "packages" "FSharp.Core" ]
                 AccessKey = getBuildParamOrDefault "nugetkey" ""
                 Publish = hasBuildParam "nugetkey" }) 
       "nuget/Paket.Core.nuspec"

    NuGet (fun p -> 
        { p with Authors = authors
                 Project = "Paket"
                 Summary = summary
                 Description = description
                 Version = release.NugetVersion
                 ReleaseNotes = toLines release.Notes
                 Tags = tags
                 OutputPath = "bin"
                 AccessKey = getBuildParamOrDefault "nugetkey" ""
                 Publish = hasBuildParam "nugetkey"
                 Dependencies = [] }) 
       "nuget/Paket.nuspec"
)

// --------------------------------------------------------------------------------------
// Generate the documentation

Target "GenerateReferenceDocs" (fun _ ->
    if not <| executeFSIWithArgs "docs/tools" "generate.fsx" ["--define:RELEASE"; "--define:REFERENCE"] [] then
      failwith "generating reference documentation failed"
)

let generateHelp fail =
    if executeFSIWithArgs "docs/tools" "generate.fsx" ["--define:RELEASE"; "--define:HELP"] [] then
        traceImportant "Help generated"
    else
        if fail then
            failwith "generating help documentation failed"
        else
            traceImportant "generating help documentation failed"
    

Target "GenerateHelp" (fun _ ->
    DeleteFile "docs/content/release-notes.md"    
    CopyFile "docs/content/" "RELEASE_NOTES.md"
    Rename "docs/content/release-notes.md" "docs/content/RELEASE_NOTES.md"

    DeleteFile "docs/content/license.md"
    CopyFile "docs/content/" "LICENSE.txt"
    Rename "docs/content/license.md" "docs/content/LICENSE.txt"

    CopyFile buildDir "packages/FSharp.Core/lib/net40/FSharp.Core.sigdata"
    CopyFile buildDir "packages/FSharp.Core/lib/net40/FSharp.Core.optdata"

    generateHelp true
)


Target "KeepRunning" (fun _ ->    
    use watcher = new FileSystemWatcher(DirectoryInfo("docs/content").FullName,"*.*")
    watcher.EnableRaisingEvents <- true
    watcher.Changed.Add(fun e -> generateHelp false)
    watcher.Created.Add(fun e -> generateHelp false)
    watcher.Renamed.Add(fun e -> generateHelp false)
    watcher.Deleted.Add(fun e -> generateHelp false)

    traceImportant "Waiting for help edits. Press any key to stop."

    System.Console.ReadKey() |> ignore

    watcher.EnableRaisingEvents <- false
    watcher.Dispose()
)

Target "GenerateDocs" DoNothing

// --------------------------------------------------------------------------------------
// Release Scripts

Target "ReleaseDocs" (fun _ ->
    let tempDocsDir = "temp/gh-pages"
    CleanDir tempDocsDir
    Repository.cloneSingleBranch "" (gitHome + "/" + gitName + ".git") "gh-pages" tempDocsDir

    fullclean tempDocsDir
    CopyRecursive "docs/output" tempDocsDir true |> tracefn "%A"
    StageAll tempDocsDir
    Git.Commit.Commit tempDocsDir (sprintf "Update generated documentation for version %s" release.NugetVersion)
    Branches.push tempDocsDir
)

#load "paket-files/fsharp/FAKE/modules/Octokit/Octokit.fsx"
open Octokit

Target "Release" (fun _ ->
    StageAll ""
    Git.Commit.Commit "" (sprintf "Bump version to %s" release.NugetVersion)
    Branches.push ""

    Branches.tag "" release.NugetVersion
    Branches.pushTag "" "origin" release.NugetVersion
    
    // release on github
    createClient (getBuildParamOrDefault "github-user" "") (getBuildParamOrDefault "github-pw" "")
    |> createDraft gitOwner gitName release.NugetVersion (release.SemVer.PreRelease <> None) release.Notes 
    |> uploadFile "./bin/merged/paket.exe"
    |> uploadFile "./bin/paket.bootstrapper.exe"
    |> uploadFile ".paket/paket.targets"
    |> releaseDraft
    |> Async.RunSynchronously
)

Target "BuildPackage" DoNothing

// --------------------------------------------------------------------------------------
// Run all targets by default. Invoke 'build <Target>' to override

Target "All" DoNothing

"Clean"
  ==> "AssemblyInfo"
  ==> "Build"
  ==> "RunTests"
  =?> ("GenerateReferenceDocs",isLocalBuild && not isMono)
  =?> ("GenerateDocs",isLocalBuild && not isMono)
  ==> "All"
  =?> ("ReleaseDocs",isLocalBuild && not isMono)

"All"
  ==> "MergePaketTool"
  ==> "SignAssemblies"
  ==> "NuGet"
  ==> "BuildPackage"

"CleanDocs"
  ==> "GenerateHelp"
  ==> "GenerateReferenceDocs"
  ==> "GenerateDocs"

"GenerateHelp"
  ==> "KeepRunning"
    
"ReleaseDocs"
  ==> "Release"

"BuildPackage"
  ==> "Release"

RunTargetOrDefault "All"
