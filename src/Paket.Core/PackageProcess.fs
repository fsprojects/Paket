module Paket.PackageProcess

open Paket
open System
open System.IO
open System.Reflection
open Paket.Domain
open Paket.Logging
open System.Collections.Generic
open Paket.PackageMetaData
open Chessie.ErrorHandling

let private merge buildConfig version projectFile templateFile = 
    let withVersion =
        match version with
        | None -> templateFile
        | Some v -> templateFile |> TemplateFile.setVersion v

    match withVersion with
    | { Contents = ProjectInfo(md, opt) } -> 
        match md with
        | Valid completeCore -> { templateFile with Contents = CompleteInfo(completeCore, opt) }
        | _ ->
            let assembly,id,assemblyFileName = loadAssemblyId buildConfig projectFile
            let md = { md with Id = md.Id ++ Some id }

            match md with
            | Valid completeCore -> { templateFile with Contents = CompleteInfo(completeCore, opt) }
            | _ ->
                let attribs = loadAssemblyAttributes assemblyFileName assembly

                let merged = 
                    { Id = md.Id
                      Version = md.Version ++ getVersion assembly attribs
                      Authors = md.Authors ++ getAuthors attribs
                      Description = md.Description ++ getDescription attribs }

                match merged with
                | Invalid ->
                    let missing =
                        [ if merged.Id = None then yield "Id"
                          if merged.Version = None then yield "Version"
                          if merged.Authors = None || merged.Authors = Some [] then yield "Authors"
                          if merged.Description = None then yield "Description" ]
                        |> fun xs -> String.Join(", ",xs)

                    failwithf 
                        "Incomplete mandatory metadata in template file %s (even including assembly attributes)%sTemplate: %A%sMissing: %s" 
                        templateFile.FileName 
                        Environment.NewLine md 
                        Environment.NewLine missing

                | Valid completeCore -> { templateFile with Contents = CompleteInfo(completeCore, opt) }
    | _ -> templateFile

let Pack(workingDir,dependencies : DependenciesFile, packageOutputPath, buildConfig, version, releaseNotes, templateFile, lockDependencies) =
    let buildConfig = defaultArg buildConfig "Release"
    let packageOutputPath = if Path.IsPathRooted(packageOutputPath) then packageOutputPath else Path.Combine(workingDir,packageOutputPath)
    Utils.createDir packageOutputPath |> returnOrFail

    let lockFile = 
        let lockFileName = DependenciesFile.FindLockfile dependencies.FileName
        LockFile.LoadFrom(lockFileName.FullName)

    let version = version |> Option.map SemVer.Parse

    let allTemplateFiles = 
        let hashSet = new HashSet<_>()
        match templateFile with
        | Some template ->
            let templatePath = if Path.IsPathRooted(template) then template else Path.Combine(workingDir,template)
            let fi = FileInfo templatePath
            hashSet.Add fi.FullName |> ignore
        | None ->
            for template in TemplateFile.FindTemplateFiles workingDir do
                hashSet.Add template |> ignore
        hashSet
    
    // load up project files and grab meta data
    let projectTemplates = 
        match templateFile with
        | Some template -> Map.empty
        | None ->
            ProjectFile.FindAllProjects workingDir
            |> Array.choose (fun projectFile ->
                match ProjectFile.FindTemplatesFile(FileInfo(projectFile.FileName)) with
                | None -> None
                | Some fileName -> Some(projectFile,TemplateFile.Load(fileName,lockFile,version)))
            |> Array.filter (fun (_,templateFile) -> 
                match templateFile with
                | CompleteTemplate _ -> false 
                | IncompleteTemplate -> true)
            |> Array.map (fun (projectFile,templateFile) ->
                allTemplateFiles.Remove(templateFile.FileName) |> ignore

                let merged = merge buildConfig version projectFile templateFile
                Path.GetFullPath projectFile.FileName |> normalizePath,(merged,projectFile))
            |> Map.ofArray

    // add dependencies
    let allTemplates =
        projectTemplates
        |> Map.map (fun _ (t, p) -> p,findDependencies dependencies buildConfig t p lockDependencies projectTemplates)
        |> Map.toList
        |> List.map (fun (_,(_,x)) -> x)
        |> List.append [for fileName in allTemplateFiles -> 
                            let templateFile = TemplateFile.Load(fileName,lockFile,version)
                            match templateFile with
                            | { Contents = ProjectInfo(_) } -> 
                                let fi = FileInfo(fileName)
                                let allProjectFiles = ProjectFile.FindAllProjects(fi.Directory.FullName) |> Array.toList

                                match allProjectFiles with
                                | [ projectFile ] -> merge buildConfig version projectFile templateFile
                                | [] -> failwithf "There was no project file found for template file %s" fileName
                                | _ -> failwithf "There was more than one project file found for template file %s" fileName
                            | _ -> templateFile ]
    
    // set version
    let templatesWithVersion =
        match version with
        | None -> allTemplates
        | Some v -> allTemplates |> List.map (TemplateFile.setVersion v)

    // set release notes
    let processedTemplates =
        match releaseNotes with
        | None ->   templatesWithVersion
        | Some r -> templatesWithVersion |> List.map (TemplateFile.setReleaseNotes r)

    // Package all templates
    processedTemplates
    |> List.map (fun templateFile -> 
            async { 
                match templateFile with
                | CompleteTemplate(core, optional) -> 
                    NupkgWriter.Write core optional (Path.GetDirectoryName templateFile.FileName) packageOutputPath
                    |> NuGetV2.fixDatesInArchive 
                    verbosefn "Packed: %s" templateFile.FileName
                | IncompleteTemplate -> 
                    failwithf "There was an attempt to pack incomplete template file %s." templateFile.FileName
            })
    |> Async.Parallel
    |> Async.RunSynchronously
    |> ignore
