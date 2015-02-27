namespace Paket

open Paket
open System
open System.IO
open System.Text.RegularExpressions
open Chessie.ErrorHandling
open Paket.Domain

module private TemplateParser =
    type private ParserState =
        {
            Remaining : string list
            Map : Map<string, string>
            Line : int
        }

    let private single = Regex(@"^(\S+)\s*$", RegexOptions.Compiled)
    let private multi = Regex(@"^(\S+)\s+(\S.*)", RegexOptions.Compiled)

    let private (!!) (i : int) (m : Match) =
        m.Groups.[i].Value.Trim().ToLowerInvariant()

    let private (|SingleToken|_|) line =
        let m = single.Match line
        match m.Success with
        | true -> Some (!! 1 m)
        | false -> None

    let private (|MultiToken|_|) line =
        let m = multi.Match line
        match m.Success with
        | true ->
            Some (!! 1 m, m.Groups.[2].Value.Trim())
        | false -> None

    let private indented = Regex(@"^\s+(.*)", RegexOptions.Compiled)
    let private (|Indented|_|) line =
        let i = indented.Match line
        match i.Success with
        | true -> i.Groups.[1].Value.Trim() |> Some
        | false -> None

    let rec private indentedBlock acc i lines =
        match lines with
        | (Indented h)::t ->
            indentedBlock (h::acc) (i + 1) t
        | _ -> acc |> List.rev |> String.concat "\n", i, lines

    let rec private inner state =
        match state with
        | { Remaining = [] } -> Choice1Of2 state.Map
        | { Remaining = h::t } ->
            match h with
            | Indented _ -> Choice2Of2 <| sprintf "Indented block with no name line %d" state.Line
            | MultiToken (key, value) ->
                inner { state with
                            Remaining = t
                            Map = Map.add key value state.Map
                            Line = state.Line + 1 }
            | SingleToken key ->
                let value, line, remaining = indentedBlock [] state.Line t
                if value = "" then
                    Choice2Of2 <| sprintf "No indented block following name '%s' line %d" key line
                else
                    inner { state with
                                Remaining = remaining 
                                Map = Map.add key value state.Map
                                Line = line }
            | "" ->
                inner { state with Line = state.Line + 1; Remaining = t }
            | _ ->
                Choice2Of2 <| sprintf "Invalid syntax line %d" state.Line

    let parse (contents : string) =
        inner {
            Remaining =
                contents.Split('\n')
                |> Array.toList
            Line = 1
            Map = Map.empty
        }
    
type internal CompleteCoreInfo = 
    { Id : string
      Version : SemVerInfo option
      Authors : string list
      Description : string }
    member this.PackageFileName = 
        match this.Version with
        | Some v -> sprintf "%s.%O.nupkg" this.Id v
        | None -> failwithf "No version given for %s" this.Id
    member this.NuspecFileName = this.Id + ".nuspec" 

type internal ProjectCoreInfo = 
    { Id : string option
      Version : SemVerInfo option
      Authors : string list option
      Description : string option }
    static member Empty = 
        { Id = None
          Authors = None
          Version = None
          Description = None }

type internal OptionalPackagingInfo = 
    { Title : string option
      Owners : string list
      ReleaseNotes : string option
      Summary : string option
      Language : string option
      ProjectUrl : string option
      IconUrl : string option
      LicenseUrl : string option
      Copyright : string option
      RequireLicenseAcceptance : bool
      Tags : string list
      DevelopmentDependency : bool
      Dependencies : (string * VersionRequirement) list
      Files : (string * string) list }
    static member Epmty : OptionalPackagingInfo = 
        { Title = None
          Owners = []
          ReleaseNotes = None
          Summary = None
          Language = None
          ProjectUrl = None
          LicenseUrl = None
          IconUrl = None
          Copyright = None
          RequireLicenseAcceptance = false
          Tags = []
          DevelopmentDependency = false
          Dependencies = []
          Files = [] }

type internal CompleteInfo = CompleteCoreInfo * OptionalPackagingInfo

type internal TemplateFileContents = 
    | CompleteInfo of CompleteInfo
    | ProjectInfo of ProjectCoreInfo * OptionalPackagingInfo

type internal TemplateFile = 
    { FileName : string
      Contents : TemplateFileContents }

[<CompilationRepresentationAttribute(CompilationRepresentationFlags.ModuleSuffix)>]
module internal TemplateFile = 
    let setVersion version templateFile = 
        let contents = 
            match templateFile.Contents with
            | CompleteInfo(core, optional) -> CompleteInfo({ core with Version = Some version }, optional)
            | ProjectInfo(core, optional) -> ProjectInfo({ core with Version = Some version }, optional)
        { templateFile with Contents = contents }
    
    let setReleaseNotes releaseNotes templateFile = 
        let contents = 
            match templateFile.Contents with
            | CompleteInfo(core, optional) -> CompleteInfo(core, { optional with ReleaseNotes = Some releaseNotes })
            | ProjectInfo(core, optional) -> ProjectInfo(core, { optional with ReleaseNotes = Some releaseNotes })
        { templateFile with Contents = contents }
    
    let private failP str = fail <| PackagingConfigParseError str
    
    type private PackageConfigType = 
        | FileType
        | ProjectType

    let private parsePackageConfigType map = 
        let t' = Map.tryFind "type" map
        t' |> function 
        | Some s -> 
            match s with
            | "file" -> succeed FileType
            | "project" -> succeed ProjectType
            | s -> failP (sprintf "Unknown package config type.")
        | None -> failP (sprintf "First line of paket.package file had no 'type' declaration.")
    
    let private getId map = 
        Map.tryFind "id" map |> function 
        | Some m -> succeed <| m
        | None -> failP "No id line in paket.template file."
    
    let private getAuthors (map : Map<string, string>) = 
        Map.tryFind "authors" map |> function 
        | Some m -> 
            m.Split ','
            |> Array.map (fun s -> s.Trim())
            |> List.ofArray
            |> succeed
        | None -> failP "No authors line in paket.template file."
    
    let private getDescription map = 
        Map.tryFind "description" map |> function 
        | Some m -> succeed m
        | None -> failP "No description line in paket.template file."
    
    let private getDependencies (map : Map<string, string>) = 
        Map.tryFind "dependencies" map
        |> Option.map (fun d -> d.Split '\n')
        |> Option.map (Array.map (fun d -> 
                           let reg = Regex(@"(?<id>\S+)(?<version>.*)").Match d
                           let id' = reg.Groups.["id"].Value
                           let versionRequirement = 
                               reg.Groups.["version"].Value.Trim() |> DependenciesFileParser.parseVersionRequirement
                           id', versionRequirement))
        |> Option.map Array.toList
        |> fun x -> defaultArg x []
    
    let private fromReg = Regex("from (?<from>.*)", RegexOptions.Compiled)
    let private toReg = Regex("to (?<to>.*)", RegexOptions.Compiled)
    
    let private getFiles (map : Map<string, string>) = 
        Map.tryFind "files" map
        |> Option.map (fun d -> d.Split '\n')
        |> Option.map 
               (Seq.map 
                    (fun (line:string) -> 
                        let splitted = line.Split([|"==>"|],StringSplitOptions.None) |> Array.map (fun s -> s.Trim())
                        if splitted.Length < 2 then
                            splitted.[0],"lib"
                        else
                            splitted.[0],splitted.[1]))
        |> Option.map List.ofSeq
        |> fun x -> defaultArg x []
    
    let private getOptionalInfo (map : Map<string, string>) = 
        let get (n : string) = Map.tryFind (n.ToLowerInvariant()) map

        let title = get "title"
        
        let owners = 
            Map.tryFind "owners" map
            |> Option.map (fun o -> 
                o.Split(',')
                |> Array.map (fun o -> o.Trim())
                |> Array.toList)
            |> fun x -> defaultArg x []
        
        let releaseNotes = get "releaseNotes"
        let summary = get "summary"
        let language = get "language"
        let projectUrl = get "projectUrl"
        let iconUrl = get "iconUrl"
        let licenseUrl = get "licenseUrl"
        let copyright = get "copyright"
        let requireLicenseAcceptance = 
            match get "requireLicenseAcceptance" with
            | Some x when x.ToLower() = "true" -> true
            | _ -> false
        
        let tags = 
            get "tags"
            |> Option.map (fun t -> 
                t.Split ' '
                |> Array.map (fun t -> t.Trim().Trim(','))
                |> Array.toList)
            |> fun x -> defaultArg x []
        
        let developmentDependency = 
            match get "developmentDependency" with
            | Some x when x.ToLower() = "true" -> true
            | _ -> false

        { Title = title
          Owners = owners
          ReleaseNotes = releaseNotes
          Summary = summary
          Language = language
          ProjectUrl = projectUrl
          IconUrl = iconUrl
          LicenseUrl = licenseUrl
          Copyright = copyright
          RequireLicenseAcceptance = requireLicenseAcceptance
          Tags = tags
          DevelopmentDependency = developmentDependency
          Dependencies = getDependencies map
          Files = getFiles map }
    
    let Parse(contentStream : Stream) = 
        attempt { 
            let sr = new StreamReader(contentStream)
            let! map =
                match TemplateParser.parse (sr.ReadToEnd()) with
                | Choice1Of2 m -> succeed m
                | Choice2Of2 f -> failP f
            sr.Dispose()
            let! type' = parsePackageConfigType map
            match type' with
            | ProjectType -> 
                let core : ProjectCoreInfo = 
                    { Id = Map.tryFind "id" map
                      Version = Map.tryFind "version" map |> Option.map SemVer.Parse
                      Authors = 
                          Map.tryFind "authors" map
                          |> Option.map (fun s -> 
                                            s.Split(',')
                                            |> Array.map (fun s -> s.Trim())
                                            |> Array.toList)
                      Description = Map.tryFind "description" map }
                
                let optionalInfo = getOptionalInfo map
                return ProjectInfo(core, optionalInfo)
            | FileType ->                 
                let! id' = getId map                
                let! authors = getAuthors map
                let! description = getDescription map
                let core : CompleteCoreInfo = 
                    { Id = id'
                      Version = Map.tryFind "version" map |> Option.map SemVer.Parse
                      Authors = authors
                      Description = description }
                
                let optionalInfo = getOptionalInfo map
                return CompleteInfo(core, optionalInfo)
        }
    
    let Load filename = 
        let contents = 
            File.OpenRead filename
            |> Parse
            |> returnOrFail
        { FileName = filename
          Contents = contents }
    
    let FindTemplateFiles root = 
        Directory.EnumerateFiles(root, "*" + Constants.TemplateFile, SearchOption.AllDirectories)
