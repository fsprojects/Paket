open System
open System.IO
open System.Collections.Generic

// --------------------------------
// init.fsx 
// This file is run the first time that you run build.sh/build.cmd
// It generates the build.fsx and generate.fsx files 
// --------------------------------
let failfUnlessExists f msg p = if not <| File.Exists f then failwithf msg p
let combine p1 p2 = Path.Combine(p2, p1)
let move p1 p2 = 
  if File.Exists p1 then
    printfn "moving %s to %s" p1 p2
    File.Move(p1, p2)
  elif Directory.Exists p1 then
    printfn "moving directory %s to %s" p1 p2
    Directory.Move(p1, p2)
  else
    failwithf "Could not move %s to %s" p1 p2
let localFile f = combine f __SOURCE_DIRECTORY__ 
let buildTemplatePath = localFile "build.template" 
let outputPath = localFile "build.fsx" 

let prompt (msg:string) = 
  Console.Write(msg)
  Console.ReadLine().Trim() |> function | "" -> None | s -> Some s
let runningOnAppveyor =
  not <| String.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI"))
let runningOnTravis =
  not <| String.IsNullOrEmpty(Environment.GetEnvironmentVariable("TRAVIS"))
let inCI = runningOnAppveyor || runningOnTravis
let promptFor friendlyName = 
  if inCI then Some "CONTINUOUSINTEGRATION"
  else prompt (sprintf "%s: " friendlyName)
let rec promptForNoSpaces friendlyName =
  match promptFor friendlyName with
  | None -> None
  | Some s when not <| String.exists (fun c -> c = ' ') s -> Some s
  | _ -> Console.WriteLine("Sorry, spaces are disallowed"); promptForNoSpaces friendlyName

failfUnlessExists buildTemplatePath "Cannot find build template file %s" 
  (Path.GetFullPath buildTemplatePath)

let border = "#####################################################"
let print msg = 
  printfn """
  %s
  %s
  %s
  """ border msg border

print """
# Project Scaffold Init Script
# Please answer a few questions and we will generate
# two files:
#
# build.fsx               This will be your build script
# docs/tools/generate.fsx This script will generate your 
#                         documentation
#
# NOTE: Aside from the Project Name, you may leave any 
# of these blank, but you will need to change the defaults 
# in the generated scripts.
#
"""

let vars = Dictionary<string,string option>()
vars.["##ProjectName##"] <- promptForNoSpaces "Project Name (used for solution/project files)"
vars.["##Summary##"]     <- promptFor "Summary (a short description)"
vars.["##Description##"] <- promptFor "Description (longer description used by NuGet)"
vars.["##Author##"]      <- promptFor "Author"
vars.["##Tags##"]        <- promptFor "Tags (separated by spaces)"
vars.["##GitHome##"]     <- promptFor "Github User or Organization"
vars.["##GitName##"]     <- promptFor "Github Project Name (leave blank to use Project Name)"

let solutionTemplateName = "FSharp.ProjectScaffold"
let projectTemplateName = "FSharp.ProjectTemplate"
let testTemplateProjectName = "FSharp.ProjectTemplate.Tests"

//Rename solution file
let templateSolutionFile = localFile (sprintf "%s.sln" solutionTemplateName)
failfUnlessExists templateSolutionFile "Cannot find solution file template %s"
            (templateSolutionFile |> Path.GetFullPath)
let projectName = 
  match vars.["##ProjectName##"] with
  | Some p -> p.Replace(" ", "")
  | None -> "ProjectScaffold"
let solutionFile = localFile (projectName + ".sln")
move templateSolutionFile solutionFile

//Rename project files and directories
let projectTemplateFile = 
  localFile "src" 
  |> combine projectTemplateName 
  |> combine (projectTemplateName + ".fsproj")
let testTemplateProjectFile = 
  localFile "tests" 
  |> combine testTemplateProjectName 
  |> combine (testTemplateProjectName + ".fsproj")
let nuspecTemplateFile = 
  localFile "nuget"
  |> combine (projectTemplateName + ".nuspec")

failfUnlessExists projectTemplateFile "Cannot find solution file %s"
            (projectTemplateFile |> Path.GetFullPath) 
failfUnlessExists testTemplateProjectFile "Cannot find project file %s"
            (testTemplateProjectFile |> Path.GetFullPath)
failfUnlessExists nuspecTemplateFile "Cannot find project file %s"
            (nuspecTemplateFile |> Path.GetFullPath)
let projectTemplateDirectory = FileInfo(projectTemplateFile).Directory
let testTemplateProjectDirectory = FileInfo(testTemplateProjectFile).Directory
let nugetDirectory = FileInfo(nuspecTemplateFile).Directory

let projectFilePath = 
  projectTemplateDirectory.FullName |> combine (projectName + ".fsproj")
let testProjectFilePath = 
  testTemplateProjectDirectory.FullName |> combine (projectName + ".Tests.fsproj")
let nuspecPath = 
  nugetDirectory.FullName |> combine (projectName + ".nuspec")
  

move projectTemplateFile projectFilePath
move testTemplateProjectFile testProjectFilePath
move nuspecTemplateFile nuspecPath
move projectTemplateDirectory.FullName 
     (combine projectName projectTemplateDirectory.Parent.FullName)
move testTemplateProjectDirectory.FullName 
      (combine (projectName + ".Tests") testTemplateProjectDirectory.Parent.FullName)

//Now that everything is renamed, we need to update the content of some files
let replace t r (lines:seq<string>) = 
  seq { 
    for s in lines do 
      if s.Contains(t) then yield s.Replace(t, r) 
      else yield s }
let overwrite file content = File.WriteAllLines(file, content |> Seq.toArray); file 
let replaceContent file = 
  File.ReadAllLines(file) |> Array.toSeq
  |> replace projectTemplateName projectName
  |> replace solutionTemplateName projectName
  |> overwrite file
  |> sprintf "%s updated"
let rec filesToReplace dir = seq {
  yield! Directory.GetFiles(dir, "*.fsproj")
  yield! Directory.GetFiles(dir, "*.fs")
  yield! Directory.GetFiles(dir, "*.fsx")
  yield! Directory.GetFiles(dir, "*.nuspec")
  yield! Directory.EnumerateDirectories(dir) |> Seq.collect filesToReplace
}
let updateFiles = 
  seq { yield solutionFile 
        yield! filesToReplace <| localFile "src"
        yield! filesToReplace <| localFile "nuget"
        yield! filesToReplace <| localFile "tests" } 
        |> Seq.map replaceContent 
        |> Seq.iter print

//Replace tokens in build template
let generate templatePath generatedFilePath = 
  failfUnlessExists templatePath "Cannot find template %s" (templatePath |> Path.GetFullPath)
  let replaceWithVarOrMsg t n lines = 
    replace t (vars.[t] |> function | None -> n | Some s -> s) lines

  let newContent = 
    File.ReadAllLines(templatePath) |> Array.toSeq 
    |> replace "##ProjectName##" projectName
    |> replaceWithVarOrMsg "##Summary##" "Project has no summmary; update build.fsx"
    |> replaceWithVarOrMsg "##Description##" "Project has no description; update build.fsx"
    |> replaceWithVarOrMsg "##Author##" "Update Author in build.fsx" 
    |> replaceWithVarOrMsg "##Tags##" "" 
    |> replaceWithVarOrMsg "##GitHome##" "Update GitHome in build.fsx"
    |> replace "##GitName##" projectName

  File.WriteAllLines(generatedFilePath, newContent)
  File.Delete(templatePath)
  print (sprintf "# Generated %s" generatedFilePath)

generate (localFile "build.template") (localFile "build.fsx")
generate (localFile "docs/tools/generate.template") (localFile "docs/tools/generate.fsx")
File.Delete "init.fsx"
