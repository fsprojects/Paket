module Paket.PackageProcess

open Paket
open System
open System.IO
open Pri.LongPath
open System.Reflection
open Paket.Domain
open Paket.Logging
open System.Collections.Generic
open Paket.PackageMetaData
open Chessie.ErrorHandling
open InstallProcess

let private tryGenerateDescription packageId outputType =
    match packageId with
    | Some id when notNullOrEmpty id ->
        let outputType =
            match outputType with
            | ProjectOutputType.Library -> "library"
            | ProjectOutputType.Exe -> "program"
        Some (sprintf "%s %s." id outputType)
    | _ -> None

let private merge buildConfig buildPlatform versionFromAssembly specificVersions (projectFile:ProjectFile) templateFile = 
    let withVersion =
        match versionFromAssembly with
        | None -> templateFile
        | Some v -> templateFile |> TemplateFile.setVersion (Some v) specificVersions

    match withVersion with
    | { Contents = ProjectInfo(md, opt) } -> 
        let assemblyReader,id,versionFromAssembly,assemblyFileName = readAssemblyFromProjFile buildConfig buildPlatform projectFile
        let attribs = loadAssemblyAttributes assemblyReader

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
                let versionFromAssembly =
                    match md.Id |> Option.bind (fun id -> Map.tryFind id specificVersions) with
                    | Some _ as specificVersion -> specificVersion
                    | None -> getVersion versionFromAssembly attribs

                // See discussion at https://github.com/fsprojects/Paket/pull/1831
                let tryGenerateDescription packageId outputType =
                    match packageId with
                    | Some id -> traceWarnfn "No description was provided for package %A. Generating from ID and project output type." id
                    | _ -> ()
                    tryGenerateDescription packageId outputType

                let execIfNone f opt =
                    match opt with
                    | None -> f ()
                    | x -> x

                let merged = 
                    { Id = md.Id
                      Version = md.Version ++ versionFromAssembly
                      Authors = md.Authors ++ getAuthors attribs
                      Description = md.Description ++ getDescription attribs |> execIfNone (fun _ -> tryGenerateDescription md.Id projectFile.OutputType)
                      Symbols = md.Symbols }

                match merged with
                | Invalid ->
                    let missing =
                        [ if merged.Id = None then yield "Id"
                          if merged.Version = None then yield "Version"
                          if merged.Authors = None || merged.Authors = Some [] then yield "Authors"
                          if merged.Description = None || merged.Description = Some "" then yield "Description" ]
                        |> fun xs -> String.Join(", ",xs)

                    failwithf 
                        "Incomplete mandatory metadata in template file %s (even including assembly attributes)%sTemplate: %A%sMissing: %s" 
                        templateFile.FileName 
                        Environment.NewLine merged
                        Environment.NewLine missing

                | Valid completeCore -> { templateFile with Contents = CompleteInfo(completeCore, mergedOpt) }
    | _ -> templateFile

let private convertToNormal (symbols : bool) templateFile =
    match templateFile.Contents with
    | CompleteInfo(core, optional) ->
        let includePdbs = optional.IncludePdbs
        { templateFile with Contents = CompleteInfo(core, { optional with IncludePdbs = (if symbols then false else includePdbs) }) }
    | ProjectInfo(core, optional) ->
        let includePdbs = optional.IncludePdbs
        { templateFile with Contents = ProjectInfo(core, { optional with IncludePdbs = (if symbols then false else includePdbs) }) }

let private convertToSymbols (projectFile : ProjectFile) (includeReferencedProjects : bool) (projDeps) (templateFile:TemplateFile) =
    let sourceFiles =
        let getTarget compileItem =
            let projectName = Path.GetFileName(compileItem.BaseDir)
            Path.Combine("src", projectName, compileItem.DestinationPath)

        projectFile.GetCompileItems (includeReferencedProjects || templateFile.IncludeReferencedProjects) projDeps
        |> Seq.map (fun c -> c.SourceFile, getTarget c)
        |> Seq.toList

    match templateFile.Contents with
    | CompleteInfo(core, optional) ->
        let augmentedFiles = optional.Files |> List.append sourceFiles
        { templateFile with Contents = CompleteInfo({ core with Symbols = true }, { optional with Files = augmentedFiles }) }
    | ProjectInfo(core, optional) ->
        let augmentedFiles = optional.Files |> List.append sourceFiles
        { templateFile with Contents = ProjectInfo({ core with Symbols = true }, { optional with Files = augmentedFiles }) }

let Pack(workingDir,dependenciesFile : DependenciesFile, packageOutputPath, buildConfig, buildPlatform, version, specificVersions, releaseNotes, templateFile, excludedTemplates, lockDependencies, minimumFromLockFile, pinProjectReferences, symbols, includeReferencedProjects, projectUrl) =
    let buildConfig = defaultArg buildConfig "Release"
    let buildPlatform = defaultArg buildPlatform ""
    let packageOutputPath = if Path.IsPathRooted(packageOutputPath) then packageOutputPath else Path.Combine(workingDir,packageOutputPath)
    Utils.createDir packageOutputPath |> returnOrFail

    let lockFile = 
        let lockFileName = DependenciesFile.FindLockfile dependenciesFile.FileName
        LockFile.LoadFrom(lockFileName.FullName)

    let version = version |> Option.map SemVer.Parse
    let specificVersions = specificVersions |> Seq.map (fun (id : string,v) -> id, SemVer.Parse v) |> Map.ofSeq

    let excludedTemplateIds =
        match excludedTemplates with
        | None -> Set.empty
        | Some excluded -> set excluded

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

    let projDeps = (Dictionary<int,ProjectFile>(),Dictionary<string,int list>())

    // load up project files and grab meta data
    let projectTemplates = 
        let getAllProjectsFiles workingDir =
            ProjectFile.FindAllProjectFiles workingDir
            |> Array.choose (fun (projectFile:FileInfo) ->
                match ProjectFile.FindCorrespondingFile(projectFile, Constants.TemplateFile) with
                | None -> None
                | Some fileName ->
                    match ProjectFile.tryLoad projectFile.FullName with
                    | Some projectFile -> Some(projectFile,TemplateFile.Load(fileName,lockFile,version,specificVersions))
                    | None -> None)
            |> Array.filter (fun (_,templateFile) -> 
                match templateFile with
                | CompleteTemplate _ -> false 
                | IncompleteTemplate -> true)
            |> Array.filter (fun (_,templateFile) -> 
                match TemplateFile.tryGetId templateFile with
                | Some id -> 
                    if excludedTemplateIds.Contains id then
                        allTemplateFiles.Remove(templateFile.FileName) |> ignore
                        false
                    else true
                | _ -> true)
            |> Array.map (fun (projectFile,templateFile') ->
                allTemplateFiles.Remove(templateFile'.FileName) |> ignore

                let merged = merge buildConfig buildPlatform version specificVersions projectFile templateFile'
                Path.GetFullPath projectFile.FileName |> normalizePath,(merged,projectFile))
            |> Map.ofArray

        match templateFile with
        | Some template -> 
            let projects = getAllProjectsFiles (FileInfo(template).Directory.FullName)
            projects
            |> Map.filter (fun p (t,_) -> normalizePath (Path.GetFullPath t.FileName) = normalizePath (Path.GetFullPath template))
        | None -> getAllProjectsFiles workingDir

    // add dependencies
    let allTemplates =
        let optWithSymbols (projectFile:ProjectFile) templateFile =
            seq { 
                yield (templateFile |> convertToNormal symbols)
                if symbols then 
                    yield templateFile |> convertToSymbols projectFile includeReferencedProjects projDeps }

        let convertRemainingTemplate fileName =
            let templateFile = TemplateFile.Load(fileName,lockFile,version,specificVersions)
            match templateFile with
            | { Contents = ProjectInfo(_) } -> 
                let fi = FileInfo(fileName)
                let allProjectFiles = ProjectFile.FindAllProjects(fi.Directory.FullName) |> Array.toList

                match allProjectFiles with
                | [ projectFile ] ->
                    merge buildConfig buildPlatform version specificVersions projectFile templateFile
                    |> optWithSymbols projectFile
                | [] -> failwithf "There was no project file found for template file %s" fileName
                | _ -> failwithf "There was more than one project file found for template file %s" fileName
            | _ -> seq { yield templateFile }

        let remaining = allTemplateFiles |> Seq.collect convertRemainingTemplate |> Seq.toList
        projectTemplates
        |> Map.toList
        |> Seq.collect(fun (_,(t, p)) -> 
            seq {
                for template in optWithSymbols p t do 
                    yield template, p
                }
            )
         |> Seq.map (fun (t, p) -> findDependencies dependenciesFile buildConfig buildPlatform t p lockDependencies minimumFromLockFile pinProjectReferences projectTemplates includeReferencedProjects version specificVersions projDeps)
         |> Seq.append remaining
         |> Seq.toList

    let excludedTemplates =
        allTemplates 
        |> List.filter (fun t -> match t with CompleteTemplate(c,_) -> not (excludedTemplateIds.Contains c.Id) | _ -> true)
    
    // set projectUrl
    let templatesWithProjectUrl = 
        match projectUrl with
        | None -> excludedTemplates
        | Some url -> excludedTemplates |> List.map (TemplateFile.setProjectUrl url)

    // set version
    let templatesWithVersion = templatesWithProjectUrl |> List.map (TemplateFile.setVersion version specificVersions)

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
                    |> NuGetCache.fixDatesInArchive 
                    tracefn "Packed: %s" templateFile.FileName
                | IncompleteTemplate -> 
                    failwithf "There was an attempt to pack incomplete template file %s." templateFile.FileName
            })
    |> Async.Parallel
    |> Async.RunSynchronously
    |> ignore
