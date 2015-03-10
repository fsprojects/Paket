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

let Pack(dependencies : DependenciesFile, packageOutputPath, buildConfig, version, releaseNotes) =
    let buildConfig = defaultArg buildConfig "Release"  
    Utils.createDir packageOutputPath |> returnOrFail
    let rootPath = dependencies.FileName |> Path.GetDirectoryName

    let version = version |> Option.map SemVer.Parse

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
                let withVersion =  
                    match version with
                    | None -> templateFile
                    | Some v -> templateFile |> TemplateFile.setVersion v

                match withVersion with
                | { Contents = ProjectInfo(md, opt) } -> 
                    match md with
                    | Valid completeCore -> { templateFile with Contents = CompleteInfo(completeCore, opt) }
                    | _ ->
                        let assembly,id = loadAssemblyId buildConfig projectFile
                        let md = { md with Id = md.Id ++ Some id }

                        match md with
                        | Valid completeCore -> { templateFile with Contents = CompleteInfo(completeCore, opt) }
                        | _ ->
                            let attribs = loadAssemblyAttributes assembly

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

            let id = 
                match merged.Contents with
                | CompleteInfo _ -> projectFile.NameWithoutExtension
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
                    verbosefn "Packed: %s" templateFile.FileName
                | IncompleteTemplate -> 
                    failwithf "There was an trial to pack incomplete template file %s." templateFile.FileName
            })
    |> Async.Parallel
    |> Async.RunSynchronously
    |> ignore