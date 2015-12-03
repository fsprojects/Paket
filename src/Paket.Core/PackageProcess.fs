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

let private merge buildConfig buildPlatform version projectFile templateFile = 
    let withVersion =
        match version with
        | None -> templateFile
        | Some v -> templateFile |> TemplateFile.setVersion v

    match withVersion with
    | { Contents = ProjectInfo(md, opt) } -> 
        let assembly,id,assemblyFileName = loadAssemblyId buildConfig buildPlatform projectFile
        let attribs = loadAssemblyAttributes assemblyFileName assembly

        let mergedOpt =
            match opt.Title with
            | Some _ -> opt
            | None -> { opt with Title = getTitle attribs }

        match md with
        | Valid completeCore -> { templateFile with Contents = CompleteInfo(completeCore, mergedOpt) }
        | _ ->
            let md = { md with Id = md.Id ++ Some id }

            match md with
            | Valid completeCore -> { templateFile with Contents = CompleteInfo(completeCore, mergedOpt) }
            | _ ->

                let merged = 
                    { Id = md.Id
                      Version = md.Version ++ getVersion assembly attribs
                      Authors = md.Authors ++ getAuthors attribs
                      Description = md.Description ++ getDescription attribs
                      Symbols = md.Symbols }

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

                | Valid completeCore -> { templateFile with Contents = CompleteInfo(completeCore, mergedOpt) }
    | _ -> templateFile

let private convertToSymbols (projectFile : ProjectFile) templateFile =
    let sourceFiles =
        let getTarget compileItem =
            match compileItem.Link with
            | Some link -> link
            | None -> compileItem.Include
            |> Path.GetDirectoryName
            |> (fun d -> Path.Combine("src", d))

        projectFile.GetCompileItems()
        |> Seq.map (fun c -> c.Include, getTarget c)
        |> Seq.toList

    match templateFile.Contents with
    | CompleteInfo(core, optional) ->
        let augmentedFiles = optional.Files |> List.append sourceFiles 
        { templateFile with Contents = CompleteInfo({ core with Symbols = true }, { optional with Files = augmentedFiles }) }
    | ProjectInfo(core, optional) ->
        let augmentedFiles = optional.Files |> List.append sourceFiles 
        { templateFile with Contents = ProjectInfo({ core with Symbols = true }, { optional with Files = augmentedFiles }) }

let Pack(workingDir,dependencies : DependenciesFile, packageOutputPath, buildConfig, buildPlatform, version, releaseNotes, templateFile, excludedTemplates, lockDependencies, symbols) =
    let buildConfig = defaultArg buildConfig "Release"
    let buildPlatform = defaultArg buildPlatform ""
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

                let merged = merge buildConfig buildPlatform version projectFile templateFile
                Path.GetFullPath projectFile.FileName |> normalizePath,(merged,projectFile))
            |> Map.ofArray

    // add dependencies
    let allTemplates =
        let optWithSymbols projectFile templateFile =
            seq { yield templateFile; if symbols then yield templateFile |> convertToSymbols projectFile }

        let convertRemainingTemplate fileName =
            let templateFile = TemplateFile.Load(fileName,lockFile,version)
            match templateFile with
            | { Contents = ProjectInfo(_) } -> 
                let fi = FileInfo(fileName)
                let allProjectFiles = ProjectFile.FindAllProjects(fi.Directory.FullName) |> Array.toList

                match allProjectFiles with
                | [ projectFile ] ->
                    merge buildConfig buildPlatform version projectFile templateFile
                    |> optWithSymbols projectFile
                | [] -> failwithf "There was no project file found for template file %s" fileName
                | _ -> failwithf "There was more than one project file found for template file %s" fileName
            | _ -> seq { yield templateFile }

        projectTemplates
        |> Map.map (fun _ (t, p) -> p,findDependencies dependencies buildConfig buildPlatform t p lockDependencies projectTemplates)
        |> Map.toList
        |> Seq.collect (fun (_,(p,t)) -> t |> optWithSymbols p)
        |> Seq.append (allTemplateFiles |> Seq.collect convertRemainingTemplate)
        |> Seq.toList

    let excludedTemplates =
        match excludedTemplates with
        | None -> allTemplates
        | Some excluded -> 
            let excluded = set excluded
            allTemplates |> List.filter (fun t -> match t with CompleteTemplate(c,_) -> not (excluded.Contains c.Id) | _ -> true)
    
    // set version
    let templatesWithVersion =
        match version with
        | None -> excludedTemplates
        | Some v -> excludedTemplates |> List.map (TemplateFile.setVersion v)

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
                    tracefn "Packed: %s" templateFile.FileName
                | IncompleteTemplate -> 
                    failwithf "There was an attempt to pack incomplete template file %s." templateFile.FileName
            })
    |> Async.Parallel
    |> Async.RunSynchronously
    |> ignore
