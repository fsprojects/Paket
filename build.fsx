System.IO.Directory.SetCurrentDirectory __SOURCE_DIRECTORY__


#load ".paket/load/net10.0/BuildScript/Fake.Core.Target.fsx"
#load ".paket/load/net10.0/BuildScript/Fake.Core.ReleaseNotes.fsx"
#load ".paket/load/net10.0/BuildScript/Fake.IO.FileSystem.fsx"
#load ".paket/load/net10.0/BuildScript/Fake.DotNet.Cli.fsx"
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
    | 0 -> failwithf "build.fsx got an argument with an empty parameter name: %s" arg
    | i when i > 0 -> System.Environment.SetEnvironmentVariable(arg.Substring(0, i), arg.Substring(i + 1))
    | _ -> ()

// Outside of the FAKE runner the target module has no execution context of its own, so give it one.
let private targetArgs =
    match commandLineArgs |> Array.filter (fun a -> a.IndexOf '=' < 0) with
    | [||] -> []
    | [| target |] -> [ "--target"; target ]
    | targets -> failwithf "build.fsx expects at most one target name, got: %s" (System.String.Join(" ", targets))

Context.FakeExecutionContext.Create false "build.fsx" targetArgs
|> Context.RuntimeContext.Fake
|> Context.setExecutionContext

// Information about the project are used
//  - for version and project name in generated AssemblyInfo file
//  - by the generated NuGet package
//  - to run tests
//  - for the documentation, the fsdocs parameters are in fsdocsArgs

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

let dotnetcliVersion = DotNet.getSDKVersionFromGlobalJson()

/// Applies the SDK installed by the InstallDotNetCore target to a dotnet invocation.
let mutable dotnetCli : DotNet.Options -> DotNet.Options = id

// --------------------------------------------------------------------------------------
// END TODO: The rest of the file includes standard build steps
// --------------------------------------------------------------------------------------

let buildDir = "bin"
let buildDirNetCore = buildDir @@ "net10.0"
let tempDir = "temp"
let buildLegacyStubDir = buildDir @@ "legacy-stub"
let paketFile = buildLegacyStubDir @@ "paket.exe"

System.Environment.CurrentDirectory <- __SOURCE_DIRECTORY__

// Read additional information from the release notes document
let releaseNotesData =
    let lines = System.IO.File.ReadAllLines "RELEASE_NOTES.md"
    let firstRelease =
        lines
        |> Array.tryFindIndex (fun line ->
            line.StartsWith("#### ", StringComparison.Ordinal)
            && line.Length > 5
            && Char.IsDigit line.[5])
        |> Option.defaultWith (fun () -> failwith "RELEASE_NOTES.md has no released version heading")

    lines.[firstRelease..]
    |> ReleaseNotes.parseAll

let release = List.head releaseNotesData

let DoNothing = ignore

/// FAKE 4's hasBuildParam semantics: an unset or empty variable counts as not set.
let hasBuildParam name = Environment.environVarOrDefault name "" <> ""

let testSuiteFilterFlakyTests = Environment.environVarAsBoolOrDefault "PAKET_TESTSUITE_FLAKYTESTS" false

let testCategoryFilter =
    if testSuiteFilterFlakyTests then "TestCategory=Flaky" else "TestCategory!=Flaky"

Target.create "InstallDotNetCore" (fun _ ->
    // FAKE 6 would install into ~/.dotnet, and its lookup can also settle on a dotnet from PATH that
    // lacks the runtimes the CI installs for the integration tests. Pin the root FAKE 4 used, which
    // is the one .github/workflows/ci.yml feeds.
    let installDir =
        System.Environment.GetFolderPath System.Environment.SpecialFolder.LocalApplicationData
        </> "dotnetcore"

    dotnetCli <-
        DotNet.install (fun c ->
            { c with
                Version = DotNet.CliVersion.Version dotnetcliVersion
                CustomInstallDir = Some installDir })
    // Read back by the integration tests, see integrationtests/Paket.IntegrationTests/TestHelper.fs
    System.Environment.SetEnvironmentVariable("DOTNET_EXE_PATH", (dotnetCli (DotNet.Options.Create())).DotNetCliPath)
)

// --------------------------------------------------------------------------------------
// Clean build results

Target.create "Clean" (fun _ ->
    !! "src/**/bin"
    ++ "tests/**/bin"
    ++ buildDir
    ++ buildDirNetCore
    ++ tempDir
    |> Shell.cleanDirs

    !! "**/obj/**/*.nuspec"
    |> File.deleteAll
)

Target.create "CleanDocs" (fun _ ->
    Shell.cleanDirs ["docs/output"]
    // fsdocs keeps the cracked projects there, whatever the --properties of the next run
    Shell.deleteDir ".fsdocs"
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
    sprintf "/p:PackageReleaseNotesFile=%s" releaseNotesPath
]

Target.create "Build" (fun _ ->
    DotNet.build (fun c ->
        { c with
            Common = c.Common |> dotnetCli |> DotNet.Options.withAdditionalArgs packageProps
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
    // is the paket.exe stub that the old bootstrappers compare with the latest release
    let publish project framework output =
        DotNet.publish (fun c ->
            { c with
                Common = dotnetCli c.Common
                Framework = Some framework
                OutputPath = Some (Path.getFullName (Shell.pwd () </> output))
                NoBuild = true
            }) project

    publish "src/Paket" "net10.0" buildDirNetCore
    publish "src/Paket.LegacyStub" "net461" buildLegacyStubDir
)
"Clean" ==> "Build" ?=> "Publish" |> ignore

// --------------------------------------------------------------------------------------
// Run the unit tests

Target.create "RunTests" (fun _ ->

    let runTest fw (projFile: string) tfm =
        let proj = System.IO.Path.GetFileNameWithoutExtension projFile
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
            }) projFile

    runTest "netcore" "tests/Paket.Tests/Paket.Tests.fsproj" "net10.0"
)

Target.create "QuickTest" (fun _ ->
    // This target builds the test assembly itself, so it has to inject the same version the Build
    // target does: `Loading assembly metadata works` compares the version baked into Paket.Tests.dll
    // with the one in RELEASE_NOTES.md.
    DotNet.test (fun c ->
        { c with
            Common = c.Common |> dotnetCli |> DotNet.Options.withAdditionalArgs packageProps
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

Target.create "RunIntegrationTestsNetCore" (fun _ ->
    Directory.create "tests_result/netcore/Paket.IntegrationTests"

    // improves the speed of the test-suite by disabling the runtime resolution.
    System.Environment.SetEnvironmentVariable("PAKET_DISABLE_RUNTIME_RESOLUTION", "true")

    DotNet.test (fun c ->
        { c with
            // Normal verbosity reports every test as it finishes. Without it only failures are
            // printed, and a run that hangs leaves no trace of which test never returned -- the
            // timeout kills the run before the trx is written.
            // The suite takes a few minutes when it is healthy, so 30 minutes is already a wide
            // margin, and a hung run gives its diagnosis back twice as fast.
            Common =
                { dotnetCli c.Common with
                    Timeout = Some (TimeSpan.FromMinutes 30.)
                    Verbosity = Some DotNet.Verbosity.Normal }
            Configuration = DotNet.BuildConfiguration.Release
            Framework = Some "net10.0"
            Filter = Some testCategoryFilter
            Logger = Some (sprintf "trx;LogFileName=%s" ("tests_result/netcore/Paket.IntegrationTests/TestResult.trx" |> Path.getFullName))
        }) "integrationtests/Paket.IntegrationTests/Paket.IntegrationTests.fsproj"
)
"Clean" ==> "Publish" ==> "RunIntegrationTestsNetCore" |> ignore

Target.create "CalculateDownloadHash" (fun _ ->
    use stream = System.IO.File.OpenRead(paketFile)
    use sha = new SHA256Managed()
    let checksum = sha.ComputeHash(stream)
    let hash = BitConverter.ToString(checksum).Replace("-", String.Empty)
    File.writeString false (buildLegacyStubDir @@ "paket-sha256.txt") (sprintf "%s paket.exe" hash)
)

Target.create "NuGet" (fun _ ->
    let pack project args =
        DotNet.pack (fun c ->
            { c with
                Common = c.Common |> dotnetCli |> DotNet.Options.withAdditionalArgs args
                OutputPath = Some tempDir
            }) project

    pack "src/Paket.Core/Paket.Core.fsproj" packageProps
    pack "src/Paket/Paket.fsproj" (packageProps @ [ "/p:PackAsTool=true" ])
    pack "src/FSharp.DependencyManager.Paket/FSharp.DependencyManager.Paket.fsproj" packageProps
)


// --------------------------------------------------------------------------------------
// Generate the documentation

let fsdocsArgs () =
    [ "--input"; "docs/content"
      "--projects"; "src/Paket.Core/Paket.Core.fsproj"
      // the API reference reads the Paket.Core.dll of the Build target, and the site has no use for
      // the llms.txt files and the Markdown copies of the pages
      "--properties"; "Configuration=Release"; "FsDocsGenerateLlmsTxt=false"
      "--sourcefolder"; __SOURCE_DIRECTORY__
      // the source links of the API reference follow the lines of the built commit, or of master out of
      // a git repository, e.g. in the source archive of a release
      "--sourcerepo"; "https://github.com/fsprojects/Paket/tree/" + (try Git.Information.getCurrentSHA1 "" with _ -> "master")
      "--parameters"
      "root"; "https://fsprojects.github.io/Paket/"
      "fsdocs-collection-name"; "Paket"
      "fsdocs-logo-src"; "img/logo.png"
      "fsdocs-favicon-src"; "img/logo.png"
      "fsdocs-logo-link"; "https://fsprojects.github.io/Paket/"
      "fsdocs-repository-link"; "https://github.com/fsprojects/Paket" ]

let fsdocs command args =
    let result = DotNet.exec dotnetCli "fsdocs" (Args.toWindowsCommandLine (command :: args))
    if not result.OK then failwithf "fsdocs %s failed" command

Target.create "GenerateHelp" (fun _ ->
    File.delete "docs/content/release-notes.md"
    Shell.copyFile "docs/content/" "RELEASE_NOTES.md"
    Shell.rename "docs/content/release-notes.md" "docs/content/RELEASE_NOTES.md"

    File.delete "docs/content/license.md"
    Shell.copyFile "docs/content/" "LICENSE.txt"
    Shell.rename "docs/content/license.md" "docs/content/LICENSE.txt"

    !! "completion/*.*.md"
    |> Seq.iter (fun f ->
        Shell.copyFile ("docs/content" </> (System.IO.Path.GetFileName f).Replace("README", "shell-completion")) f)

    // the pages of commands removed since a previous run, the two others are written by hand
    !! "docs/content/paket-*.md"
    -- "docs/content/paket-folder.md"
    -- "docs/content/paket-and-dotnet-cli.md"
    |> File.deleteAll

    let result = DotNet.exec dotnetCli "fsi" "docs/tools/generate-commands.fsx"
    if not result.OK then failwith "generating the command pages failed"
)

Target.create "GenerateDocs" (fun _ ->
    // --strict: without it, fsdocs skips a project or a page it fails to generate and still succeeds
    fsdocs "build" ([ "--output"; "docs/output"; "--clean"; "--strict" ] @ fsdocsArgs ())
    // fsdocs copies the other files of its input and of its default content: the files of the REPL
    // sample of docs/content and these files of fsdocs don't belong on the site
    [ "docs/output/paket.dependencies"
      "docs/output/paket.lock"
      "docs/output/NuGet.config"
      "docs/output/content/img/github-pages-settings.png" ]
    |> File.deleteAll

    // The paket highlighter of FSharp.Formatting matches a comment with //\s.*?(?:\r|\n|$), line break
    // included, and drops that line break: put it back, but at the end of a code block. fsdocs joins the
    // lines of a code block with Environment.NewLine, so on Windows the match stops at the \r and the \n
    // is still there.
    if Environment.NewLine = "\n" then
        let paketBlock = System.Text.RegularExpressions.Regex("<code lang=\"paket\">.*?</code>", System.Text.RegularExpressions.RegexOptions.Singleline)
        let paketComment = System.Text.RegularExpressions.Regex("(<span class=\"c\">//[^<]*</span>)(?!</code>)")
        !! "docs/output/**/*.html"
        |> Seq.iter (fun file ->
            let html = System.IO.File.ReadAllText file
            let fixedHtml = paketBlock.Replace(html, fun block -> paketComment.Replace(block.Value, "$1\n"))
            if fixedHtml <> html then System.IO.File.WriteAllText(file, fixedHtml))
)

Target.create "KeepRunning" (fun _ ->
    // in tmp/watch: watch serves the site on localhost and writes its links that way. Clean it, or the
    // pages of the commands removed since the last preview stay
    Shell.cleanDir "tmp/watch"
    fsdocs "watch" (fsdocsArgs ())
)
Target.create "BuildPackage" DoNothing
// --------------------------------------------------------------------------------------
// Run all targets by default. Invoke 'build <Target>' to override

let hasBuildParams buildParams =
    buildParams
    |> List.exists hasBuildParam
let unlessBuildParams buildParams =
    not (hasBuildParams buildParams)

Target.create "All" DoNothing

"Clean"
  ==> "InstallDotNetCore"
  ==> "Restore"
  ==> "Build"
  ==> "Publish"
  =?> ("RunTests", unlessBuildParams [ "SkipTests"; "SkipUnitTests" ])
  =?> ("GenerateDocs",BuildServer.isLocalBuild && not (hasBuildParam "SkipDocs"))
  ==> "All"
  |> ignore

"All"
  =?> ("RunIntegrationTestsNetCore", unlessBuildParams [ "SkipTests"; "SkipIntegrationTests"; "SkipIntegrationTestsNetCore" ] )
  ==> "CalculateDownloadHash"
  =?> ("NuGet", unlessBuildParams [ "SkipNuGet" ])
  ==> "BuildPackage"
  |> ignore


// generate-commands.fsx reads the paket.dll of the Publish output
"Publish"
  ==> "GenerateHelp"
  |> ignore

"GenerateHelp"
  ==> "GenerateDocs"
  |> ignore

// a dependency of GenerateDocs only, so that KeepRunning leaves the generated site
"CleanDocs"
  ==> "GenerateDocs"
  |> ignore

"GenerateHelp"
  ==> "KeepRunning"
  |> ignore

Target.runOrDefaultWithArguments "All"
