namespace Paket

open System
open System.Collections.Generic
open System.IO
open Paket.Domain
open Paket.Logging
open Paket.PackageResolver
open Paket.ModuleResolver
open Paket.PackageSources
open Paket.Requirements

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
              if options.Redirects then yield "REDIRECTS: ON"
              for (source, _), packages in sources do
                  if not !hasReported then
                    yield "NUGET"
                    hasReported := true

                  yield "  remote: " + source

                  yield "  specs:"
                  for _,_,package in packages |> Seq.sortBy (fun (_,_,p) -> NormalizedPackageName p.Name) do
                      let (PackageName packageName) = package.Name

                      let restrictions =
                        package.FrameworkRestrictions
                        |> List.map (fun restriction ->
                            match restriction with
                            | FrameworkRestriction.Exactly r -> r.ToString()
                            | FrameworkRestriction.AtLeast r -> ">= " + r.ToString()
                            | FrameworkRestriction.Between(min,max) -> sprintf ">= %s < %s" (min.ToString()) (max.ToString()))
                      
                      let versionStr = 
                          let s = package.Version.ToString()
                          if s = "" then s else "(" + s + ")"

                      match restrictions with
                      | [] -> yield sprintf "    %s %s" packageName versionStr
                      | _  -> yield sprintf "    %s %s - %s" packageName versionStr (String.Join(", ",restrictions))

                      for (PackageName name),v,restrictions in package.Dependencies do
                          let restrictions =
                            restrictions
                            |> List.map (fun restriction ->
                                match restriction with
                                | FrameworkRestriction.Exactly r -> r.ToString()
                                | FrameworkRestriction.AtLeast r -> ">= " + r.ToString()
                                | FrameworkRestriction.Between(min,max) -> sprintf ">= %s < %s" (min.ToString()) (max.ToString()))

                          
                          let versionStr = 
                              let s = v.ToString()
                              if s = "" then s else "(" + s + ")"

                          match restrictions with
                          | [] -> yield sprintf "      %s %s" name versionStr
                          | _  -> yield sprintf "      %s %s - %s" name versionStr (String.Join(", ",restrictions))]
    
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
                        let versionStr = 
                            let s = v.ToString()
                            if s = "" then s else "(" + s + ")"
                        yield sprintf "      %s %s" name versionStr]

        String.Join(Environment.NewLine, all |> List.map (fun s -> s.TrimEnd()))

module LockFileParser =

    type ParseState =
        { RepositoryType : string option
          RemoteUrl :string option
          Packages : ResolvedPackage list
          SourceFiles : ResolvedSourceFile list
          LastWasPackage : bool
          Options: InstallOptions }

    type private ParserOption =
    | ReferencesMode of bool
    | OmitContent of bool
    | Redirects of bool

    let private (|Remote|NugetPackage|NugetDependency|SourceFile|RepositoryType|Blank|InstallOption|) (state, line:string) =
        match (state.RepositoryType, line.Trim()) with
        | _, "HTTP" -> RepositoryType "HTTP"
        | _, "GIST" -> RepositoryType "GIST"
        | _, "NUGET" -> RepositoryType "NUGET"
        | _, "GITHUB" -> RepositoryType "GITHUB"
        | _, _ when String.IsNullOrWhiteSpace line -> Blank
        | _, String.StartsWith "remote:" trimmed -> Remote(trimmed.Trim().Split(' ').[0])
        | _, String.StartsWith "specs:" _ -> Blank
        | _, String.StartsWith "REFERENCES:" trimmed -> InstallOption(ReferencesMode(trimmed.Trim() = "STRICT"))
        | _, String.StartsWith "REDIRECTS:" trimmed -> InstallOption(Redirects(trimmed.Trim() = "ON"))
        | _, String.StartsWith "CONTENT:" trimmed -> InstallOption(OmitContent(trimmed.Trim() = "NONE"))
        | _, trimmed when line.StartsWith "      " ->
            if trimmed.Contains("(") then
                let parts = trimmed.Split '(' 
                NugetDependency (parts.[0].Trim(),parts.[1].Replace("(", "").Replace(")", "").Trim())
            else
                NugetDependency (trimmed,">= 0")                
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
            | InstallOption (ReferencesMode(mode)) -> { state with Options = {state.Options with Strict = mode} }
            | InstallOption (Redirects(mode)) -> { state with Options = {state.Options with Redirects = mode} }
            | InstallOption (OmitContent(omit)) -> { state with Options = {state.Options with OmitContent = omit} }
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
                                       FrameworkRestrictions = 
                                            if parts.Length < 2 then 
                                                [] 
                                            else
                                                let commaSplit = parts.[1].Trim().Split(',')
                                                [for p in commaSplit do
                                                    let operatorSplit = p.Trim().Split(' ')
                                                    let framework =
                                                        if operatorSplit.Length < 2 then 
                                                           operatorSplit.[0] 
                                                        else 
                                                           operatorSplit.[1]
                                                    match FrameworkDetection.Extract(framework) with
                                                    | None -> ()
                                                    | Some x -> 
                                                        if operatorSplit.[0] = ">=" then
                                                            if operatorSplit.Length < 4 then
                                                                yield FrameworkRestriction.AtLeast x
                                                            else
                                                                match FrameworkDetection.Extract(operatorSplit.[3]) with
                                                                | None -> ()
                                                                | Some y -> yield FrameworkRestriction.Between(x,y)
                                                        else
                                                            yield FrameworkRestriction.Exactly x]

                                       Version = SemVer.Parse version } :: state.Packages }
                | None -> failwith "no source has been specified."
            | NugetDependency (name, _) ->
                if state.LastWasPackage then                 
                    match state.Packages with
                    | currentPackage :: otherPackages -> 
                        { state with
                                Packages = { currentPackage with
                                                Dependencies = Set.add (PackageName name, VersionRequirement.AllReleases, []) currentPackage.Dependencies
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
                                            Origin = HttpLink(state.RemoteUrl.Value)
                                            Project = domain
                                            Dependencies = Set.empty
                                            Name = details } :: state.SourceFiles }
                    | Some [| protocol; _; domain; project |] ->
                        { state with  
                            LastWasPackage = false
                            SourceFiles = { Commit = String.Empty
                                            Owner = domain
                                            Origin = HttpLink(state.RemoteUrl.Value)
                                            Project = project
                                            Dependencies = Set.empty
                                            Name = details } :: state.SourceFiles }
                    | Some [| protocol; _; domain; project; moredetails |] ->
                        { state with  
                            LastWasPackage = false
                            SourceFiles = { Commit = String.Empty
                                            Owner = domain
                                            Origin = HttpLink(state.RemoteUrl.Value)
                                            Project = project+"/"+moredetails
                                            Dependencies = Set.empty
                                            Name = details } :: state.SourceFiles }
                    | _ ->  failwith "invalid remote details."
            )



/// Allows to parse and analyze paket.lock files.
type LockFile(fileName:string,options,resolution:PackageResolution,remoteFiles:ResolvedSourceFile list) =

    member __.SourceFiles = remoteFiles
    member __.ResolvedPackages = resolution
    member __.FileName = fileName
    member __.Options = options

    /// Gets all dependencies of the given package
    member this.GetAllNormalizedDependenciesOf(package:NormalizedPackageName) = 
        let usedPackages = HashSet<_>()

        let rec addPackage (identity:NormalizedPackageName) =
            match resolution.TryFind identity with
            | Some package ->
                if usedPackages.Add identity then
                    if not this.Options.Strict then
                        for d,_,_ in package.Dependencies do
                            addPackage(NormalizedPackageName d)
            | None ->
                failwithf "A package was referenced, but it was not found in the paket.lock file." 

        addPackage package

        usedPackages

    /// Gets all dependencies of the given package
    member this.GetAllDependenciesOf(package) = 
        let usedPackages = HashSet<_>()

        let rec addPackage (packageName:PackageName) =
            let identity = NormalizedPackageName packageName
            match resolution.TryFind identity with
            | Some package ->
                if usedPackages.Add packageName then
                    if not this.Options.Strict then
                        for d,_,_ in package.Dependencies do
                            addPackage d
            | None ->
                let (PackageName name) = packageName
                failwithf "Package %s was referenced, but it was not found in the paket.lock file." name

        addPackage package

        usedPackages

    member this.GetAllNormalizedDependenciesOf(package:PackageName) = 
        this.GetAllDependenciesOf(package)
        |> Seq.map NormalizedPackageName
        |> Set.ofSeq

    member this.GetIndirectDependencies() = 
        this.ResolvedPackages 
        |> Seq.map (fun d -> d.Value.Dependencies |> Seq.map (fun (n,_,_) -> n))
        |> Seq.concat

    member this.GetTopLevelDependencies() = 
        let indirect = 
            this.GetIndirectDependencies() 
            |> Seq.map NormalizedPackageName 
            |> Set.ofSeq

        this.ResolvedPackages
        |> Map.filter (fun name _ -> indirect.Contains name |> not)

    /// Checks if the first package is a dependency of the second package
    member this.IsDependencyOf(dependentPackage,package) =
        this.GetAllDependenciesOf(package).Contains dependentPackage

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

    /// Parses a paket.lock file from file
    static member LoadFrom(lockFileName) : LockFile =        
        LockFile.Parse(lockFileName, File.ReadAllLines lockFileName)

    /// Parses a paket.lock file from lines
    static member Parse(lockFileName,lines) : LockFile =        
        LockFileParser.Parse lines
        |> fun state -> LockFile(lockFileName, state.Options ,state.Packages |> Seq.fold (fun map p -> Map.add (NormalizedPackageName p.Name) p map) Map.empty, List.rev state.SourceFiles)

    member this.GetPackageHull(referencesFile:ReferencesFile) =
        let usedPackages = HashSet<_>()

        referencesFile.NugetPackages
        |> List.iter (fun package -> 
            try
                usedPackages.UnionWith(this.GetAllDependenciesOf(package))
            with exn -> failwithf "%s - in %s" exn.Message referencesFile.FileName)

        usedPackages   
