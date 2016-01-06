/// Contains logic which helps to resolve the dependency graph for modules
module Paket.ModuleResolver

open System
open System.IO
open Paket.Domain
open Paket.Requirements

type Origin = 
| GitHubLink 
| GistLink
| GitLink of string
| HttpLink of string

// Represents details on a dependent source.
type UnresolvedSource =
    { Owner : string
      Project : string
      Name : string
      Origin : Origin
      Commit : string option
      Command: string option
      PackagePath: string option
      AuthKey : string option }

    override this.ToString() =
        let name = if this.Name = Constants.FullProjectSourceFileName then "" else " " + this.Name
        match this.Origin with
        | HttpLink url -> sprintf "http %s%s %s" url (defaultArg this.Commit "") this.Name
        | GitLink url -> url
        | _ ->
            let link = 
                match this.Origin with
                | GitHubLink -> "github"
                | GistLink -> "gist"
                | _ -> failwithf "invalid linktype %A" this.Origin

            match this.Commit with
            | Some commit -> sprintf "%s %s/%s:%s%s" link this.Owner this.Project commit name
            | None -> sprintf "%s %s/%s%s" link this.Owner this.Project name

    member this.GetCloneUrl() =
        match this.Origin with
        | GitLink url -> url
        | _ -> failwithf "invalid linktype %A" this.Origin

type ResolvedSourceFile = 
    { Owner : string
      Project : string
      Name : string
      Commit : string
      Dependencies : Set<PackageName * VersionRequirement>
      Origin : Origin
      Command: string option
      PackagePath: string option
      AuthKey : string option }

    member this.FilePath(root,groupName) = this.ComputeFilePath(root,groupName,this.Name)
    
    member this.ComputeFilePath(root,groupName:GroupName,name : string) = 
        let path = normalizePath (name.TrimStart('/'))
        let owner = if String.IsNullOrWhiteSpace this.Owner then "localhost" else this.Owner
        let dir = 
            if groupName = Constants.MainDependencyGroup then
                Path.Combine(root,Constants.PaketFilesFolderName, owner, this.Project, path)
            else
                Path.Combine(root,Constants.PaketFilesFolderName, groupName.GetCompareString(), owner, this.Project, path)
        let di = DirectoryInfo(dir)
        di.FullName
    
    override this.ToString() = sprintf "%s/%s:%s %s" this.Owner this.Project this.Commit this.Name

let private getCommit (file : UnresolvedSource) = defaultArg file.Commit "master"

let resolve getDependencies getSha1 (file : UnresolvedSource) : ResolvedSourceFile = 
    let sha =
        let commit = getCommit file
        match file.Origin with
        | Origin.HttpLink _  -> commit
        | _ -> getSha1 file.Origin file.Owner file.Project commit file.AuthKey
    
    let resolved = 
        { Commit = sha
          Owner = file.Owner
          Origin = file.Origin
          Project = file.Project
          Dependencies = Set.empty
          Name = file.Name
          Command = None
          PackagePath = None
          AuthKey = file.AuthKey  }
    
    let dependencies = 
        getDependencies resolved 
        |> List.map (fun (package:PackageRequirement) -> package.Name, package.VersionRequirement)
        |> Set.ofList

    { resolved with Dependencies = dependencies }

let private detectConflicts (remoteFiles : UnresolvedSource list) : unit =
    let conflicts =
        remoteFiles
        |> List.groupBy (fun file ->
            let directoryName =
                normalizePath (file.Name.TrimStart('/'))
            file.Owner, file.Project, directoryName)
        |> List.map (fun (key, files) -> key, files |> List.map getCommit |> List.distinct)
        |> List.filter (snd >> Seq.length >> (<) 1)
        |> List.map (fun ((owner, project, directoryName), commits) ->
            sprintf "   - %s/%s%s%s     Versions:%s     - %s" owner project directoryName
                Environment.NewLine Environment.NewLine
                (String.concat (Environment.NewLine + "     - ") commits))
        |> String.concat Environment.NewLine

    if conflicts <> "" then
        failwithf "Found conflicting source file requirements:%s%s%s   Currently multiple versions for same source directory are not supported.%s   Please adjust the dependencies file."
            Environment.NewLine conflicts Environment.NewLine Environment.NewLine

// TODO: github has a rate limit - try to convince them to whitelist Paket
let Resolve(getDependencies, getSha1, remoteFiles : UnresolvedSource list) : ResolvedSourceFile list = 
    detectConflicts remoteFiles

    remoteFiles |> List.map (resolve getDependencies getSha1)