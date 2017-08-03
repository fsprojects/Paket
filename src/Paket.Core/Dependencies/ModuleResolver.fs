/// Contains logic which helps to resolve the dependency graph for modules
module Paket.ModuleResolver

open System
open System.IO
open Pri.LongPath
open Paket.Domain
open Paket.Requirements
open Paket.Git.Handling

type Origin = 
| GitHubLink 
| GistLink
| GitLink of GitLinkOrigin
| HttpLink of string


let internal computeFilePath(owner,project,root,groupName:GroupName,name : string) = 
    let path = normalizePath (name.TrimStart('/'))
    let owner = if String.IsNullOrWhiteSpace owner then "localhost" else owner
    let dir = 
        if groupName = Constants.MainDependencyGroup then
            Path.Combine(root,Constants.PaketFilesFolderName, owner, project, path)
        else
            Path.Combine(root,Constants.PaketFilesFolderName, groupName.CompareString, owner, project, path)
        |> normalizePath
    let di = DirectoryInfo(dir)
    di.FullName

[<RequireQualifiedAccess>]
type VersionRestriction =
| NoVersionRestriction
| Concrete of string
| VersionRequirement of VersionRequirement

// Represents details on a dependent source.
type UnresolvedSource =
    { Owner : string
      Project : string
      Name : string
      Origin : Origin
      Version : VersionRestriction
      Command: string option
      OperatingSystemRestriction: string option
      PackagePath: string option
      AuthKey : string option }

    override this.ToString() =
        let name = if this.Name = Constants.FullProjectSourceFileName then "" else " " + this.Name
        match this.Origin with
        | HttpLink url -> 
            let v =
                match this.Version with
                | VersionRestriction.NoVersionRestriction -> ""
                | VersionRestriction.Concrete x -> x
                | VersionRestriction.VersionRequirement vr -> vr.ToString()

            sprintf "http %s%s %s" url v this.Name
        | GitLink (RemoteGitOrigin url) -> url
        | GitLink (LocalGitOrigin path) -> path
        | _ ->
            let link = 
                match this.Origin with
                | GitHubLink -> "github"
                | GistLink -> "gist"
                | _ -> failwithf "invalid linktype %A" this.Origin

            match this.Version with
            | VersionRestriction.Concrete vr -> sprintf "%s %s/%s:%s%s" link this.Owner this.Project vr name
            | VersionRestriction.VersionRequirement vr -> sprintf "%s %s/%s:%O%s" link this.Owner this.Project vr name
            | VersionRestriction.NoVersionRestriction -> sprintf "%s %s/%s%s" link this.Owner this.Project name

    member this.GetCloneUrl() =
        match this.Origin with
        | GitLink (LocalGitOrigin path) -> path
        | GitLink (RemoteGitOrigin url) -> url
        | _ -> failwithf "invalid linktype %A" this.Origin

    member this.FilePath(root,groupName) = this.ComputeFilePath(root,groupName,this.Name)
    
    member this.ComputeFilePath(root,groupName:GroupName,name : string) = computeFilePath(this.Owner,this.Project,root,groupName,name)

type ResolvedSourceFile = 
    { Owner : string
      Project : string
      Name : string
      Commit : string
      Dependencies : Set<PackageName * VersionRequirement>
      Origin : Origin
      Command: string option
      OperatingSystemRestriction: string option
      PackagePath: string option
      AuthKey : string option }

    member this.FilePath(root,groupName) = this.ComputeFilePath(root,groupName,this.Name)
    
    member this.ComputeFilePath(root,groupName:GroupName,name : string) = computeFilePath(this.Owner,this.Project,root,groupName,name)
    
    override this.ToString() = sprintf "%s/%s:%s %s" this.Owner this.Project this.Commit this.Name

let getVersionRequirement version = 
    match version with
    | VersionRestriction.NoVersionRestriction -> "master"
    | VersionRestriction.Concrete x -> x
    | VersionRestriction.VersionRequirement vr -> vr.ToString()

let resolve getDependencies getSha1 (file : UnresolvedSource) : ResolvedSourceFile list = 
    let rec resolve getDependencies getSha1 (file : UnresolvedSource) : ResolvedSourceFile list = 
        let sha =
            let commit = getVersionRequirement file.Version
            match file.Origin with
            | Origin.HttpLink _  -> commit
            | _ -> getSha1 file.Origin file.Owner file.Project file.Version file.AuthKey
    
        let resolved = 
            { Commit = sha
              Owner = file.Owner
              Origin = file.Origin
              Project = file.Project
              Dependencies = Set.empty
              Name = file.Name
              Command = file.Command
              OperatingSystemRestriction = file.OperatingSystemRestriction
              PackagePath = file.PackagePath
              AuthKey = file.AuthKey  }


        let nugetDependencies,remoteDependencies = getDependencies resolved 
        let dependencies = 
            nugetDependencies
            |> List.map (fun (package:PackageRequirement) -> package.Name, package.VersionRequirement)
            |> Set.ofList

        let recursiveDeps =
            remoteDependencies
            |> List.map (resolve getDependencies getSha1)
            |> List.concat

        { resolved with Dependencies = dependencies } :: recursiveDeps

    let getDependencies resolved =
        let cache = System.Collections.Generic.HashSet<_>()
        if cache.Add resolved then
            getDependencies resolved
        else
            [],[]
        
    resolve getDependencies getSha1 file

let private detectConflicts (remoteFiles : UnresolvedSource list) : unit =
    let conflicts =
        remoteFiles
        |> List.groupBy (fun file ->
            let directoryName =
                normalizePath (file.Name.TrimStart('/'))
            file.Owner, file.Project, directoryName)
        |> List.map (fun (key, files) -> key, files |> List.map (fun x -> getVersionRequirement x.Version) |> List.distinct)
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

    remoteFiles 
    |> List.map (resolve getDependencies getSha1)
    |> List.concat
