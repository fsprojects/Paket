System.IO.Directory.SetCurrentDirectory __SOURCE_DIRECTORY__


#load ".paket/load/net10.0/BuildScript/Fake.Core.Target.fsx"
#load ".paket/load/net10.0/BuildScript/Fake.Core.ReleaseNotes.fsx"
#load ".paket/load/net10.0/BuildScript/Fake.Core.UserInput.fsx"
#load ".paket/load/net10.0/BuildScript/Fake.IO.FileSystem.fsx"
#load ".paket/load/net10.0/BuildScript/Fake.DotNet.Cli.fsx"
#load ".paket/load/net10.0/BuildScript/Fake.DotNet.Paket.fsx"
#load ".paket/load/net10.0/BuildScript/Fake.Tools.Git.fsx"

open System
open System.Security.Cryptography
open System.Xml.Linq
open Fake.Core
open Fake.Core.TargetOperators
open Fake.DotNet
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.Tools

// This script is run with `dotnet fsi`, so there is no FAKE.exe runner to parse the
// command line for us. The runner used to turn `build.sh <Target> key=value ...` into
// environment variables, which FAKE then reads back through the environment.
// Do that translation here, before any value below reads a build parameter.
let private commandLineArgs =
    let all = System.Environment.GetCommandLineArgs()
    if all.Length > 2 then all.[2..] else [||] // skip fsi.dll and build.fsx

for arg in commandLineArgs do
    match arg.IndexOf '=' with
    | i when i > 0 -> System.Environment.SetEnvironmentVariable(arg.Substring(0, i), arg.Substring(i + 1))
    | _ -> ()

// Outside of the FAKE runner the target module has no execution context of its own, so give it one.
Context.FakeExecutionContext.Create false "build.fsx"
    [ match commandLineArgs |> Array.tryFind (fun a -> a.IndexOf '=' < 0) with
      | Some target -> yield! [ "--target"; target ]
      | None -> () ]
|> Context.RuntimeContext.Fake
|> Context.setExecutionContext

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

// Git configuration (used for publishing documentation in gh-pages branch)
// The profile where the project is posted
let gitOwner = "fsprojects"
let gitHome = "https://github.com/" + gitOwner

// The name of the project on GitHub
let gitName = "Paket"

let dotnetcliVersion = DotNet.getSDKVersionFromGlobalJson()

/// Applies the SDK installed by the InstallDotNetCore target to a dotnet invocation.
let mutable dotnetCli : DotNet.Options -> DotNet.Options = id

let private withArgs args (o: DotNet.Options) =
    { dotnetCli o with CustomParams = Some (String.separated " " args) }

// --------------------------------------------------------------------------------------
// END TODO: The rest of the file includes standard build steps
// --------------------------------------------------------------------------------------

let buildDir = "bin"
let buildDirNet461 = buildDir @@ "net461"
let buildDirNetCore = buildDir @@ "net10.0"
let buildDirBootstrapper = "bin_bootstrapper"
let buildDirBootstrapperNet461 = buildDirBootstrapper @@ "net461"
let buildDirBootstrapperNetCore = buildDirBootstrapper @@ "net10.0"
let tempDir = "temp"
let buildMergedDir = buildDir @@ "merged"
let paketFile = buildMergedDir @@ "paket.exe"

System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

// Read additional information from the release notes document
let releaseNotesData =
    File.read "RELEASE_NOTES.md"
    |> ReleaseNotes.parseAll

let release = List.head releaseNotesData

let stable =
    match releaseNotesData |> List.tryFind (fun r -> r.NugetVersion.Contains("-") |> not) with
    | Some stable -> stable
    | _ -> release

let DoNothing = ignore

let testSuiteFilterFlakyTests = Environment.environVarAsBoolOrDefault "PAKET_TESTSUITE_FLAKYTESTS" false

let testCategoryFilter =
    if testSuiteFilterFlakyTests then "TestCategory=Flaky" else "TestCategory!=Flaky"

Target.create "InstallDotNetCore" (fun _ ->
    dotnetCli <- DotNet.install (fun c -> { c with Version = DotNet.CliVersion.Version dotnetcliVersion })
    // Read back by the integration tests, see integrationtests/Paket.IntegrationTests/TestHelper.fs
    System.Environment.SetEnvironmentVariable("DOTNET_EXE_PATH", (dotnetCli (DotNet.Options.Create())).DotNetCliPath)
)

// --------------------------------------------------------------------------------------
// Clean build results

Target.create "Clean" (fun _ ->
    !! "src/**/bin"
    ++ "tests/**/bin"
    ++ buildDir
    ++ buildDirNet461
    ++ buildDirNetCore
    ++ buildDirBootstrapper
    ++ buildDirBootstrapperNet461
    ++ buildDirBootstrapperNetCore
    ++ tempDir
    |> Shell.cleanDirs

    !! "**/obj/**/*.nuspec"
    |> File.deleteAll
)

Target.create "CleanDocs" (fun _ ->
    Shell.cleanDirs ["docs/output"]
)

// --------------------------------------------------------------------------------------
// Build library & test project

let releaseNotesProp releaseNotesLines =
    let xn name = XName.Get(name)
    let text = releaseNotesLines |> String.concat System.Environment.NewLine
    let doc =
        XDocument(
            [ XComment("This document was automatically generated.") :> obj
              XElement(xn "Project",
                XElement(xn "PropertyGroup",
                    XElement(xn "PackageReleaseNotes", text)
                )
              ) :> obj ]
        )

    let path = System.IO.Path.GetTempFileName()
    doc.Save(path)
    path

let releaseNotesPath = releaseNotesProp release.Notes

let packageProps = [
    sprintf "/p:Version=%s" release.NugetVersion
    sprintf "/p:PackageReleaseNotesFile=\"%s\"" releaseNotesPath
]

Target.create "Build" (fun _ ->
    DotNet.build (fun c ->
        { c with
            Common = withArgs packageProps c.Common
        }) solutionFile
)

Target.create "Restore" (fun _ ->
    let result = DotNet.exec dotnetCli "tool" "restore"
    if not result.OK then failwith "dotnet tool restore failed"

    DotNet.restore (fun c ->
        { c with
            Common = dotnetCli c.Common
        }) "Paket.sln"
)

Target.create "Publish" (fun _ ->
    // since no build, we have to ensure that the build sets assemblyinfo correctly, especially because the publish output of this step
    // is used in the ILRepack of the .net executable
    let publish project framework output =
        DotNet.publish (fun c ->
            { c with
                Common = dotnetCli c.Common
                Framework = Some framework
                OutputPath = Some (Path.getFullName (Shell.pwd () </> output))
                NoBuild = true
            }) project

    publish "src/Paket" "net461" buildDirNet461
    publish "src/Paket" "net10.0" buildDirNetCore
    publish "src/Paket.Bootstrapper" "net461" buildDirBootstrapperNet461
    publish "src/Paket.Bootstrapper" "net10.0" buildDirBootstrapperNetCore
)
"Clean" ==> "Build" ?=> "Publish" |> ignore

// --------------------------------------------------------------------------------------
// Run the unit tests

Target.create "RunTests" (fun _ ->

    let runTest fw proj tfm =
        Directory.create (sprintf "tests_result/%s/%s" fw proj)

        let logFilePath = (sprintf "tests_result/%s/%s/TestResult.trx" fw proj) |> Path.getFullName

        DotNet.test (fun c ->
            { c with
                Common = { dotnetCli c.Common with Verbosity = Some DotNet.Verbosity.Normal }
                Configuration = DotNet.BuildConfiguration.Release
                Framework = Some tfm
                Filter = Some testCategoryFilter
                Logger = Some (sprintf "trx;LogFileName=%s" logFilePath)
                NoBuild = true
            }) "tests/Paket.Tests/Paket.Tests.fsproj"

    runTest "net" "Paket.Tests" "net461"
    runTest "netcore" "Paket.Tests" "netcoreapp3.0"

    runTest "net" "Paket.Bootstrapper.Tests" "net461"
    runTest "netcore" "Paket.Bootstrapper.Tests" "netcoreapp3.0"
)

Target.create "QuickTest" (fun _ ->
    // This target builds the test assembly itself, so it has to inject the same version the Build
    // target does: `Loading assembly metadata works` compares the version baked into Paket.Tests.dll
    // with the one in RELEASE_NOTES.md.
    DotNet.test (fun c ->
        { c with
            Common = withArgs packageProps c.Common
            Configuration = DotNet.BuildConfiguration.Release
            Filter = Some testCategoryFilter
        }) "tests/Paket.Tests/Paket.Tests.fsproj"
)
"Clean" ==> "QuickTest" |> ignore

Target.create "QuickIntegrationTests" (fun _ ->
    DotNet.test (fun c ->
        { c with
            Common = { dotnetCli c.Common with Timeout = Some (TimeSpan.FromMinutes 40.) }
            Configuration = DotNet.BuildConfiguration.Release
            Filter = Some "TestCategory=scriptgen"
        }) "integrationtests/Paket.IntegrationTests/Paket.IntegrationTests.fsproj"
)
"Clean" ==> "Publish" ==> "QuickIntegrationTests" |> ignore


// --------------------------------------------------------------------------------------
// Build a NuGet package

Target.create "MergePaketTool" (fun _ ->
    Directory.create buildMergedDir
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
        |> String.separated " "

    // The .NET Framework reference assemblies used to come for free from the Mono that ran
    // ILRepack.exe. The dotnet-ilrepack tool runs on .NET, so point it at the same directory
    // MSBuild resolves net461 against (see TargetFrameworkRootPath in Directory.Build.props).
    let referenceAssemblies =
        "packages" </> "build" </> "0x53A.ReferenceAssemblies.Paket" </> "tools" </> "framework" </> ".NETFramework" </> "v4.5"

    let result =
        DotNet.exec dotnetCli "ilrepack"
            (sprintf "/copyattrs /targetplatform:v4,%s /lib:%s /lib:%s /ver:%s /out:%s %s %s"
                referenceAssemblies referenceAssemblies buildDirNet461 release.AssemblyVersion paketFile primaryExe mergeLibs)

    if not result.OK then failwithf "Error during ILRepack execution."
)
"Publish" ==> "MergePaketTool" |> ignore

Target.create "RunIntegrationTestsNet" (fun _ ->
    Directory.create "tests_result/net/Paket.IntegrationTests"

    // improves the speed of the test-suite by disabling the runtime resolution.
    System.Environment.SetEnvironmentVariable("PAKET_DISABLE_RUNTIME_RESOLUTION", "true")

    DotNet.test (fun c ->
        { c with
            Common = { dotnetCli c.Common with Timeout = Some (TimeSpan.FromMinutes 60.) }
            Configuration = DotNet.BuildConfiguration.Release
            Framework = Some "net461"
            Filter = Some testCategoryFilter
            Logger = Some (sprintf "trx;LogFileName=%s" ("tests_result/net/Paket.IntegrationTests/TestResult.trx" |> Path.getFullName))
        }) "integrationtests/Paket.IntegrationTests/Paket.IntegrationTests.fsproj"

)
"Clean" ==> "Publish" ==> "RunIntegrationTestsNet" |> ignore


Target.create "RunIntegrationTestsNetCore" (fun _ ->
    Directory.create "tests_result/netcore/Paket.IntegrationTests"

    // improves the speed of the test-suite by disabling the runtime resolution.
    System.Environment.SetEnvironmentVariable("PAKET_DISABLE_RUNTIME_RESOLUTION", "true")

    DotNet.test (fun c ->
        { c with
            Common = { dotnetCli c.Common with Timeout = Some (TimeSpan.FromMinutes 60.) }
            Configuration = DotNet.BuildConfiguration.Release
            Framework = Some "net10.0"
            Filter = Some testCategoryFilter
            Logger = Some (sprintf "trx;LogFileName=%s" ("tests_result/netcore/Paket.IntegrationTests/TestResult.trx" |> Path.getFullName))
        }) "integrationtests/Paket.IntegrationTests/Paket.IntegrationTests.fsproj"
)
"Clean" ==> "Publish" ==> "RunIntegrationTestsNetCore" |> ignore

let pfx = "code-sign.pfx"
let mutable isUnsignedAllowed = true
Target.create "EnsurePackageSigned" (fun _ -> isUnsignedAllowed <- false)

Target.create "SignAssemblies" (fun _ ->
    // if not <| File.exists pfx then
    //     if isUnsignedAllowed then ()
    //     else failwithf "%s not found, can't sign assemblies" pfx
    // else

    // let filesToSign =
    //     !! "bin/**/*.exe"
    //     ++ "bin/**/Paket.Core.dll"
    //     ++ "bin_bootstrapper/**/*.exe"
    //     |> Seq.cache

    // if Seq.length filesToSign < 3 then failwith "Didn't find files to sign"

    // match Environment.environVarOrDefault "cert-pw" "" with
    // | pw when not (System.String.IsNullOrWhiteSpace pw) ->
    //     filesToSign
    //         |> Seq.iter (fun executable ->
    //             let signtool = Shell.pwd () @@ "tools" @@ "SignTool" @@ "signtool.exe"
    //             let args = sprintf "sign /f %s /p \"%s\" /t http://timestamp.comodoca.com/authenticode %s" pfx pw executable
    //             let result =
    //                 CreateProcess.fromRawCommandLine signtool args
    //                 |> Proc.run
    //             if result.ExitCode <> 0 then failwithf "Error during signing %s with %s" executable pfx)
    // | _ -> failwith "PW for cert missing"
    ()
)

Target.create "CalculateDownloadHash" (fun _ ->
    use stream = System.IO.File.OpenRead(paketFile)
    use sha = new SHA256Managed()
    let checksum = sha.ComputeHash(stream)
    let hash = BitConverter.ToString(checksum).Replace("-", String.Empty)
    File.writeString false (buildMergedDir @@ "paket-sha256.txt") (sprintf "%s paket.exe" hash)
)

Target.create "AddIconToExe" (fun _ ->
    // add icon to paket.exe
    // workaround https://github.com/dotnet/fsharp/issues/1172
    let paketExeIcon = "src" @@ "Paket" @@ "paket.ico"

    // use resourcehacker to add the icon
    let rhPath = "paket-files" @@ "build" @@ "enricosada" @@ "add_icon_to_exe" @@ "rh" @@ "ResourceHacker.exe"
    let args = sprintf """-open "%s" -save "%s" -action addskip -res "%s" -mask ICONGROUP,MAINICON,""" paketFile paketFile paketExeIcon

    let result =
        CreateProcess.fromRawCommandLine rhPath args
        |> CreateProcess.withTimeout (TimeSpan.FromMinutes 1.)
        |> Proc.run

    if result.ExitCode <> 0 then failwithf "Error during adding icon %s to %s with %s %s" paketExeIcon paketFile rhPath args
)

Target.create "NuGet" (fun _ ->
    let pack project args =
        DotNet.pack (fun c ->
            { c with
                Common = withArgs args c.Common
                OutputPath = Some tempDir
            }) project

    pack "src/Paket.Core/Paket.Core.fsproj" packageProps
    pack "src/Paket/Paket.fsproj" (packageProps @ [ "/p:PackAsTool=true" ])
    pack "src/Paket.Bootstrapper/Paket.Bootstrapper.csproj" (packageProps @ [ "/p:PackAsTool=true" ])
    pack "src/FSharp.DependencyManager.Paket/FSharp.DependencyManager.Paket.fsproj" packageProps
)

Target.create "PublishNuGet" (fun _ ->
    if Environment.hasEnvironVar "PublishBootstrapper" |> not then
        !! (tempDir </> "*bootstrapper*")
        |> File.deleteAll

    Paket.push (fun p ->
        { p with
            ToolPath = "bin/merged/paket.exe"
            // paket.exe is a .NET Framework binary, so it goes through Mono outside of Windows
            ToolType = ToolType.CreateFullFramework()
            ApiKey = Environment.environVarOrDefault "NugetKey" ""
            WorkingDir = tempDir })
)


// --------------------------------------------------------------------------------------
// Generate the documentation

let disableDocs = false // https://github.com/fsprojects/FSharp.Formatting/issues/461

// docs/tools/generate.fsx is still a FAKE 4 script bound to FSharp.Formatting 3, so it keeps
// running through the FAKE 4 runner of the Build group. Windows-only, see the target graph.
let fakePath = __SOURCE_DIRECTORY__ @@ "packages" @@ "build" @@ "FAKE" @@ "tools" @@ "FAKE.exe"

/// Run generate.fsx through the FAKE 4 runner, printing its output live, and fail on a non-zero exit code
let execute fail traceMsg failMessage fsiargs script workingDirectory =
    Trace.trace traceMsg

    let result =
        CreateProcess.fromRawCommandLine fakePath (sprintf "--fsiargs %s -d:FAKE \"%s\"" fsiargs script)
        |> CreateProcess.withWorkingDirectory workingDirectory
        |> Proc.run

    if result.ExitCode <> 0 then
        if fail then
            failwith failMessage
        else
            Trace.traceImportant failMessage
    else
        Trace.traceImportant "Succeeded"

Target.create "GenerateReferenceDocs" (fun _ ->
    if disableDocs then () else
    let args = ["--define:RELEASE"; "--define:REFERENCE"]
    let argLine = System.String.Join(" ", args)
    execute
      true
      (sprintf "Building reference documentation, this could take some time, please wait...")
      "generating reference documentation failed"
      argLine "generate.fsx" "docs/tools"
)




let generateHelp' commands fail debug =
    // remove FSharp.Compiler.Service.MSBuild.v12.dll
    // otherwise FCS thinks  it should use msbuild, which leads to insanity
    !! "packages/**/FSharp.Compiler.Service.MSBuild.*.dll"
    |> File.deleteAll

    let args =
        [ if not debug then yield "--define:RELEASE"
          if commands then yield "--define:COMMANDS"
          yield "--define:HELP"]
    let argLine = System.String.Join(" ", args)
    execute
      fail
      (sprintf "Building documentation (%A), this could take some time, please wait..." commands)
      "generating documentation failed"
      argLine "generate.fsx" "docs/tools"

    Shell.cleanDir "docs/output/commands"

let generateHelp commands fail =
    generateHelp' commands fail false

Target.create "GenerateHelp" (fun _ ->
    if disableDocs then () else
    File.delete "docs/content/release-notes.md"
    Shell.copyFile "docs/content/" "RELEASE_NOTES.md"
    Shell.rename "docs/content/release-notes.md" "docs/content/RELEASE_NOTES.md"

    File.delete "docs/content/license.md"
    Shell.copyFile "docs/content/" "LICENSE.txt"
    Shell.rename "docs/content/license.md" "docs/content/LICENSE.txt"

    generateHelp true true
)

Target.create "GenerateHelpDebug" (fun _ ->
    if disableDocs then () else
    File.delete "docs/content/release-notes.md"
    Shell.copyFile "docs/content/" "RELEASE_NOTES.md"
    Shell.rename "docs/content/release-notes.md" "docs/content/RELEASE_NOTES.md"

    File.delete "docs/content/license.md"
    Shell.copyFile "docs/content/" "LICENSE.txt"
    Shell.rename "docs/content/license.md" "docs/content/LICENSE.txt"

    generateHelp' true true true
)

Target.create "KeepRunning" (fun _ ->
    use watcher = !! "docs/content/**/*.*" |> ChangeWatcher.run (fun changes ->
         generateHelp false false
    )

    Trace.traceImportant "Waiting for help edits. Press any key to stop."

    System.Console.ReadKey() |> ignore

    watcher.Dispose()
)

Target.create "GenerateDocs" DoNothing

// --------------------------------------------------------------------------------------
// Release Scripts

Target.create "ReleaseDocs" (fun _ ->
    if disableDocs then () else
    let tempDocsDir = "temp/gh-pages"
    Shell.cleanDir tempDocsDir
    Git.Repository.cloneSingleBranch "" "git@github.com:fsprojects/Paket.git" "gh-pages" tempDocsDir

    Git.CommandHelper.runSimpleGitCommand tempDocsDir "rm . -f -r" |> ignore
    Shell.copyRecursive "docs/output" tempDocsDir true |> Trace.tracefn "%A"

    File.writeString false "temp/gh-pages/latest" (sprintf "https://github.com/fsprojects/Paket/releases/download/%s/paket.exe" release.NugetVersion)
    File.writeString false "temp/gh-pages/stable" (sprintf "https://github.com/fsprojects/Paket/releases/download/%s/paket.exe" stable.NugetVersion)

    Git.Staging.stageAll tempDocsDir
    Git.Commit.exec tempDocsDir (sprintf "Update generated documentation for version %s" release.NugetVersion)
    Git.Branches.push tempDocsDir
)

#load "paket-files/build/fsharp/FAKE/modules/Octokit/Octokit.fsx"
open Octokit

Target.create "ReleaseGitHub" (fun _ ->
    let user =
        match Environment.environVarOrDefault "github_user" "" with
        | s when not (System.String.IsNullOrWhiteSpace s) -> s
        | _ ->
            eprintfn "Please update your release script to set 'github_user'!"
            match Environment.environVarOrDefault "github-user" "" with
            | s when not (System.String.IsNullOrWhiteSpace s) -> s
            | _ -> UserInput.getUserInput "Username: "
    let pw =
        match Environment.environVarOrDefault "github_password" "" with
        | s when not (System.String.IsNullOrWhiteSpace s) -> s
        | _ ->
            eprintfn "Please update your release script to set 'github_password'!"
            match Environment.environVarOrDefault "github_pw" "", Environment.environVarOrDefault "github-pw" "" with
            | s, _ | _, s when not (System.String.IsNullOrWhiteSpace s) -> s
            | _ -> UserInput.getUserPassword "Password: "
    let remote =
        Git.CommandHelper.getGitResult "" "remote -v"
        |> Seq.filter (fun (s: string) -> s.EndsWith("(push)"))
        |> Seq.tryFind (fun (s: string) -> s.Contains(gitOwner + "/" + gitName))
        |> function None -> gitHome + "/" + gitName | Some (s: string) -> s.Split().[0]

    Git.Staging.stageAll ""
    Git.Commit.exec "" (sprintf "Bump version to %s" release.NugetVersion)
    Git.Branches.pushBranch "" remote (Git.Information.getBranchName "")

    Git.Branches.tag "" release.NugetVersion
    Git.Branches.pushTag "" remote release.NugetVersion

    Trace.tracefn "Creating gihub release"

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


Target.create "Release" DoNothing
Target.create "BuildPackage" DoNothing
// --------------------------------------------------------------------------------------
// Run all targets by default. Invoke 'build <Target>' to override

let hasBuildParams buildParams =
    buildParams
    |> List.map Environment.hasEnvironVar
    |> List.exists id
let unlessBuildParams buildParams =
    not (hasBuildParams buildParams)

Target.create "All" DoNothing

"Clean"
  ==> "InstallDotNetCore"
  ==> "Restore"
  ==> "Build"
  ==> "Publish"
  =?> ("RunTests", unlessBuildParams [ "SkipTests"; "SkipUnitTests" ])
  =?> ("GenerateReferenceDocs",BuildServer.isLocalBuild && Environment.isWindows && not (Environment.hasEnvironVar "SkipDocs"))
  =?> ("GenerateDocs",BuildServer.isLocalBuild && Environment.isWindows && not (Environment.hasEnvironVar "SkipDocs"))
  ==> "All"
  =?> ("ReleaseDocs",BuildServer.isLocalBuild && Environment.isWindows && not (Environment.hasEnvironVar "SkipDocs"))
  |> ignore

"All"
  ==> "MergePaketTool"
  =?> ("AddIconToExe", Environment.isWindows)
  =?> ("RunIntegrationTestsNet", unlessBuildParams [ "SkipTests"; "SkipIntegrationTests"; "SkipIntegrationTestsNet" ] )
  =?> ("RunIntegrationTestsNetCore", unlessBuildParams [ "SkipTests"; "SkipIntegrationTests"; "SkipIntegrationTestsNetCore" ] )
  ==> "SignAssemblies"
  ==> "CalculateDownloadHash"
  =?> ("NuGet", unlessBuildParams [ "SkipNuGet" ])
  ==> "BuildPackage"
  |> ignore

"EnsurePackageSigned"
  ?=> "SignAssemblies"
  |> ignore


"CleanDocs"
  ==> "GenerateHelp"
  ==> "GenerateReferenceDocs"
  ==> "GenerateDocs"
  |> ignore

"CleanDocs"
  ==> "GenerateHelpDebug"
  |> ignore

"GenerateHelp"
  ==> "KeepRunning"
  |> ignore

"BuildPackage"
  ==> "PublishNuGet"
  |> ignore

"ReleaseGitHub"
  ==> "ReleaseDocs"
  ==> "PublishNuGet"
  ==> "Release"
  |> ignore

"EnsurePackageSigned"
  ==> "Release"
  |> ignore

Target.runOrDefaultWithArguments "All"
