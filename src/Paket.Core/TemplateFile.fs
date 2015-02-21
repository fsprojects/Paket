namespace Paket

open Paket

type CompleteCoreInfo = 
    { Id : string
      Version : SemVerInfo
      Authors : string list
      Description : string }
    member this.PackageFileName = sprintf "%s.%O.nupkg" this.Id this.Version
    member this.NuspecFileName = sprintf "/%s.%O.nuspec" this.Id this.Version

type ProjectCoreInfo = 
    { Id : string option
      Version : SemVerInfo option
      Authors : string list option
      Description : string option }
    static member Empty = 
        { Id = None
          Authors = None
          Version = None
          Description = None }

type OptionalPackagingInfo = 
    { Title : string option
      Owners : string list option
      ReleaseNotes : string option
      Summary : string option
      Language : string option
      ProjectUrl : string option
      IconUrl : string option
      LicenseUrl : string option
      Copyright : string option
      RequireLicenseAcceptance : string option
      Tags : string list option
      DevelopmentDependency : bool option
      Dependencies : (string * VersionRequirement) list
      Files : (string * string) list }
    static member Epmty : OptionalPackagingInfo = 
        { Title = None
          Owners = None
          ReleaseNotes = None
          Summary = None
          Language = None
          ProjectUrl = None
          LicenseUrl = None
          IconUrl = None
          Copyright = None
          RequireLicenseAcceptance = None
          Tags = None
          DevelopmentDependency = None
          Dependencies = []
          Files = [] }

type CompleteInfo = CompleteCoreInfo * OptionalPackagingInfo

type TemplateFileContents = 
    | CompleteInfo of CompleteInfo
    | ProjectInfo of ProjectCoreInfo * OptionalPackagingInfo

type TemplateFile = 
    { FileName : string
      Contents : TemplateFileContents }

[<CompilationRepresentationAttribute(CompilationRepresentationFlags.ModuleSuffix)>]
module TemplateFile = 
    open System
    open System.IO
    open System.Text.RegularExpressions
    open Paket.Rop
    open Paket.Domain
    
    let setVersion version templateFile = 
        let contents = 
            match templateFile.Contents with
            | CompleteInfo(core, optional) -> CompleteInfo({ core with Version = version }, optional)
            | ProjectInfo(core, optional) -> ProjectInfo({ core with Version = Some version }, optional)
        { templateFile with Contents = contents }
    
    let setReleaseNotes releaseNotes templateFile = 
        let contents = 
            match templateFile.Contents with
            | CompleteInfo(core, optional) -> CompleteInfo(core, { optional with ReleaseNotes = releaseNotes })
            | ProjectInfo(core, optional) -> ProjectInfo(core, { optional with ReleaseNotes = releaseNotes })
        { templateFile with Contents = contents }
    
    let private basicOperators = [ "~>"; "=="; "<="; ">="; "="; ">"; "<" ]
    let private operators = basicOperators @ (basicOperators |> List.map (fun o -> "!" + o))
    
    let private (!<) prefix lines = 
        let singleLine str = 
            let regex = sprintf "^%s (?<%s>.*)" prefix prefix
            let reg = Regex(regex, RegexOptions.Compiled ||| RegexOptions.CultureInvariant ||| RegexOptions.IgnoreCase)
            if reg.IsMatch str then Some <| (reg.Match str).Groups.[prefix].Value
            else None
        
        let multiLine lines = 
            let rec findBody acc (lines : string list) = 
                match lines with
                | h :: t when h.StartsWith " " -> findBody (h.Trim() :: acc) t
                | _ -> 
                    Some(acc
                         |> List.rev
                         |> String.concat "\n")
            
            let rec findStart lines = 
                match (lines : String list) with
                | h :: t when h.ToLowerInvariant() = prefix.ToLowerInvariant() -> findBody [] t
                | h :: t -> findStart t
                | [] -> None
            
            findStart lines
        
        [ lines |> List.tryPick singleLine
          multiLine lines ]
        |> List.tryPick id
    
    let private failP str = fail <| PackagingConfigParseError str
    
    type private PackageConfigType = 
        | FileType
        | ProjectType
    
    let private parsePackageConfigType contents = 
        match contents with
        | firstLine :: _ -> 
            let t' = (!<) "type" [ firstLine ]
            t' |> function 
            | Some s -> 
                match s with
                | "file" -> succeed FileType
                | "project" -> succeed ProjectType
                | s -> failP (sprintf "Unknown package config type.")
            | None -> failP (sprintf "First line of paket.package file had no 'type' declaration.")
        | [] -> failP "Empty paket.packaging file."
    
    let private getId lines = 
        (!<) "id" lines |> function 
        | Some m -> succeed <| m
        | None -> failP "No id line in paket.packaging file."
    
    let private getVersion lines = 
        (!<) "version" lines |> function 
        | Some m -> 
            let versionString = m
            succeed <| SemVer.Parse versionString
        | None -> failP "No version line in paket.packaging file."
    
    let private getAuthors lines = 
        (!<) "authors" lines |> function 
        | Some m -> 
            m.Split ','
            |> Array.map (fun s -> s.Trim())
            |> List.ofArray
            |> succeed
        | None -> failP "No authors line in paket.packaging file."
    
    let private getDescription lines = 
        (!<) "description" lines |> function 
        | Some m -> succeed m
        | None -> failP "No description line in paket.packaging file."
    
    let private getDependencies lines = 
        (!<) "dependencies" lines
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
    
    let private getFiles lines = 
        (!<) "files" lines
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
    
    let private getOptionalInfo configLines = 
        let title = (!<) "title" configLines
        
        let owners = 
            (!<) "owners" configLines |> Option.map (fun o -> 
                                             o.Split(',')
                                             |> Array.map (fun o -> o.Trim())
                                             |> Array.toList)
        
        let releaseNotes = (!<) "releaseNotes" configLines
        let summary = (!<) "summary" configLines
        let language = (!<) "language" configLines
        let projectUrl = (!<) "projectUrl" configLines
        let iconUrl = (!<) "iconUrl" configLines
        let licenseUrl = (!<) "licenseUrl" configLines
        let copyright = (!<) "copyright" configLines
        let requireLicenseAcceptance = (!<) "requireLicenseAcceptance" configLines
        
        let tags = 
            (!<) "tags" configLines |> Option.map (fun t -> 
                                           t.Split ' '
                                           |> Array.map (fun t -> t.Trim())
                                           |> Array.toList)
        
        let developmentDependency = (!<) "developmentDependency" configLines |> Option.map Boolean.Parse
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
          Dependencies = getDependencies configLines
          Files = getFiles configLines }
    
    let Parse(contentStream : Stream) = 
        rop { 
            let configLines = 
                use sr = new StreamReader(contentStream, System.Text.Encoding.UTF8)
                
                let rec inner (s : StreamReader) = 
                    seq { 
                        let line = s.ReadLine()
                        if line <> null then 
                            yield line
                            yield! inner s
                    }
                inner sr |> Seq.toList
            let! type' = parsePackageConfigType configLines
            match type' with
            | ProjectType -> 
                let core : ProjectCoreInfo = 
                    { Id = (!<) "id" configLines
                      Version = (!<) "version" configLines |> Option.map SemVer.Parse
                      Authors = 
                          (!<) "authors" configLines |> Option.map (fun s -> 
                                                            s.Split(',')
                                                            |> Array.map (fun s -> s.Trim())
                                                            |> Array.toList)
                      Description = (!<) "description" configLines }
                
                let optionalInfo = getOptionalInfo configLines
                return ProjectInfo(core, optionalInfo)
            | FileType -> 
                let! id' = getId configLines
                let! version = getVersion configLines
                let! authors = getAuthors configLines
                let! description = getDescription configLines
                let core : CompleteCoreInfo = 
                    { Id = id'
                      Version = version
                      Authors = authors
                      Description = description }
                
                let optionalInfo = getOptionalInfo configLines
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
