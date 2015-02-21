module Paket.PackageProcess

open Paket
open System
open System.IO
open System.Reflection
open Paket.Domain
open Paket.Logging
open System.Collections.Generic
open Paket.PackageMetaData

let Pack(dependencies : DependenciesFile, packageOutputPath, buildConfig, version, releaseNotes) =
    let buildConfig = defaultArg buildConfig "Release"  
    Utils.createDir packageOutputPath |> Rop.returnOrFail
    let rootPath = dependencies.FileName |> Path.GetDirectoryName

    let allTemplateFiles = 
        let hashSet = new HashSet<_>()
        for template in TemplateFile.FindTemplateFiles rootPath do
            hashSet.Add template |> ignore
        hashSet
    
    // load up project files and grab meta data
    let projectTemplates =
        ProjectFile.FindAllProjects rootPath
        |> Array.choose (fun projectFile ->
            match ProjectFile.FindTemplatesFile(FileInfo(projectFile.FileName)) with
            | None -> None
            | Some fileName ->                
                Some(projectFile,TemplateFile.Load fileName))
        |> Array.filter (fun (_,templateFile) -> 
            match templateFile with
            | CompleteTemplate _ -> false 
            | IncompleteTemplate -> true)
        |> Array.map (fun (projectFile,templateFile) ->
            allTemplateFiles.Remove(templateFile.FileName) |> ignore
            let merged = 
                projectFile
                |> loadAssemblyMetadata buildConfig
                |> merge templateFile

            let id = 
                match merged.Contents with 
                | CompleteInfo (c, _) -> c.Id 
                | x -> failwithf "unexpected failure while merging meta data: %A" x

            id,(merged,projectFile))
        |> Map.ofArray

    // add dependencies
    let allTemplates =
        projectTemplates
        |> Map.map (fun _ (t, p) -> p,findDependencies dependencies buildConfig t p projectTemplates)
        |> Map.toList
        |> List.map (fun (_,(_,x)) -> x)
        |> List.append [for fileName in allTemplateFiles -> TemplateFile.Load fileName]
    
    // set version
    let templatesWithVersion =
        match version with
        | None -> allTemplates
        | Some v ->
            let version = SemVer.Parse v
            allTemplates |> List.map (TemplateFile.setVersion version)

        // set release notes
    let processedTemplates =
        match releaseNotes with
        | None ->   templatesWithVersion
        | Some v -> templatesWithVersion |> List.map (TemplateFile.setReleaseNotes releaseNotes)

    // Package all templates
    processedTemplates
    |> List.map (fun templateFile -> 
            async { 
                match templateFile with
                | CompleteTemplate(core, optional) -> 
                    NupkgWriter.Write core optional (Path.GetDirectoryName templateFile.FileName) packageOutputPath
                    verbosefn "Packed: %s" templateFile.FileName
                | IncompleteTemplate -> 
                    failwithf "There was an attempt to pack incomplete template file %s." templateFile.FileName
            })
    |> Async.Parallel
    |> Async.RunSynchronously
    |> ignore