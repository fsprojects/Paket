module Paket.PackageProcess

open Paket
open System
open System.IO
open System.Reflection
open Paket.Domain
open Paket.Logging
open System.Collections.Generic

let internal (|CompleteTemplate|IncompleteTemplate|) templateFile = 
    match templateFile with
    | { Contents = (CompleteInfo(core, optional)) } -> CompleteTemplate(core, optional)
    | _ -> IncompleteTemplate

let (|Title|Description|Version|InformationalVersion|Company|Ignore|) (attribute : obj) = 
    match attribute with
    | :? AssemblyTitleAttribute as title -> Title title.Title
    | :? AssemblyDescriptionAttribute as description -> Description description.Description
    | :? AssemblyVersionAttribute as version -> Version(SemVer.Parse version.Version)
    | :? AssemblyInformationalVersionAttribute as version -> 
        InformationalVersion(SemVer.Parse version.InformationalVersion)
    | :? AssemblyCompanyAttribute as company -> Company company.Company
    | _ -> Ignore

let internal getId (assembly : Assembly) (md : ProjectCoreInfo) = { md with Id = Some(assembly.GetName().Name) }

let internal getVersion (assembly : Assembly) attributes (md : ProjectCoreInfo) = 
    let informational = 
        attributes |> Seq.tryPick (function 
                          | InformationalVersion v -> Some v
                          | _ -> None)
    
    let normal = 
        let fromAss = 
            match assembly.GetName().Version with
            | null -> None
            | v -> SemVer.Parse(v.ToString()) |> Some
        
        let fromAtt = 
            attributes |> Seq.tryPick (function 
                              | Version v -> Some v
                              | _ -> None)
        
        fromAss ++ fromAtt
    
    { md with Version = informational ++ normal }

let internal getAuthors attributes (md : ProjectCoreInfo) =
    let authors =
        attributes
        |> Seq.tryPick (function Company a -> Some a | _ -> None)
    { md with Authors = authors |> Option.map (fun a -> a.Split(',') |> Array.map (fun s -> s.Trim()) |> List.ofArray) }

let internal getDescription attributes (md : ProjectCoreInfo) =
    let desc =
        attributes
        |> Seq.tryPick (function Description d -> Some d | _ -> None)
    { md with Description = desc }

let internal loadAssemblyMetadata buildConfig (projectFile : ProjectFile) =
    let output =
        Path.Combine(
            projectFile.FileName |> Path.GetDirectoryName,
            projectFile.GetOutputDirectory buildConfig,
            projectFile.GetAssemblyName())
        |> normalizePath
    let bytes = File.ReadAllBytes output
    let assembly = Assembly.Load bytes
    let attribs = assembly.GetCustomAttributes(true)

    ProjectCoreInfo.Empty
    |> getId assembly
    |> getVersion assembly attribs
    |> getAuthors attribs
    |> getDescription attribs

let internal (|Valid|Invalid|) md =
    match md with
    | { ProjectCoreInfo.Id = None }
    | { Version = None }
    | { Authors = None }
    | { Description = None } ->
        Invalid
    | { Id = Some id'
        Version = Some v
        Authors = Some a
        Description = Some d } ->
            Valid { CompleteCoreInfo.Id = id'; Version = v; Authors = a; Description = d }

let internal mergeMetadata template md' =
    match template with
    | { Contents = ProjectInfo (md, opt) } ->
        let completeCore =
            match {
                    Id = md.Id ++ md'.Id
                    Version = md.Version ++ md'.Version
                    Authors = md.Authors ++ md'.Authors
                    Description = md.Description ++ md'.Description
                  } with
            | Invalid -> failwithf "Incomplete mandatory metadata in template file %s (even including assembly attributes)\nTemplate: %A\nAssembly: %A" template.FileName md md'
            | Valid c -> c
        { template with Contents = CompleteInfo (completeCore, opt) }
    | _ -> template

let internal toDep (t : TemplateFile) =
    match t with
    | CompleteTemplate(core, opt) ->
        core.Id, VersionRequirement(Minimum (core.Version), PreReleaseStatus.All)
    | IncompleteTemplate ->
        failwith "You cannot create a dependency on a template file with incomplete metadata."

let internal addDep (t: TemplateFile) (d : string * VersionRequirement) =
    match t with
    | CompleteTemplate(core, opt) ->
        let deps = 
            match opt.Dependencies with
            | Some ds -> Some (d::ds)
            | None -> Some [d]
        { FileName = t.FileName; Contents = CompleteInfo (core, { opt with Dependencies = deps }) }
    | IncompleteTemplate ->
        failwith "You should only try and add dependencies to template files with complete metadata."

let internal toFile config targetDir (p : ProjectFile) =
    let src =
        Path.Combine(
            p.FileName |> Path.GetDirectoryName,
            p.GetOutputDirectory(config),
            p.GetAssemblyName())
    let dest =
        targetDir
    src, dest

let internal addFile (t: TemplateFile) (f : string * string) =
    match t with
    | CompleteTemplate(core, opt) ->
        let files = 
            match opt.Files with
            | Some fs -> Some (f::fs)
            | None -> Some [f]
        { FileName = t.FileName; Contents = CompleteInfo (core, { opt with Files = files }) }
    | IncompleteTemplate ->
        failwith "You should only try and add dependencies to template files with complete metadata."

let internal findDependencies (dependencies : DependenciesFile) config (template : TemplateFile) (project : ProjectFile) (map : Map<string, TemplateFile * ProjectFile>) =
    let targetDir =
        match project.OutputType with
        | ProjectOutputType.Exe ->
            "tools/"
        | ProjectOutputType.Library ->
            sprintf "lib/%s/" (project.GetTargetFramework().ToString())

    let projectDir = project.FileName |> Path.GetDirectoryName
                        
    let deps, files =
        project.GetInterProjectDependencies()
        |> Seq.fold (fun (deps, files) p ->
            match Map.tryFind p.Name map with
            | Some packagedRef ->
                packagedRef::deps, files
            | None ->
                deps, (ProjectFile.Load (Path.Combine (projectDir, p.Path))
                       |> function
                          | Some p -> p 
                          | None -> failwithf "Missing project reference proj file %s" p.Path)::files) ([], [])

    // Add the assembly from this project
    let withOutput =
        project
        |> toFile config targetDir
        |> addFile template

    // If project refs will also be packaged, add dependency
    let withDeps =
        deps
        |> List.map (fst >> toDep)
        |> List.fold addDep withOutput

    // If project refs will not be packaged, add the assembly to the package
    let withDepsAndIncluded =
        files
        |> List.map (toFile config targetDir)
        |> List.fold addFile withDeps

    // Add any paket references
    let referenceFile =
        ProjectFile.FindReferencesFile <| FileInfo project.FileName
        |> Option.map (ReferencesFile.FromFile)

    match referenceFile with
    | Some r ->
        r.NugetPackages
        |> List.map (fun np ->
            np.Name.Id, dependencies.DirectDependencies.[np.Name])
        |> List.fold addDep withDepsAndIncluded
    | None ->
        withDepsAndIncluded

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
                |> mergeMetadata templateFile

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