namespace Paket

open System
open System.Collections.Generic
open System.IO
open Paket.Domain
open Paket.Logging
open Paket.PackageResolver
open Paket.ModuleResolver
open Paket.PackageSources

module LockFileSerializer =
    /// [omit]
    let serializePackages options (resolved : PackageResolution) = 
        let sources = 
            resolved
            |> Seq.map (fun kv ->
                    let package = kv.Value
                    match package.Source with
                    | Nuget source -> source.Url,source.Authentication,package
                    | LocalNuget path -> path,None,package
                )
            |> Seq.groupBy (fun (a,b,_) -> a,b)

        let all = 
            let hasReported = ref false
            [ if options.Strict then yield "REFERENCES: STRICT"
              if options.OmitContent then yield "CONTENT: NONE"
              for (source, _), packages in sources do
                  if not !hasReported then
                    yield "NUGET"
                    hasReported := true

                  yield "  remote: " + source

                  yield "  specs:"
                  for _,_,package in packages |> Seq.sortBy (fun (_,_,p) -> NormalizedPackageName p.Name) do
                      let (PackageName packageName) = package.Name
                      match package.FrameworkRestriction with
                      | None -> yield sprintf "    %s (%s)" packageName (package.Version.ToString())
                      | Some restriction -> yield sprintf "    %s (%s) - %s" packageName (package.Version.ToString()) (restriction.ToString())
                      for (PackageName name),v,restriction in package.Dependencies do
                          match restriction with
                          | None -> yield sprintf "      %s (%s)" name (v.ToString())
                          | Some restriction -> yield sprintf "      %s (%s) - %s" name (v.ToString()) (restriction.ToString())]
    
        String.Join(Environment.NewLine, all)

    let serializeSourceFiles (files:ResolvedSourceFile list) =    
        let all =
            let updateHasReported = new List<SingleSourceFileOrigin>()

            [ for (owner,project,origin), files in files |> Seq.groupBy(fun f -> f.Owner, f.Project, f.Origin) do
                match origin with
                | GitHubLink -> 
                    if not (updateHasReported.Contains(GitHubLink)) then
                        yield "GITHUB"
                        updateHasReported.Remove (HttpLink "") |> ignore
                        updateHasReported.Remove GistLink |> ignore
                        updateHasReported.Add GitHubLink
                    yield sprintf "  remote: %s/%s" owner project
                    yield "  specs:"
                | GistLink -> 
                    if not (updateHasReported.Contains(GistLink)) then
                        yield "GIST"
                        updateHasReported.Remove GitHubLink |> ignore
                        updateHasReported.Remove (HttpLink "") |> ignore
                        updateHasReported.Add GistLink
                    yield sprintf "  remote: %s/%s" owner project
                    yield "  specs:"
                | HttpLink url ->
                    if not (updateHasReported.Contains(HttpLink(""))) then
                        yield "HTTP"
                        updateHasReported.Remove GitHubLink |> ignore
                        updateHasReported.Remove GistLink |> ignore
                        updateHasReported.Add (HttpLink "")
                    yield sprintf "  remote: " + url
                    yield "  specs:"

                for file in files |> Seq.sortBy (fun f -> f.Owner.ToLower(),f.Project.ToLower(),f.Name.ToLower())  do
                    
                    let path = file.Name.TrimStart '/'
                    match String.IsNullOrEmpty(file.Commit) with
                    | false -> yield sprintf "    %s (%s)" path file.Commit 
                    | true -> yield sprintf "    %s" path 
                    for (PackageName name,v) in file.Dependencies do
                        yield sprintf "      %s (%s)" name (v.ToString())]

        String.Join(Environment.NewLine, all)

module LockFileParser =

    type ParseState =
        { RepositoryType : string option
          RemoteUrl :string option
          Packages : ResolvedPackage list
          SourceFiles : ResolvedSourceFile list
          LastWasPackage : bool
          Options: InstallOptions }
    
    type private InstallOptionCase = StrictCase | OmitContentCase

    let private (|Remote|NugetPackage|NugetDependency|SourceFile|RepositoryType|Blank|InstallOption|) (state, line:string) =
        match (state.RepositoryType, line.Trim()) with
        | _, "HTTP" -> RepositoryType "HTTP"
        | _, "GIST" -> RepositoryType "GIST"
        | _, "NUGET" -> RepositoryType "NUGET"
        | _, "GITHUB" -> RepositoryType "GITHUB"
        | _, _ when String.IsNullOrWhiteSpace line -> Blank
        | _, String.StartsWith "remote:" trimmed -> Remote(trimmed.Trim().Split(' ').[0])
        | _, String.StartsWith "specs:" _ -> Blank
        | _, String.StartsWith "REFERENCES:" trimmed -> InstallOption(StrictCase,trimmed.Trim() = "STRICT")
        | _, String.StartsWith "CONTENT:" trimmed -> InstallOption(OmitContentCase,trimmed.Trim() = "NONE")
        | _, trimmed when line.StartsWith "      " ->
            let parts = trimmed.Split '(' 
            NugetDependency (parts.[0].Trim(),parts.[1].Replace("(", "").Replace(")", "").Trim())
        | Some "NUGET", trimmed -> NugetPackage trimmed
        | Some "GITHUB", trimmed -> SourceFile(GitHubLink, trimmed)
        | Some "GIST", trimmed -> SourceFile(GistLink, trimmed)
        | Some "HTTP", trimmed  -> SourceFile(HttpLink(String.Empty), trimmed)
        | Some _, _ -> failwith "unknown Repository Type."
        | _ -> failwith "unknown lock file format."

    let Parse(lockFileLines) =
        let remove textToRemove (source:string) = source.Replace(textToRemove, "")
        let removeBrackets = remove "(" >> remove ")"
        ({ RepositoryType = None; RemoteUrl = None; Packages = []; SourceFiles = []; Options = InstallOptions.Default; LastWasPackage = false }, lockFileLines)
        ||> Seq.fold(fun state line ->
            match (state, line) with
            | Remote(url) -> { state with RemoteUrl = Some url }
            | Blank -> state
            | InstallOption (StrictCase,mode) -> { state with Options = {state.Options with Strict = mode} }
            | InstallOption (OmitContentCase,omit) -> { state with Options = {state.Options with OmitContent = omit} }
            | RepositoryType repoType -> { state with RepositoryType = Some repoType }
            | NugetPackage details ->
                match state.RemoteUrl with
                | Some remote -> 
                    let parts = details.Split([|" - "|],StringSplitOptions.None)
                    let parts' = parts.[0].Split ' '
                    let version = parts'.[1] |> removeBrackets
                    { state with LastWasPackage = true
                                 Packages = 
                                     { Source = PackageSource.Parse(remote, None)
                                       Name = PackageName parts'.[0]
                                       Dependencies = Set.empty
                                       Unlisted = false
                                       FrameworkRestriction = if parts.Length < 2 then None else FrameworkIdentifier.Extract(parts.[1].Trim())
                                       Version = SemVer.Parse version } :: state.Packages }
                | None -> failwith "no source has been specified."
            | NugetDependency (name, _) ->
                if state.LastWasPackage then                 
                    match state.Packages with
                    | currentPackage :: otherPackages -> 
                        { state with
                                Packages = { currentPackage with
                                                Dependencies = Set.add (PackageName name, VersionRequirement.AllReleases, None) currentPackage.Dependencies
                                            } :: otherPackages }                    
                    | [] -> failwith "cannot set a dependency - no package has been specified."
                else
                    match state.SourceFiles with
                    | currentFile :: rest -> 
                        { state with
                                SourceFiles = 
                                    { currentFile with
                                                Dependencies = Set.add (PackageName name, VersionRequirement.AllReleases) currentFile.Dependencies
                                            } :: rest }                    
                    | [] -> failwith "cannot set a dependency - no remote file has been specified."
            | SourceFile(origin, details) ->
                match origin with
                | GitHubLink | GistLink ->
                    match state.RemoteUrl |> Option.map(fun s -> s.Split '/') with
                    | Some [| owner; project |] ->
                        let path, commit = match details.Split ' ' with
                                            | [| filePath; commit |] -> filePath, commit |> removeBrackets                                       
                                            | _ -> failwith "invalid file source details."
                        { state with  
                            LastWasPackage = false
                            SourceFiles = { Commit = commit
                                            Owner = owner
                                            Origin = origin
                                            Project = project
                                            Dependencies = Set.empty
                                            Name = path } :: state.SourceFiles }
                    | _ -> failwith "invalid remote details."
                | HttpLink x ->
                    match state.RemoteUrl |> Option.map(fun s -> s.Split '/') with
                    | Some [| protocol; _; domain; |] ->
                        { state with  
                            LastWasPackage = false
                            SourceFiles = { Commit = String.Empty
                                            Owner = domain
                                            Origin = origin
                                            Project = domain
                                            Dependencies = Set.empty
                                            Name = details } :: state.SourceFiles }
                    | Some [| protocol; _; domain; project |] ->
                        { state with  
                            LastWasPackage = false
                            SourceFiles = { Commit = String.Empty
                                            Owner = domain
                                            Origin = origin
                                            Project = project
                                            Dependencies = Set.empty
                                            Name = details } :: state.SourceFiles }
                    | Some [| protocol; _; domain; project; moredetails |] ->
                        { state with  
                            LastWasPackage = false
                            SourceFiles = { Commit = String.Empty
                                            Owner = domain
                                            Origin = origin
                                            Project = project+"/"+moredetails
                                            Dependencies = Set.empty
                                            Name = details } :: state.SourceFiles }
                    | _ ->  failwith "invalid remote details."
            )



/// Allows to parse and analyze paket.lock files.
type LockFile(fileName:string,options,resolution:PackageResolution,remoteFiles:ResolvedSourceFile list) =
    let lowerCaseResolution =
        resolution
        |> Map.fold (fun resolution name p -> Map.add name p resolution) Map.empty

    member __.SourceFiles = remoteFiles
    member __.ResolvedPackages = resolution
    member __.FileName = fileName
    member __.Options = options

    /// Updates the Lock file with the analyzed dependencies from the paket.dependencies file.
    member __.Save() =
        let output = 
            String.Join
                (Environment.NewLine,                  
                    LockFileSerializer.serializePackages options resolution, 
                    LockFileSerializer.serializeSourceFiles remoteFiles)
        File.WriteAllText(fileName, output)
        tracefn "Locked version resolutions written to %s" fileName

    /// Creates a paket.lock file at given location
    static member Create (lockFileName: string, installOptions: InstallOptions, resolvedPackages: PackageResolver.ResolvedPackages, resolvedSourceFiles: ModuleResolver.ResolvedSourceFile list) : LockFile =
        let resolvedPackages = resolvedPackages.GetModelOrFail()
        let lockFile = LockFile(lockFileName, installOptions, resolvedPackages, resolvedSourceFiles)
        lockFile.Save()
        lockFile

    /// Parses a paket.lock file from lines
    static member LoadFrom(lockFileName) : LockFile =        
        LockFileParser.Parse(File.ReadAllLines lockFileName)
        |> fun state -> LockFile(lockFileName, state.Options ,state.Packages |> Seq.fold (fun map p -> Map.add (NormalizedPackageName p.Name) p map) Map.empty, List.rev state.SourceFiles)

    member this.GetPackageHull(referencesFile:ReferencesFile) =
        let usedPackages = HashSet<_>()

        let rec addPackage directly (packageName:PackageName) =
            let identity = NormalizedPackageName packageName
            match lowerCaseResolution.TryFind identity with
            | Some package ->
                if usedPackages.Add packageName then
                    if not this.Options.Strict then
                        for d,_,_ in package.Dependencies do
                            addPackage false d
            | None ->
                failwithf "%s references package %O, but it was not found in the paket.lock file." referencesFile.FileName packageName

        referencesFile.NugetPackages
        |> List.iter (addPackage true)

        usedPackages    