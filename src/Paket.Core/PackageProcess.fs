module Paket.PackageProcess

open Paket
open System
open System.IO
open System.Reflection
open Paket.Domain
open Paket.Logging

let internal (|Complete|Incomplete|) templateFile =
    match templateFile with
    | { Contents = (CompleteInfo (core, optional)) } ->
        Complete (core, optional)
    | _ ->
        Incomplete

let internal pack outputPath templateFile =
    match templateFile with
    | Complete (core, optional) ->
        NupkgWriter.Write core optional (Path.GetDirectoryName templateFile.FileName) outputPath
    | Incomplete ->
        failwithf "There was an attempt to pack incomplete template file %s" templateFile.FileName

let (|Title|Description|Version|InformationalVersion|Company|Ignore|) (att : obj) =
    match att with
    | :? AssemblyTitleAttribute as title ->
        Title title.Title
    | :? AssemblyDescriptionAttribute as description ->
        Description description.Description
    | :? AssemblyVersionAttribute as version ->
        Version (version.Version |> SemVer.Parse)
    | :? AssemblyInformationalVersionAttribute as version ->
        InformationalVersion (version.InformationalVersion |> SemVer.Parse)
    | :? AssemblyCompanyAttribute as company ->
        Company company.Company
    | _ ->
        Ignore

let internal emptyMetadata =
    {
        Id = None
        Authors = None
        Version = None
        Description = None
    }

let internal getId (assembly : Assembly) (md : ProjectCoreInfo) =
    { md with Id = Some <| assembly.GetName().Name }

let internal (++) opt opt' =
    match opt with
    | Some v -> Some v
    | None -> opt'

let internal getVersion (ass : Assembly) attributes (md : ProjectCoreInfo) =
    let informational =
        attributes
        |> Seq.tryPick (function InformationalVersion v -> Some v | _ -> None)
    let normal =
        let fromAss =
            match ass.GetName().Version with
            | null -> None
            | v -> SemVer.Parse (v.ToString()) |> Some
        let fromAtt =
            attributes
            |> Seq.tryPick (function Version v -> Some v | _ -> None)
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

    emptyMetadata
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
    | Complete (core, opt) ->
        core.Id, VersionRequirement(Minimum (core.Version), PreReleaseStatus.All)
    | Incomplete ->
        failwith "You cannot create a dependency on a template file with incomplete metadata."

let internal addDep (t: TemplateFile) (d : string * VersionRequirement) =
    match t with
    | Complete (core, opt) ->
        let deps = 
            match opt.Dependencies with
            | Some ds -> Some (d::ds)
            | None -> Some [d]
        { FileName = t.FileName; Contents = CompleteInfo (core, { opt with Dependencies = deps }) }
    | Incomplete ->
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
    | Complete (core, opt) ->
        let files = 
            match opt.Files with
            | Some fs -> Some (f::fs)
            | None -> Some [f]
        { FileName = t.FileName; Contents = CompleteInfo (core, { opt with Files = files }) }
    | Incomplete ->
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

let Pack(dependencies : DependenciesFile, buildConfig, packageOutputPath) =
    Utils.createDir packageOutputPath |> Rop.returnOrFail
    let rootPath = dependencies.FileName |> Path.GetDirectoryName
    let templates = TemplateFile.FindTemplateFiles rootPath |> Seq.map TemplateFile.Load
    let complete, incomplete =
        templates
        |> List.ofSeq
        |> List.partition (function Complete _ -> true | Incomplete -> false)

    // load up project files and grab meta data
    let projectTemplates =
        ProjectFile.FindAllProjects rootPath
        |> Array.choose (fun p ->
            match ProjectFile.FindTemplatesFile(FileInfo(p.FileName)) with
            | None -> None
            | Some fileName ->
                let templatesFile = TemplateFile.Load fileName
                match templatesFile with
                | Complete _ -> None
                | Incomplete -> Some(templatesFile,p))
        |> Array.map (fun (t, p) ->
            mergeMetadata t (loadAssemblyMetadata buildConfig p), p)
        |> Array.map (fun (t, p) ->
            (match t.Contents with CompleteInfo (c, _) -> c.Id | x -> failwithf "unexpected failure: %A" x), (t, p))
        |> Map.ofArray

    // add dependencies
    let projectTemplatesWithDeps =
        projectTemplates
        |> Map.map (fun _ (t, p) -> findDependencies dependencies buildConfig t p projectTemplates)
        |> Map.toList
        |> List.map snd

    // Package all templates
    projectTemplatesWithDeps @ complete
    |> Array.ofList
    |> Array.map (fun t -> async {pack packageOutputPath t })
    |> Async.Parallel
    |> Async.Ignore
    |> Async.RunSynchronously

    [complete;projectTemplatesWithDeps]
    |> List.concat
    |> List.iter (fun t -> verbosefn "Packed: %s" t.FileName)
