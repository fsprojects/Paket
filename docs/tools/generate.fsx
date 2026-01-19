/// Getting help docs from Paket.exe
open System
open System.IO
open System.Diagnostics

let paketExePath = Path.Combine(__SOURCE_DIRECTORY__, "../../src/Paket/bin/Release/net461/paket.exe")

#if COMMANDS
// Get list of commands by parsing paket --help output
let runPaket args =
    let psi = ProcessStartInfo(paketExePath, args)
    psi.RedirectStandardOutput <- true
    psi.RedirectStandardError <- true
    psi.UseShellExecute <- false
    psi.CreateNoWindow <- true
    use p = Process.Start(psi)
    let output = p.StandardOutput.ReadToEnd()
    p.WaitForExit()
    output

// Parse command names from help output
let helpOutput = runPaket "--help"
let commandLines =
    helpOutput.Split([|'\n'|], StringSplitOptions.RemoveEmptyEntries)
    |> Array.skipWhile (fun l -> not (l.Contains("SUBCOMMANDS")))
    |> Array.skip 1
    |> Array.takeWhile (fun l -> not (l.Trim().StartsWith("Use")))
    |> Array.filter (fun l -> l.Trim().Length > 0)

let commands =
    commandLines
    |> Array.choose (fun line ->
        let trimmed = line.Trim()
        if trimmed.Length > 0 && not (trimmed.StartsWith("--")) then
            let parts = trimmed.Split([|' '|], 2, StringSplitOptions.RemoveEmptyEntries)
            if parts.Length > 0 && not (parts.[0].StartsWith("-")) then
                Some parts.[0]
            else None
        else None)
    |> Array.filter (fun cmd -> cmd <> "<options>")
    |> Array.distinct

// Generate markdown for each command
for commandName in commands do
    let commandHelp = runPaket (sprintf "%s --help" commandName)

    let verboseOption = """

If you add the `--verbose` flag Paket will run in verbose mode and show detailed information.

With `--log-file [path]` you can trace the logged information into a file.

"""
    let optFile = sprintf "../content/commands/%s.md" commandName
    let additionalText =
        if File.Exists optFile
        then verboseOption + File.ReadAllText optFile
        else verboseOption

    // Create markdown content
    let markdown =
        "# paket " + commandName + "\n\n```\n" +
        commandHelp.Trim() + "\n```\n" +
        additionalText

    File.WriteAllText(sprintf "../content/paket-%s.md" commandName, markdown)
    printfn "Generated docs for: %s" commandName
#endif


// --------------------------------------------------------------------------------------
// Builds the documentation from `.fsx` and `.md` files in the 'docs/content' directory
// (the generated documentation is stored in the 'docs/output' directory)
// --------------------------------------------------------------------------------------

// Binaries that have XML documentation (in a corresponding generated XML file)
let referenceBinaries = [ "Paket.Core.dll" ]

let githubLink = "http://github.com/fsprojects/Paket"

// Specify more information about your project
let info =
  [ "project-name", "Paket"
    "project-author", "Steffen Forkmann, Alexander Groß"
    "project-summary", "A dependency manager for .NET with support for NuGet packages and git repositories."
    "project-github", githubLink
    "project-nuget", "http://nuget.org/packages/Paket" ]

// --------------------------------------------------------------------------------------
// For typical project, no changes are needed below
// --------------------------------------------------------------------------------------

#load "../../packages/build/FSharp.Formatting/FSharp.Formatting.fsx"
#I "../../packages/build/FAKE/tools/"
#r "NuGet.Core.dll"
#r "FakeLib.dll"
open Fake
open System.IO
open Fake.FileHelper
open FSharp.Literate
open FSharp.MetadataFormat
open FSharp.Formatting.Razor

// Paths with template/source/output locations
let bin        = __SOURCE_DIRECTORY__ @@ "../../src/paket/bin"
let content    = __SOURCE_DIRECTORY__ @@ "../content"
let completion = __SOURCE_DIRECTORY__ @@ "../../completion"
let output     = __SOURCE_DIRECTORY__ @@ "../output"
let files      = __SOURCE_DIRECTORY__ @@ "../files"
let templates  = __SOURCE_DIRECTORY__ @@ "templates"
let formatting = __SOURCE_DIRECTORY__ @@ "../../packages/build/FSharp.Formatting/"
let docTemplate = formatting @@ "templates/docpage.cshtml"

// Where to look for *.csproj templates (in this order)
let layoutRootsAll = new System.Collections.Generic.Dictionary<string, string list>()
layoutRootsAll.Add("en",[ templates; formatting @@ "templates"
                          formatting @@ "templates/reference" ])
subDirectories (directoryInfo templates)
|> Seq.iter (fun d ->
                let name = d.Name
                if name.Length = 2 || name.Length = 3 then
                    layoutRootsAll.Add(
                            name, [templates @@ name
                                   formatting @@ "templates"
                                   formatting @@ "templates/reference" ]))

// Copy static files and CSS + JS from F# Formatting
let copyFiles () =
  CopyRecursive files output true |> Log "Copying file: "
  ensureDirectory (output @@ "content")
  CopyRecursive (formatting @@ "styles") (output @@ "content") true
    |> Log "Copying styles and scripts: "

// Build API reference from XML comments
let buildReference () =
  CleanDir (output @@ "reference")
  let binaries =
    referenceBinaries
    |> List.map (fun lib-> bin @@ lib)
  RazorMetadataFormat.Generate
    ( binaries, output @@ "reference", layoutRootsAll.["en"],
      parameters = ("root", "../")::info,
      sourceRepo = githubLink @@ "tree/master",
      sourceFolder = __SOURCE_DIRECTORY__ @@ ".." @@ "..",
      publicOnly = true, libDirs = [bin] )

// Build documentation from `fsx` and `md` files in `docs/content`
let buildDocumentation () =
  !!(completion @@ "*.*.md")
  |> Seq.iter (fun f ->
    let target =
      let name = filename f
      name.Replace("README", "shell-completion")

    CopyFile (content @@ target) f
  )

  let subdirs = Directory.EnumerateDirectories(content, "*", SearchOption.AllDirectories)
  for dir in Seq.append [content] subdirs do
    let sub = if dir.Length > content.Length then dir.Substring(content.Length + 1) else "."
    let langSpecificPath(lang, path:string) =
        path.Split([|'/'; '\\'|], System.StringSplitOptions.RemoveEmptyEntries)
        |> Array.exists(fun i -> i = lang)
    let layoutRoots =
        let key = layoutRootsAll.Keys |> Seq.tryFind (fun i -> langSpecificPath(i, dir))
        match key with
        | Some lang -> layoutRootsAll.[lang]
        | None -> layoutRootsAll.["en"] // "en" is the default language
    RazorLiterate.ProcessDirectory
      ( dir, docTemplate, output @@ sub, replacements = ("root", ".")::info,
        layoutRoots = layoutRoots,
        generateAnchors = true )

// Generate
copyFiles()
#if HELP
buildDocumentation()
#endif
#if REFERENCE
buildReference()
#endif
