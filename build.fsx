System.IO.Directory.SetCurrentDirectory __SOURCE_DIRECTORY__


#r @"packages/build/FAKE/tools/FakeLib.dll"
#r "System.IO.Compression.FileSystem"
#r "System.Xml.Linq"

open Fake
open Fake.Git
open Fake.ReleaseNotesHelper
open Fake.UserInputHelper
open System
open System.IO
open Fake.Testing.NUnit3
open System.Security.Cryptography
open System.Xml.Linq

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
let solutionFile = "Paket.sln"

// Pattern specifying assemblies to be tested using NUnit
let testAssemblies = "tests/**/bin/Release/net461/*Tests*.dll"
let integrationTestAssemblies = "integrationtests/Paket.IntegrationTests/bin/Release/net461/*Tests*.dll"

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
let buildDirNet461 = buildDir @@ "net461"
let buildDirNetCore = buildDir @@ "netcoreapp2.1"
let buildDirBootstrapper = "bin_bootstrapper"
let buildDirBootstrapperNet461 = buildDirBootstrapper @@ "net461"
let buildDirBootstrapperNetCore = buildDirBootstrapper @@ "netcoreapp2.1"
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


let runDotnet workingDir args =
    let result =
        ExecProcess (fun info ->
            info.FileName <- dotnetExePath
            info.WorkingDirectory <- workingDir
            info.Arguments <- args) TimeSpan.MaxValue
    if result <> 0 then
        failwithf "dotnet %s failed" args

let testSuiteFilterFlakyTests = getEnvironmentVarAsBoolOrDefault "PAKET_TESTSUITE_FLAKYTESTS" false

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
    ++ buildDirNet461
    ++ buildDirNetCore
    ++ buildDirBootstrapper
    ++ buildDirBootstrapperNet461
    ++ buildDirBootstrapperNetCore
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

let releaseNotesProp releaseNotesLines =
    let xn name = XName.Get(name)
    let text = releaseNotesLines |> String.concat Environment.NewLine
    let doc =
        XDocument(
            [ XComment("This document was automatically generated.") :> obj
              XElement(xn "Project",
                XElement(xn "PropertyGroup",
                    XElement(xn "PackageReleaseNotes", text)
                )
              ) :> obj ]
        )

    let path = Path.GetTempFileName()
    doc.Save(path)
    path

let releaseNotesPath = releaseNotesProp release.Notes

let packageProps = [
    sprintf "/p:Version=%s" release.NugetVersion
    sprintf "/p:PackageReleaseNotesFile=%s" releaseNotesPath
]

Target "Build" (fun _ ->
    DotNetCli.Build (fun c ->
        { c with
            Project = solutionFile
            ToolPath = dotnetExePath
            AdditionalArgs = packageProps
        })
)

Target "Restore" (fun _ ->
    DotNetCli.RunCommand (fun c ->
        { c with
            ToolPath = dotnetExePath
        }) "tool restore"

    DotNetCli.Restore (fun c ->
        { c with
            Project = "Paket.sln"
            ToolPath = dotnetExePath
        })
)

Target "Publish" (fun _ ->
    let publishArgs =
        [
            "--no-build"
        ] // since no build, we have to ensure that the build sets assemblyinfo correctly, especially because the publish output of this step
          // is used in the ILRepack of the .net executable

    DotNetCli.Publish (fun c ->
        { c with
            Project = "src/Paket"
            Framework = "net461"
            Output = FullName (currentDirectory </> buildDirNet461)
            ToolPath = dotnetExePath
            AdditionalArgs = publishArgs
        })

    DotNetCli.Publish (fun c ->
        { c with
            Project = "src/Paket"
            Framework = "netcoreapp2.1"
            Output = FullName (currentDirectory </> buildDirNetCore)
            ToolPath = dotnetExePath
            AdditionalArgs = publishArgs
        })

    DotNetCli.Publish (fun c ->
        { c with
            Project = "src/Paket.Bootstrapper"
            Framework = "net461"
            Output = FullName (currentDirectory </> buildDirBootstrapperNet461)
            ToolPath = dotnetExePath
            AdditionalArgs = publishArgs
        })

    DotNetCli.Publish (fun c ->
        { c with
            Project = "src/Paket.Bootstrapper"
            Framework = "netcoreapp2.1"
            Output = FullName (currentDirectory </> buildDirBootstrapperNetCore)
            ToolPath = dotnetExePath
            AdditionalArgs = publishArgs
        })
)
"Clean" ==> "Build" ?=> "Publish"

// --------------------------------------------------------------------------------------
// Run the unit tests

Target "RunTests" (fun _ ->

    let runTest fw proj tfm =
        CreateDir (sprintf "tests_result/%s/%s" fw proj)

        let logFilePath = (sprintf "tests_result/%s/%s/TestResult.trx" fw proj) |> Path.GetFullPath

        DotNetCli.Test (fun c ->
            { c with
                Project = "tests/Paket.Tests/Paket.Tests.fsproj"
                Framework = tfm
                AdditionalArgs =
                  [ "--filter"; (if testSuiteFilterFlakyTests then "TestCategory=Flaky" else "TestCategory!=Flaky")
                    sprintf "--logger:trx;LogFileName=%s" logFilePath
                    "--no-build"
                    "-v"; "n"]
                ToolPath = dotnetExePath
            })

    runTest "net" "Paket.Tests" "net461"
    runTest "netcore" "Paket.Tests" "netcoreapp3.0"

    runTest "net" "Paket.Bootstrapper.Tests" "net461"
    runTest "netcore" "Paket.Bootstrapper.Tests" "netcoreapp3.0"
)

Target "QuickTest" (fun _ ->
    DotNetCli.Test (fun c ->
        { c with
            Project = "tests/Paket.Tests/Paket.Tests.fsproj"
            AdditionalArgs =
              [ "--filter"; (if testSuiteFilterFlakyTests then "TestCategory=Flaky" else "TestCategory!=Flaky") ]
            ToolPath = dotnetExePath
        })
)
"Clean" ==> "QuickTest"

Target "QuickIntegrationTests" (fun _ ->
    DotNetCli.Test (fun c ->
        { c with
            Project = "integrationtests/Paket.IntegrationTests/Paket.IntegrationTests.fsproj"
            AdditionalArgs =
              [ "--filter"; "TestCategory=scriptgen" ]
            TimeOut = TimeSpan.FromMinutes 40.
            ToolPath = dotnetExePath
        })
)
"Clean" ==> "Publish" ==> "QuickIntegrationTests"


// --------------------------------------------------------------------------------------
// Build a NuGet package

Target "MergePaketTool" (fun _ ->
    CreateDir buildMergedDir
    let inBuildDirNet461 (file: string) = buildDirNet461 @@ file

    // syntax for ilrepack requires the 'primary' assembly to be the first positional argument, so we enforce that by not making
    // paket.exe part of the ordered 'component' libraries
    let primaryExe = inBuildDirNet461 "paket.exe"

    let mergeLibs =
        [
            "Argu.dll"
            "Chessie.dll"
            "Fake.Core.ReleaseNotes.dll"
            "FSharp.Core.dll"
            "Mono.Cecil.dll"
            "Newtonsoft.Json.dll"
            "NuGet.Common.dll"
            "NuGet.Configuration.dll"
            "NuGet.Frameworks.dll"
            "NuGet.Packaging.dll"
            "NuGet.Versioning.dll"
            "Paket.Core.dll"
            "System.Buffers.dll"
            "System.Configuration.ConfigurationManager.dll"
            "System.Memory.dll"
            "System.Net.Http.WinHttpHandler.dll"
            "System.Numerics.Vectors.dll"
            "System.Runtime.CompilerServices.Unsafe.dll"
            "System.Security.Cryptography.Cng.dll"
            "System.Security.Cryptography.Pkcs.dll"
            "System.Threading.Tasks.Extensions.dll"
        ]
        |> List.map inBuildDirNet461
        |> separated " "

    let result =
        ExecProcess (fun info ->
            info.FileName <- currentDirectory </> "packages" </> "build" </> "ILRepack" </> "tools" </> "ILRepack.exe"
            info.Arguments <- sprintf "/copyattrs /lib:%s /ver:%s /out:%s %s %s" buildDirNet461 release.AssemblyVersion paketFile primaryExe mergeLibs
            ) (TimeSpan.FromMinutes 5.)

    if result <> 0 then failwithf "Error during ILRepack execution."
)
"Publish" ==> "MergePaketTool"

Target "RunIntegrationTestsNet" (fun _ ->
    CreateDir "tests_result/net/Paket.IntegrationTests"

    // improves the speed of the test-suite by disabling the runtime resolution.
    System.Environment.SetEnvironmentVariable("PAKET_DISABLE_RUNTIME_RESOLUTION", "true")

    DotNetCli.Test (fun c ->
        { c with
            Project = "integrationtests/Paket.IntegrationTests/Paket.IntegrationTests.fsproj"
            Framework = "net461"
            AdditionalArgs =
              [ "--filter"; (if testSuiteFilterFlakyTests then "TestCategory=Flaky" else "TestCategory!=Flaky")
                sprintf "--logger:trx;LogFileName=%s" ("tests_result/net/Paket.IntegrationTests/TestResult.trx" |> Path.GetFullPath) ]
            TimeOut = TimeSpan.FromMinutes 60.
            ToolPath = dotnetExePath
        })

)
"Clean" ==> "Publish" ==> "RunIntegrationTestsNet"


Target "RunIntegrationTestsNetCore" (fun _ ->
    CreateDir "tests_result/netcore/Paket.IntegrationTests"

    // improves the speed of the test-suite by disabling the runtime resolution.
    System.Environment.SetEnvironmentVariable("PAKET_DISABLE_RUNTIME_RESOLUTION", "true")
    DotNetCli.Test (fun c ->
        { c with
            Project = "integrationtests/Paket.IntegrationTests/Paket.IntegrationTests.fsproj"
            Framework = "netcoreapp3.1"
            AdditionalArgs =
              [ "--filter"; (if testSuiteFilterFlakyTests then "TestCategory=Flaky" else "TestCategory!=Flaky")
                sprintf "--logger:trx;LogFileName=%s" ("tests_result/netcore/Paket.IntegrationTests/TestResult.trx" |> Path.GetFullPath) ]
            TimeOut = TimeSpan.FromMinutes 60.
            ToolPath = dotnetExePath
        })
)
"Clean" ==> "Publish" ==> "RunIntegrationTestsNetCore"

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
        ++ "bin_bootstrapper/**/*.exe"
        |> Seq.cache

    if Seq.length filesToSign < 3 then failwith "Didn't find files to sign"

    match getBuildParam "cert-pw" with
    | pw when not (String.IsNullOrWhiteSpace pw) ->
        filesToSign
            |> Seq.iter (fun executable ->
                let signtool = currentDirectory @@ "tools" @@ "SignTool" @@ "signtool.exe"
                let args = sprintf "sign /f %s /p \"%s\" /t http://timestamp.comodoca.com/authenticode %s" pfx pw executable
                let result =
                    ExecProcess (fun info ->
                        info.FileName <- signtool
                        info.Arguments <- args) System.TimeSpan.MaxValue
                if result <> 0 then failwithf "Error during signing %s with %s" executable pfx)
    | _ -> failwith "PW for cert missing"
)

Target "CalculateDownloadHash" (fun _ ->
    use stream = File.OpenRead(paketFile)
    use sha = new SHA256Managed()
    let checksum = sha.ComputeHash(stream)
    let hash = BitConverter.ToString(checksum).Replace("-", String.Empty)
    File.WriteAllText(buildMergedDir @@ "paket-sha256.txt", sprintf "%s paket.exe" hash)
)

Target "AddIconToExe" (fun _ ->
    // add icon to paket.exe
    // workaround https://github.com/dotnet/fsharp/issues/1172
    let paketExeIcon = "src" @@ "Paket" @@ "paket.ico"

    // use resourcehacker to add the icon
    let rhPath = "paket-files" @@ "build" @@ "enricosada" @@ "add_icon_to_exe" @@ "rh" @@ "ResourceHacker.exe"
    let args = sprintf """-open "%s" -save "%s" -action addskip -res "%s" -mask ICONGROUP,MAINICON,""" paketFile paketFile paketExeIcon

    let result =
        ExecProcess (fun info ->
            info.FileName <- rhPath
            info.Arguments <- args) (TimeSpan.FromMinutes 1.)
    if result <> 0 then failwithf "Error during adding icon %s to %s with %s %s" paketExeIcon paketFile rhPath args
)

Target "NuGet" (fun _ ->
    DotNetCli.Pack (fun c ->
        { c with
            Project = "src/Paket.Core/Paket.Core.fsproj"
            OutputPath = tempDir
            AdditionalArgs = packageProps
            ToolPath = dotnetExePath
        })

    DotNetCli.Pack (fun c ->
        { c with
            Project = "src/Paket/Paket.fsproj"
            OutputPath = tempDir
            AdditionalArgs = packageProps @ [ "/p:PackAsTool=true" ]
            ToolPath = dotnetExePath
        })
    DotNetCli.Pack (fun c ->
        { c with
            Project = "src/Paket.Bootstrapper/Paket.Bootstrapper.csproj"
            OutputPath = tempDir
            AdditionalArgs = packageProps @ [ "/p:PackAsTool=true" ]
            ToolPath = dotnetExePath
        })
    DotNetCli.Pack (fun c ->
        { c with
            Project = "src/FSharp.DependencyManager.Paket/FSharp.DependencyManager.Paket.fsproj"
            OutputPath = tempDir
            AdditionalArgs = packageProps
            ToolPath = dotnetExePath
        })
)

Target "PublishNuGet" (fun _ ->
    if hasBuildParam "PublishBootstrapper" |> not then
        !! (tempDir </> "*bootstrapper*")
        |> Seq.iter File.Delete

    Paket.Push (fun p ->
        { p with
            ToolPath = "bin/merged/paket.exe"
            ApiKey = getBuildParam "NugetKey"
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

    tracefn "Creating gihub release"

    // release on github
    createClient user pw
    |> createDraft gitOwner gitName release.NugetVersion (release.SemVer.PreRelease <> None) release.Notes
    |> uploadFile "./bin/merged/paket.exe"
    |> uploadFile "./bin/merged/paket-sha256.txt"
    |> uploadFile "./src/FSharp.DependencyManager.Paket/bin/Release/netstandard2.0/FSharp.DependencyManager.Paket.dll"
    |> uploadFile "./bin_bootstrapper/net461/paket.bootstrapper.exe"
    |> uploadFile ".paket/paket.targets"
    |> uploadFile ".paket/Paket.Restore.targets"
    |> uploadFile (tempDir </> sprintf "Paket.%s.nupkg" (release.NugetVersion))
    |> uploadFile (tempDir </> sprintf "FSharp.DependencyManager.Paket.%s.nupkg" (release.NugetVersion))
    |> releaseDraft
    |> Async.RunSynchronously
)


Target "Release" DoNothing
Target "BuildPackage" DoNothing
// --------------------------------------------------------------------------------------
// Run all targets by default. Invoke 'build <Target>' to override

let hasBuildParams buildParams =
    buildParams
    |> List.map hasBuildParam
    |> List.exists id
let unlessBuildParams buildParams =
    not (hasBuildParams buildParams)

Target "All" DoNothing

"Clean"
  ==> "InstallDotNetCore"
  ==> "Restore"
  ==> "Build"
  ==> "Publish"
  =?> ("RunTests", unlessBuildParams [ "SkipTests"; "SkipUnitTests" ])
  =?> ("GenerateReferenceDocs",isLocalBuild && not isMono && not (hasBuildParam "SkipDocs"))
  =?> ("GenerateDocs",isLocalBuild && not isMono && not (hasBuildParam "SkipDocs"))
  ==> "All"
  =?> ("ReleaseDocs",isLocalBuild && not isMono && not (hasBuildParam "SkipDocs"))

"All"
  ==> "MergePaketTool"
  =?> ("AddIconToExe", not isMono)
  =?> ("RunIntegrationTestsNet", unlessBuildParams [ "SkipTests"; "SkipIntegrationTests"; "SkipIntegrationTestsNet" ] )
  =?> ("RunIntegrationTestsNetCore", unlessBuildParams [ "SkipTests"; "SkipIntegrationTests"; "SkipIntegrationTestsNetCore" ] )
  ==> "SignAssemblies"
  ==> "CalculateDownloadHash"
  =?> ("NuGet", unlessBuildParams [ "SkipNuGet" ])
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

RunTargetOrDefault "All"
