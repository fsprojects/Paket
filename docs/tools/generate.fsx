// --------------------------------------------------------------------------------------
// Builds the documentation from `.fsx` and `.md` files in the 'docs/content' directory
// (the generated documentation is stored in the 'docs/output' directory)
// --------------------------------------------------------------------------------------

#I "../../packages/FSharp.Formatting.2.0.4/lib/net40"
#r "../../packages/FAKE/tools/FakeLib.dll"
#r "FSharp.Literate.dll"
#r "FSharp.CodeFormat.dll"
#r "FSharp.MetadataFormat.dll"
open Fake
open System.IO
open Fake.FileHelper
open FSharp.Literate
open FSharp.MetadataFormat
let (++) a b = Path.Combine(a, b)

// Binaries that have XML documentation (in a corresponding generated XML file)
let referenceBinaries = [ "FSharp.ProjectScaffold.dll" ]
// Web site location for the generated documentation
let website = "http://tpetricek.github.io/FSharp.FSharp.ProjectScaffold"

// --------------------------------------------------------------------------------------
// For typical project, no changes are needed below
// --------------------------------------------------------------------------------------

// When called from 'build.fsx', use the public project URL as <root>
// otherwise, use the current 'output' directory.
#if RELEASE
let root = website
#else
let root = "file://" + (__SOURCE_DIRECTORY__ ++ "../output")
#endif

// Paths with template/source/output locations
let bin      = __SOURCE_DIRECTORY__ ++ "../../bin"
let content  = __SOURCE_DIRECTORY__ ++ "../content"
let output   = __SOURCE_DIRECTORY__ ++ "../output"
let files    = __SOURCE_DIRECTORY__ ++ "../files"
let template = __SOURCE_DIRECTORY__ ++ "template.html"
let referenceTemplate = __SOURCE_DIRECTORY__ ++ "reference"

// Build API reference from XML comments
let buildReference () = 
  CleanDir (output ++ "reference")
  for lib in referenceBinaries do
    MetadataFormat.Generate(bin ++ lib, output ++ "reference", referenceTemplate)

// Build documentation from `fsx` and `md` files in `docs/content`
let buildDocumentation () =
  CopyRecursive files output true |> Log "Copying file: "
  let subdirs = Directory.EnumerateDirectories(content, "*", SearchOption.AllDirectories)
  for dir in Seq.append [content] subdirs do
    let sub = if dir.Length > content.Length then dir.Substring(content.Length + 1) else "."
    Literate.ProcessDirectory
      ( dir, template, output ++ sub, 
        replacements = [ "root", root ] )

// Generate 
buildDocumentation()
buildReference()