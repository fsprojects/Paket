/// Getting help docs from Paket.exe
#r "../../bin/Argu.dll"
#r "../../bin/Paket.exe"
#r "../../packages/build/Pri.LongPath/lib/net45/Pri.LongPath.dll"
open System.IO
open Pri.LongPath


#if COMMANDS
let MaxCodeWidth = 100

Paket.Commands.getAllCommands()
|> List.iter (fun command ->
    let metadata = command.ParentInfo |> Option.get
    let additionalText = 
        let verboseOption = """

If you add the `--verbose` flag Paket will run in verbose mode and show detailed information.

With `--log-file [path]` you can trace the logged information into a file.

"""
        let optFile = sprintf "../content/commands/%s.md" metadata.Name
        if File.Exists optFile
        then verboseOption + File.ReadAllText optFile
        else verboseOption

    File.WriteAllText(sprintf "../content/paket-%s.md" metadata.Name, Paket.Commands.markdown command MaxCodeWidth additionalText))
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
open Pri.LongPath
open Fake.FileHelper
open FSharp.Literate
open FSharp.MetadataFormat
open FSharp.Formatting.Razor

// Paths with template/source/output locations
let bin        = __SOURCE_DIRECTORY__ @@ "../../bin"
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
