open System
open System.IO
open System.Collections.Generic

// --------------------------------
// init.fsx 
// This file is run the first time that you run build.sh/build.cmd
// It generates the build.fsx and generate.fsx files 
// --------------------------------
let failfUnlessExists f msg p = if not (f |> File.Exists) then failwithf msg p
let localFile f = Path.Combine(__SOURCE_DIRECTORY__, f)
let buildTemplatePath = localFile "build.template" 
let outputPath = localFile "build.fsx" 

let prompt (msg:string) = 
  Console.Write(msg)
  Console.ReadLine().Trim() |> function | "" -> None | s -> Some s
let promptFor friendlyName = prompt(sprintf "Please enter %s:" friendlyName)

failfUnlessExists buildTemplatePath "Cannot find build template file %s" 
  (Path.GetFullPath buildTemplatePath)


let vars = Dictionary<string,string option>()
vars.["##ProjectName##"] <- promptFor "Project Name (used to name solution and project files)"
vars.["##Summary##"]     <- promptFor "NuGet Summary"
vars.["##Description##"] <- promptFor "NuGet Description"
vars.["##Author##"]      <- promptFor "NuGet Author"
vars.["##Tags##"]        <- promptFor "NuGet Tags"
vars.["##GitHome##"]     <- promptFor "Github User or Organization"
vars.["##GitName##"]     <- promptFor "Name of Project on Github (or blank to use Project Name)"

//Rename solution file
let templateSolutionFile = localFile "FSharp.ProjectScaffold.sln"
failfUnlessExists templateSolutionFile "Cannot find solution file template %s"
            (templateSolutionFile |> Path.GetFullPath)
let projectName = 
  match vars.["##ProjectName##"] with
  | Some p -> p.Replace(" ", "")
  | None -> "ProjectScaffold"
let solutionFile = localFile (projectName + ".sln")
File.Move(templateSolutionFile, solutionFile)

//Rename project files and directories
let projectTemplateFile = localFile "./src/FSharp.ProjectTemplate/FSharp.ProjectTemplate.fsproj"
let testTemplateProjectFile = localFile "./tests/FSharp.ProjectTemplate.Tests/FSharp.ProjectTemplate.Tests.fsproj"
failfUnlessExists projectTemplateFile "Cannot find solution file template %s"
            (projectTemplateFile |> Path.GetFullPath) 
failfUnlessExists testTemplateProjectFile "Cannot find solution file template %s"
            (testTemplateProjectFile |> Path.GetFullPath)
let projectTemplateDirectory = FileInfo(projectTemplateFile).Directory
let testTemplateProjectDirectory = FileInfo(testTemplateProjectFile).Directory

let projectFilePath = Path.Combine(projectTemplateDirectory.FullName, (projectName + ".fsproj"))
let testProjectFilePath = Path.Combine(testTemplateProjectDirectory.FullName, (projectName + ".Tests.fsproj"))
File.Move(projectTemplateFile, projectFilePath)
File.Move(testTemplateProjectFile, testProjectFilePath)
File.Move(projectTemplateDirectory.FullName, Path.Combine(projectTemplateDirectory.Parent.FullName, projectName))
File.Move(testTemplateProjectDirectory.FullName, Path.Combine(testTemplateProjectDirectory.Parent.FullName, projectName + ".Tests"))


let buildTemplate = File.ReadAllLines(buildTemplatePath)
let replaceToken t r (lines:seq<string>) = seq { for s in lines do if s.Contains(t) then yield s.Replace(t, r) else yield s }
let replaceSomeToken t n lines =  replaceToken t (vars.[t] |> function | None -> n | Some s -> s) lines
let newContent = 
  buildTemplate |> Array.toSeq 
  |> replaceToken "##ProjectName##" projectName
  |> replaceSomeToken "##Summary##" "This project has no summmary, please updated build.fsx"
  |> replaceSomeToken "##Description##" "This project has no description, please updated build.fsx"
  |> replaceSomeToken "##Author##" "Update Author in build.fsx" 
  |> replaceSomeToken "##Tags##" "" 
  |> replaceSomeToken "##GitHome##" "Update GitHome in build.fsx"
  |> replaceToken "##GitName##" projectName

let buildFilePath = localFile "build.fsx"
File.WriteAllLines(buildFilePath, newContent)
File.Delete(buildTemplatePath)