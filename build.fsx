System.IO.Directory.SetCurrentDirectory __SOURCE_DIRECTORY__


#r @"packages/build/FAKE/tools/FakeLib.dll"
#r "System.IO.Compression.FileSystem"

open Fake
open Fake.Git
open Fake.AssemblyInfoFile
open Fake.ReleaseNotesHelper
open Fake.UserInputHelper
open System
open System.IO
open Fake.Testing.NUnit3
open System.Security.Cryptography

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
let summary = "A dependency manager for .NET with support for NuGet packages and git repositories."

// Longer description of the project
// (used as a description for NuGet package; line breaks are automatically cleaned up)
let description = "A dependency manager for .NET with support for NuGet packages and git repositories."

// List of author names (for NuGet package)
let authors = [ "Paket team" ]

// Tags for your project (for NuGet package)
let tags = "nuget, bundler, F#"

// File system information
let solutionFile  = "Paket.sln"

// Pattern specifying assemblies to be tested using NUnit
let testAssemblies = "tests/**/bin/Release/*Tests*.dll"
let integrationTestAssemblies = "integrationtests/Paket.IntegrationTests/bin/Release/*Tests*.dll"

// Git configuration (used for publishing documentation in gh-pages branch)
// The profile where the project is posted
let gitOwner = "fsprojects"
let gitHome = "https://github.com/" + gitOwner

// The name of the project on GitHub
let gitName = "Paket"

// The url for the raw files hosted
let gitRaw = environVarOrDefault "gitRaw" "https://raw.github.com/fsprojects"

let dotnetcliVersion = DotNetCli.GetDotNetSDKVersionFromGlobalJson()

let mutable dotnetExePath = "dotnet"    

// --------------------------------------------------------------------------------------
// END TODO: The rest of the file includes standard build steps
// --------------------------------------------------------------------------------------

let buildDir = "bin"
let buildDirNetCore = "bin_netcore"
let tempDir = "temp"
let buildMergedDir = buildDir @@ "merged"
let paketFile = buildMergedDir @@ "paket.exe"

Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

System.Net.ServicePointManager.SecurityProtocol <- unbox 192 ||| unbox 768 ||| unbox 3072 ||| unbox 48

// Read additional information from the release notes document
let releaseNotesData = 
    File.ReadAllLines "RELEASE_NOTES.md"
    |> parseAllReleaseNotes

let release = List.head releaseNotesData

let stable = 
    match releaseNotesData |> List.tryFind (fun r -> r.NugetVersion.Contains("-") |> not) with
    | Some stable -> stable
    | _ -> release


let testSuiteFilterFlakyTests = getEnvironmentVarAsBoolOrDefault "PAKET_TESTSUITE_FLAKYTESTS" false

let genFSAssemblyInfo (projectPath) =
    let projectName = System.IO.Path.GetFileNameWithoutExtension(projectPath)
    let folderName = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(projectPath))
    let basePath = "src" @@ folderName
    let fileName = basePath @@ "AssemblyInfo.fs"
    CreateFSharpAssemblyInfo fileName
      [ Attribute.Title (projectName)
        Attribute.Product project
        Attribute.Company (authors |> String.concat ", ")
        Attribute.Description summary
        Attribute.Version release.AssemblyVersion
        Attribute.FileVersion release.AssemblyVersion
        Attribute.InformationalVersion release.NugetVersion ]

let genCSAssemblyInfo (projectPath) =
    let projectName = System.IO.Path.GetFileNameWithoutExtension(projectPath)
    let folderName = System.IO.Path.GetDirectoryName(projectPath)
    let basePath = folderName @@ "Properties"
    let fileName = basePath @@ "AssemblyInfo.cs"
    CreateCSharpAssemblyInfo fileName
      [ Attribute.Title (projectName)
        Attribute.Product project
        Attribute.Description summary
        Attribute.Version release.AssemblyVersion
        Attribute.FileVersion release.AssemblyVersion
        Attribute.InformationalVersion release.NugetVersion ]

// Generate assembly info files with the right version & up-to-date information
Target "AssemblyInfo" (fun _ ->
    let fsProjs =  !! "src/**/*.fsproj" |> Seq.filter (fun s -> not <| s.Contains("preview"))
    let csProjs = !! "src/**/*.csproj" |> Seq.filter (fun s -> not <| s.Contains("preview"))
    fsProjs |> Seq.iter genFSAssemblyInfo
    csProjs |> Seq.iter genCSAssemblyInfo
)

Target "InstallDotNetCore" (fun _ ->
    dotnetExePath <- DotNetCli.InstallDotNetSDK dotnetcliVersion
    Environment.SetEnvironmentVariable("DOTNET_EXE_PATH", dotnetExePath)
)

// --------------------------------------------------------------------------------------
// Clean build results

Target "Clean" (fun _ ->
    !! "src/**/bin"
    ++ "tests/**/bin"
    ++ buildDir 
    ++ buildDirNetCore
    ++ tempDir
    |> CleanDirs

    !! "**/obj/**/*.nuspec"
    |> DeleteFiles
)

Target "CleanDocs" (fun _ ->
    CleanDirs ["docs/output"]
)

// --------------------------------------------------------------------------------------
// Build library & test project

Target "Build" (fun _ ->
    if isMono then
        !! solutionFile
        |> MSBuildReleaseExt "" [
                "VisualStudioVersion", "14.0"
                "ToolsVersion"       , "14.0"
        ] "Rebuild"
        |> ignore
    else
        !! solutionFile
        |> MSBuildReleaseExt "" [
                "VisualStudioVersion", "14.0"
                "ToolsVersion"       , "14.0"
                "SourceLinkCreate"   , "true"
        ] "Rebuild"
        |> ignore
)

let assertExitCodeZero x = 
    if x = 0 then () else 
    failwithf "Command failed with exit code %i" x

let runCmdIn workDir exe = 
    Printf.ksprintf (fun args -> 
        tracefn "%s %s" exe args
        Shell.Exec(exe, args, workDir) |> assertExitCodeZero)

/// Execute a dotnet cli command
let dotnet workDir = runCmdIn workDir "dotnet"

Target "DotnetRestoreTools" (fun _ ->
    DotNetCli.Restore (fun c ->
        { c with
            Project = currentDirectory </> "tools" </> "tools.fsproj"
            ToolPath = dotnetExePath
        })
)

Target "DotnetRestore" (fun _ ->

    //WORKAROUND dotnet restore with paket doesnt restore the PackageReference of SourceLink
    // ref https://github.com/fsprojects/Paket/issues/2930
    Paket.Restore (fun p ->
        { p with
            Group = "NetCoreTools" })

    DotNetCli.Restore (fun c ->
        { c with
            Project = "Paket.preview3.sln"
            ToolPath = dotnetExePath
        })
)

Target "DotnetBuild" (fun _ ->
    DotNetCli.Build (fun c ->
        { c with
            Project = "Paket.preview3.sln"
            ToolPath = dotnetExePath
            AdditionalArgs = [ "/p:SourceLinkCreate=true" ]
        })
)
"Clean" ==> "DotnetRestore" ==> "DotnetBuild" 

Target "DotnetPublish" (fun _ ->
    DotNetCli.Publish (fun c ->
        { c with
            Project = "src/Paket.preview3"
            ToolPath = dotnetExePath
            Output = FullName (currentDirectory </> buildDirNetCore)
        })
)
"Clean" ==> "DotnetBuild" ?=> "DotnetPublish" 

Target "DotnetPackage" (fun _ ->
    let outPath = FullName (currentDirectory </> tempDir </> "dotnetcore")
    CleanDir outPath
    DotNetCli.Pack (fun c ->
        { c with
            Project = "src/Paket.Core.preview3/Paket.Core.fsproj"
            ToolPath = dotnetExePath
            AdditionalArgs = [(sprintf "-o \"%s\"" outPath); (sprintf "/p:Version=%s" release.NugetVersion)]
        })
    DotNetCli.Pack (fun c ->
        { c with
            Project = "src/Paket.preview3/Paket.fsproj"
            ToolPath = dotnetExePath
            AdditionalArgs = [(sprintf "-o \"%s\"" outPath); (sprintf "/p:Version=%s" release.NugetVersion)]
        })
    DotNetCli.Pack (fun c ->
        { c with
            Project = "src/Paket.Bootstrapper.preview3/Paket.Bootstrapper.csproj"
            ToolPath = dotnetExePath
            AdditionalArgs = [(sprintf "-o \"%s\"" outPath); (sprintf "/p:Version=%s" release.NugetVersion)]
        })
)

Target "DotnetTest" (fun _ ->
    CreateDir "tests_result/netcore/Paket.Tests"

    DotNetCli.Test (fun c ->
        { c with
            Project = "tests/Paket.Tests.preview3/Paket.Tests.fsproj"
            AdditionalArgs =
              [ "--filter"; (if testSuiteFilterFlakyTests then "TestCategory=Flaky" else "TestCategory!=Flaky")
                sprintf "--logger:trx;LogFileName=%s" ("tests_result/netcore/Paket.Tests/TestResult.trx" |> Path.GetFullPath) 
                "-v"; "n"]
            ToolPath = dotnetExePath
        })
)

Target "RunIntegrationTestsNetCore" (fun _ ->
    CreateDir "tests_result/netcore/Paket.IntegrationTests"

    // improves the speed of the test-suite by disabling the runtime resolution.
    System.Environment.SetEnvironmentVariable("PAKET_DISABLE_RUNTIME_RESOLUTION", "true")
    DotNetCli.Test (fun c ->
        { c with
            Project = "integrationtests/Paket.IntegrationTests.preview3/Paket.IntegrationTests.fsproj"
            ToolPath = dotnetExePath
            AdditionalArgs =
              [ "--filter"; (if testSuiteFilterFlakyTests then "TestCategory=Flaky" else "TestCategory!=Flaky")
                sprintf "--logger:trx;LogFileName=%s" ("tests_result/netcore/Paket.IntegrationTests/TestResult.trx" |> Path.GetFullPath) ]
            TimeOut = TimeSpan.FromMinutes 60.
        })
)
"Clean" ==> "DotnetPublish" ==> "RunIntegrationTestsNetCore" 

// --------------------------------------------------------------------------------------
// Run the unit tests using test runner

Target "RunTests" (fun _ ->
    CreateDir "tests_result/net/Paket.Tests"

    !! testAssemblies
    |> NUnit3 (fun p ->
        { p with
            ShadowCopy = false
            WorkingDir = "tests_result/net/Paket.Tests" |> Path.GetFullPath
            Agents = Some 1 //workaround for https://github.com/nunit/nunit-console/issues/219 
            TimeOut = TimeSpan.FromMinutes 20. })
)

Target "QuickTest" (fun _ ->

    [   "src/Paket.Core/Paket.Core.fsproj"
        "tests/Paket.Tests/Paket.Tests.fsproj"
    ]   |> MSBuildRelease "" "Rebuild"
        |> ignore

    !! testAssemblies
    |> NUnit3 (fun p ->
        { p with
            ShadowCopy = false
            WorkingDir = "tests/Paket.Tests" |> Path.GetFullPath
            TimeOut = TimeSpan.FromMinutes 20. })
)
"Clean" ==> "QuickTest"

Target "QuickIntegrationTests" (fun _ ->
    [   "src/Paket.Core/Paket.Core.fsproj"
        "src/Paket/Paket.fsproj"
        "integrationtests/Paket.IntegrationTests/Paket.IntegrationTests.fsproj"
    ]   |> MSBuildDebug "" "Rebuild"
        |> ignore
    
    
    !! integrationTestAssemblies    
    |> NUnit3 (fun p ->
        { p with
            ShadowCopy = false
            Where = "cat==scriptgen"
            WorkingDir = "integrationtests/Paket.IntegrationTests" |> Path.GetFullPath
            TimeOut = TimeSpan.FromMinutes 40. })
)
"Clean" ==> "QuickIntegrationTests" 


// --------------------------------------------------------------------------------------
// Build a NuGet package

let mergeLibs = ["paket.exe"; "Paket.Core.dll"; "FSharp.Core.dll"; "Newtonsoft.Json.dll"; "Argu.dll"; "Chessie.dll"; "Mono.Cecil.dll"]

Target "MergePaketTool" (fun _ ->
    CreateDir buildMergedDir
    
    let toPack =
        mergeLibs
        |> List.map (fun l -> buildDir @@ l)
        |> separated " "

    let result =
        ExecProcess (fun info ->
            info.FileName <- currentDirectory </> "packages" </> "build" </> "ILRepack" </> "tools" </> "ILRepack.exe"
            info.Arguments <- sprintf "/lib:%s /ver:%s /out:%s %s" buildDir release.AssemblyVersion paketFile toPack
            ) (TimeSpan.FromMinutes 5.)

    if result <> 0 then failwithf "Error during ILRepack execution."
)

Target "RunIntegrationTests" (fun _ ->
    CreateDir "tests_result/net/Paket.IntegrationTests" 

    // improves the speed of the test-suite by disabling the runtime resolution.
    System.Environment.SetEnvironmentVariable("PAKET_DISABLE_RUNTIME_RESOLUTION", "true")
    !! integrationTestAssemblies    
    |> NUnit3 (fun p ->
        { p with
            ShadowCopy = false
            WorkingDir = "tests_result/net/Paket.IntegrationTests" |> Path.GetFullPath
            Where = if testSuiteFilterFlakyTests then "cat==Flaky" else "cat!=Flaky"
            TimeOut = TimeSpan.FromMinutes 40. })
)
"Clean" ==> "Build" ==> "RunIntegrationTests" 


let pfx = "code-sign.pfx"
let mutable isUnsignedAllowed = true
Target "EnsurePackageSigned" (fun _ -> isUnsignedAllowed <- false)

Target "SignAssemblies" (fun _ ->
    if not <| fileExists pfx then
        if isUnsignedAllowed then ()
        else failwithf "%s not found, can't sign assemblies" pfx
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

Target "CalculateDownloadHash" (fun _ ->
    use stream = File.OpenRead(paketFile)
    use sha = new SHA256Managed()
    let checksum = sha.ComputeHash(stream)
    let hash = BitConverter.ToString(checksum).Replace("-", String.Empty)
    File.WriteAllText(buildMergedDir @@ "paket-sha256.txt", sprintf "%s paket.exe" hash)
)

Target "NuGet" (fun _ ->
    let testTemplateFiles =
        !! "integrationtests/**/paket.template"
        |> Seq.map (fun f -> f, f + "-disabled")
        |> Seq.toList
        

    try
        testTemplateFiles
        |> Seq.iter (fun (f, d) -> File.Move(f, d))

        let files = !! "src/**/*.preview*" |> Seq.toList
        for file in files do
            File.Move(file,file + ".temp")

        Paket.Pack (fun p -> 
            { p with 
                ToolPath = "bin/merged/paket.exe" 
                Version = release.NugetVersion
                ReleaseNotes = toLines release.Notes })

        for file in files do
            File.Move(file + ".temp",file)
    finally
        testTemplateFiles
        |> Seq.iter (fun (f, d) ->
            if File.Exists(d) then File.Move(d, f))
)

"DotnetPublish" ==> "NuGet" 

Target "MergeDotnetCoreIntoNuget" (fun _ ->

    let runTool = runCmdIn "tools" dotnetExePath

    let mergeNupkg packageName args =
        let nupkg = tempDir </> sprintf "%s.%s.nupkg" packageName (release.NugetVersion) |> Path.GetFullPath
        let netcoreNupkg = tempDir </> "dotnetcore" </> sprintf "%s.%s.nupkg" packageName (release.NugetVersion) |> Path.GetFullPath

        runTool """mergenupkg --source "%s" --other "%s" %s""" nupkg netcoreNupkg args

    mergeNupkg "Paket.Core" "--framework netstandard2.0"
    mergeNupkg "Paket" "--tools"
    mergeNupkg "paket.bootstrapper" "--tools"
)

Target "PublishNuGet" (fun _ ->
    if hasBuildParam "PublishBootstrapper" |> not then
        !! (tempDir </> "*bootstrapper*")
        |> Seq.iter File.Delete

    !! (tempDir </> "dotnetcore" </> "*.nupkg")
    |> Seq.iter File.Delete

    Paket.Push (fun p -> 
        { p with 
            ToolPath = "bin/merged/paket.exe"
            WorkingDir = tempDir }) 
)


// --------------------------------------------------------------------------------------
// Generate the documentation

let disableDocs = false // https://github.com/fsprojects/FSharp.Formatting/issues/461

let fakePath = __SOURCE_DIRECTORY__ @@ "packages" @@ "build" @@ "FAKE" @@ "tools" @@ "FAKE.exe"
let fakeStartInfo fsiargs script workingDirectory args environmentVars =
    (fun (info: System.Diagnostics.ProcessStartInfo) ->
        info.FileName <- fakePath
        info.Arguments <- sprintf "%s --fsiargs %s -d:FAKE \"%s\"" args fsiargs script
        info.WorkingDirectory <- workingDirectory
        let setVar k v =
            info.EnvironmentVariables.[k] <- v
        for (k, v) in environmentVars do
            setVar k v
        setVar "MSBuild" msBuildExe
        setVar "GIT" Git.CommandHelper.gitPath
        setVar "FSI" fsiPath)


/// Run the given startinfo by printing the output (live)
let executeWithOutput configStartInfo =
    let exitCode =
        ExecProcessWithLambdas
            configStartInfo
            TimeSpan.MaxValue false ignore ignore
    System.Threading.Thread.Sleep 1000
    exitCode

/// Run the given startinfo by redirecting the output (live)
let executeWithRedirect errorF messageF configStartInfo =
    let exitCode =
        ExecProcessWithLambdas
            configStartInfo
            TimeSpan.MaxValue true errorF messageF
    System.Threading.Thread.Sleep 1000
    exitCode

/// Helper to fail when the exitcode is <> 0
let executeHelper executer fail traceMsg failMessage configStartInfo =
    trace traceMsg
    let exit = executer configStartInfo
    if exit <> 0 then
        if fail then
            failwith failMessage
        else
            traceImportant failMessage
    else
        traceImportant "Succeeded"
    ()

let execute = executeHelper executeWithOutput

Target "GenerateReferenceDocs" (fun _ ->
    if disableDocs then () else
    let args = ["--define:RELEASE"; "--define:REFERENCE"]
    let argLine = System.String.Join(" ", args)
    execute
      true
      (sprintf "Building reference documentation, this could take some time, please wait...")
      "generating reference documentation failed"
      (fakeStartInfo argLine "generate.fsx" "docs/tools" "" [])
)




let generateHelp' commands fail debug =
    // remove FSharp.Compiler.Service.MSBuild.v12.dll
    // otherwise FCS thinks  it should use msbuild, which leads to insanity
    !! "packages/**/FSharp.Compiler.Service.MSBuild.*.dll"
    |> DeleteFiles

    let args =
        [ if not debug then yield "--define:RELEASE"
          if commands then yield "--define:COMMANDS"
          yield "--define:HELP"]
    let argLine = System.String.Join(" ", args)
    execute
      fail
      (sprintf "Building documentation (%A), this could take some time, please wait..." commands)
      "generating documentation failed"
      (fakeStartInfo argLine "generate.fsx" "docs/tools" "" [])

    CleanDir "docs/output/commands"

let generateHelp commands fail =
    generateHelp' commands fail false

Target "GenerateHelp" (fun _ ->
    if disableDocs then () else
    DeleteFile "docs/content/release-notes.md"
    CopyFile "docs/content/" "RELEASE_NOTES.md"
    Rename "docs/content/release-notes.md" "docs/content/RELEASE_NOTES.md"

    DeleteFile "docs/content/license.md"
    CopyFile "docs/content/" "LICENSE.txt"
    Rename "docs/content/license.md" "docs/content/LICENSE.txt"

    generateHelp true true
)

Target "GenerateHelpDebug" (fun _ ->
    if disableDocs then () else
    DeleteFile "docs/content/release-notes.md"
    CopyFile "docs/content/" "RELEASE_NOTES.md"
    Rename "docs/content/release-notes.md" "docs/content/RELEASE_NOTES.md"

    DeleteFile "docs/content/license.md"
    CopyFile "docs/content/" "LICENSE.txt"
    Rename "docs/content/license.md" "docs/content/LICENSE.txt"

    generateHelp' true true true
)

Target "KeepRunning" (fun _ ->    
    use watcher = !! "docs/content/**/*.*" |> WatchChanges (fun changes ->
         generateHelp false false
    )

    traceImportant "Waiting for help edits. Press any key to stop."

    System.Console.ReadKey() |> ignore

    watcher.Dispose()
)

Target "GenerateDocs" DoNothing

// --------------------------------------------------------------------------------------
// Release Scripts

Target "ReleaseDocs" (fun _ ->
    if disableDocs then () else   
    let tempDocsDir = "temp/gh-pages"
    CleanDir tempDocsDir
    Repository.cloneSingleBranch "" (gitHome + "/" + gitName + ".git") "gh-pages" tempDocsDir

    Git.CommandHelper.runSimpleGitCommand tempDocsDir "rm . -f -r" |> ignore
    CopyRecursive "docs/output" tempDocsDir true |> tracefn "%A"

    File.WriteAllText("temp/gh-pages/latest",sprintf "https://github.com/fsprojects/Paket/releases/download/%s/paket.exe" release.NugetVersion)
    File.WriteAllText("temp/gh-pages/stable",sprintf "https://github.com/fsprojects/Paket/releases/download/%s/paket.exe" stable.NugetVersion)

    StageAll tempDocsDir
    Git.Commit.Commit tempDocsDir (sprintf "Update generated documentation for version %s" release.NugetVersion)
    Branches.push tempDocsDir
)

#load "paket-files/build/fsharp/FAKE/modules/Octokit/Octokit.fsx"
open Octokit

Target "ReleaseGitHub" (fun _ ->
    let user =
        match getBuildParam "github_user" with
        | s when not (String.IsNullOrWhiteSpace s) -> s
        | _ ->
            eprintfn "Please update your release script to set 'github_user'!"
            match getBuildParam "github-user" with
            | s when not (String.IsNullOrWhiteSpace s) -> s
            | _ -> getUserInput "Username: "
    let pw =
        match getBuildParam "github_password" with
        | s when not (String.IsNullOrWhiteSpace s) -> s
        | _ ->
            eprintfn "Please update your release script to set 'github_password'!"
            match getBuildParam "github_pw", getBuildParam "github-pw" with
            | s, _ | _, s when not (String.IsNullOrWhiteSpace s) -> s
            | _ -> getUserPassword "Password: "
    let remote =
        Git.CommandHelper.getGitResult "" "remote -v"
        |> Seq.filter (fun (s: string) -> s.EndsWith("(push)"))
        |> Seq.tryFind (fun (s: string) -> s.Contains(gitOwner + "/" + gitName))
        |> function None -> gitHome + "/" + gitName | Some (s: string) -> s.Split().[0]

    StageAll ""
    Git.Commit.Commit "" (sprintf "Bump version to %s" release.NugetVersion)
    Branches.pushBranch "" remote (Information.getBranchName "")

    Branches.tag "" release.NugetVersion
    Branches.pushTag "" remote release.NugetVersion
    
    // release on github
    createClient user pw
    |> createDraft gitOwner gitName release.NugetVersion (release.SemVer.PreRelease <> None) release.Notes 
    |> uploadFile "./bin/merged/paket.exe"
    |> uploadFile "./bin/merged/paket-sha256.txt"
    |> uploadFile "./bin/paket.bootstrapper.exe"
    |> uploadFile ".paket/paket.targets"
    |> uploadFile ".paket/Paket.Restore.targets"
    |> uploadFile (tempDir </> sprintf "Paket.%s.nupkg" (release.NugetVersion))
    |> releaseDraft
    |> Async.RunSynchronously
)


Target "Release" DoNothing
Target "BuildPackage" DoNothing
Target "BuildCore" DoNothing
// --------------------------------------------------------------------------------------
// Run all targets by default. Invoke 'build <Target>' to override

Target "All" DoNothing

"Clean"
  =?> ("InstallDotNetCore", not <| hasBuildParam "DISABLE_NETCORE")
  =?> ("DotnetRestore", not <| hasBuildParam "DISABLE_NETCORE")
  =?> ("DotnetBuild", not <| hasBuildParam "DISABLE_NETCORE")
  ==> "BuildCore"

"Clean"
  ==> "AssemblyInfo"
  ==> "Build"
  <=> "BuildCore"
  ==> "RunTests"
  =?> ("DotnetTest", not <| hasBuildParam "DISABLE_NETCORE")
  =?> ("GenerateReferenceDocs",isLocalBuild && not isMono && not (hasBuildParam "SkipDocs"))
  =?> ("GenerateDocs",isLocalBuild && not isMono && not (hasBuildParam "SkipDocs"))
  ==> "All"
  =?> ("ReleaseDocs",isLocalBuild && not isMono && not (hasBuildParam "SkipDocs"))

"All"
  ==> "MergePaketTool"
  =?> ("RunIntegrationTests", not <| (hasBuildParam "SkipIntegrationTests" || hasBuildParam "SkipIntegrationTestsNet"))
  =?> ("RunIntegrationTestsNetCore", not <| (hasBuildParam "SkipIntegrationTests" || hasBuildParam "SkipIntegrationTestsNetCore" || hasBuildParam "DISABLE_NETCORE"))
  ==> "SignAssemblies"
  ==> "CalculateDownloadHash"
  =?> ("NuGet", not <| hasBuildParam "SkipNuGet")
  =?> ("DotnetPackage", not <| hasBuildParam "DISABLE_NETCORE" && not <| hasBuildParam "SkipNuGet")
  =?> ("MergeDotnetCoreIntoNuget", not <| hasBuildParam "DISABLE_NETCORE" && not <| hasBuildParam "SkipNuGet")
  ==> "BuildPackage"

"EnsurePackageSigned"
  ?=> "SignAssemblies"


"CleanDocs"
  ==> "GenerateHelp"
  ==> "GenerateReferenceDocs"
  ==> "GenerateDocs"

"CleanDocs"
  ==> "GenerateHelpDebug"

"GenerateHelp"
  ==> "KeepRunning"

"BuildPackage"
  ==> "PublishNuGet"

"PublishNuGet"
  ==> "ReleaseGitHub"
  ==> "Release"

"ReleaseGitHub"
  ?=> "ReleaseDocs"

"ReleaseDocs"
  ==> "Release"

"EnsurePackageSigned"
  ==> "Release"

"DotnetRestoreTools"
  ==> "MergeDotnetCoreIntoNuget"

RunTargetOrDefault "All"
