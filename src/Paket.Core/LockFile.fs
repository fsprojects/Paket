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
open Chessie.ErrorHandling

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
              if options.Redirects then yield "REDIRECTS: ON"
              if not options.Settings.CopyLocal then yield "COPY-LOCAL: FALSE"   
              if not options.Settings.ImportTargets then yield "IMPORT-TARGETS: FALSE"
              if options.Settings.OmitContent then yield "CONTENT: NONE"      
              match options.Settings.FrameworkRestrictions with
              | [] -> ()
              | _  -> yield "FRAMEWORK: " + (String.Join(", ",options.Settings.FrameworkRestrictions)).ToUpper()
              for (source, _), packages in sources do
                  if not !hasReported then
                    yield "NUGET"
                    hasReported := true

                  yield "  remote: " + String.quoted source

                  yield "  specs:"
                  for _,_,package in packages |> Seq.sortBy (fun (_,_,p) -> NormalizedPackageName p.Name) do
                      let (PackageName packageName) = package.Name
                      
                      let versionStr = 
                          let s = package.Version.ToString()
                          if s = "" then s else "(" + s + ")"

                      let s = package.Settings.ToString()

                      if s = "" then 
                        yield sprintf "    %s %s" packageName versionStr 
                      else
                        yield sprintf "    %s %s - %s" packageName versionStr s

                      for (PackageName name),v,restrictions in package.Dependencies do
                          let versionStr = 
                              let s = v.ToString()
                              if s = "" then s else "(" + s + ")"

                          match restrictions with
                          | [] -> yield sprintf "      %s %s" name versionStr
                          | _  -> yield sprintf "      %s %s - framework: %s" name versionStr (String.Join(", ",restrictions))]
    
        String.Join(Environment.NewLine, all |> List.map (fun s -> s.TrimEnd()))

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
    | ImportTargets of bool
    | FrameworkRestrictions of FrameworkRestrictions
    | CopyLocal of bool
    | Redirects of bool

    let private (|Remote|NugetPackage|NugetDependency|SourceFile|RepositoryType|Blank|InstallOption|) (state, line:string) =
        match (state.RepositoryType, line.Trim()) with
        | _, "HTTP" -> RepositoryType "HTTP"
        | _, "GIST" -> RepositoryType "GIST"
        | _, "NUGET" -> RepositoryType "NUGET"
        | _, "GITHUB" -> RepositoryType "GITHUB"
        | _, _ when String.IsNullOrWhiteSpace line -> Blank
        | _, String.StartsWith "remote:" trimmed -> Remote(PackageSource.Parse("source " + trimmed.Trim()).ToString())
        | _, String.StartsWith "specs:" _ -> Blank
        | _, String.StartsWith "REFERENCES:" trimmed -> InstallOption(ReferencesMode(trimmed.Trim() = "STRICT"))
        | _, String.StartsWith "REDIRECTS:" trimmed -> InstallOption(Redirects(trimmed.Trim() = "ON"))
        | _, String.StartsWith "IMPORT-TARGETS:" trimmed -> InstallOption(ImportTargets(trimmed.Trim() = "TRUE"))
        | _, String.StartsWith "COPY-LOCAL:" trimmed -> InstallOption(CopyLocal(trimmed.Trim() = "TRUE"))
        | _, String.StartsWith "FRAMEWORK:" trimmed -> InstallOption(FrameworkRestrictions(trimmed.Trim() |> Requirements.parseRestrictions))
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
            | InstallOption (ImportTargets(mode)) -> { state with Options = {state.Options with Settings = { state.Options.Settings with ImportTargets = mode} } }
            | InstallOption (CopyLocal(mode)) -> { state with Options = {state.Options with Settings = { state.Options.Settings with CopyLocal = mode}} }
            | InstallOption (FrameworkRestrictions(r)) -> { state with Options = {state.Options with Settings = { state.Options.Settings with FrameworkRestrictions = r}} }
            | InstallOption (OmitContent(omit)) -> { state with Options = {state.Options with Settings = { state.Options.Settings with OmitContent = omit} }}
            | RepositoryType repoType -> { state with RepositoryType = Some repoType }
            | NugetPackage details ->
                match state.RemoteUrl with
                | Some remote -> 
                    let parts = details.Split([|" - "|],StringSplitOptions.None)
                    let parts' = parts.[0].Split ' '
                    let version = parts'.[1] |> removeBrackets
                    let optionsString = 
                        if parts.Length < 2 then "" else 
                        if parts.[1] <> "" && parts.[1].Contains(":") |> not then
                            ("framework: " + parts.[1]) // TODO: This is for backwards-compat and should be removed later
                        else
                            parts.[1]

                    { state with LastWasPackage = true
                                 Packages = 
                                     { Source = PackageSource.Parse(remote, None)
                                       Name = PackageName parts'.[0]
                                       Dependencies = Set.empty
                                       Unlisted = false
                                       Settings = InstallSettings.Parse(optionsString)
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
                    match state.RemoteUrl |> Option.map(fun s -> s.Split '/' |> Array.toList) with
                    | Some [ protocol; _; domain; ] ->
                        let name, path = 
                            match details.Split ' ' with
                            | [| filePath; path |] -> filePath, path |> removeBrackets
                            | _ -> failwith "invalid file source details."

                        let sourceFile =
                            { Commit = path
                              Owner = domain
                              Origin = HttpLink(state.RemoteUrl.Value)
                              Project = ""
                              Dependencies = Set.empty
                              Name = name } 

                        { state with  
                            LastWasPackage = false
                            SourceFiles = sourceFile :: state.SourceFiles }
                    | Some [ protocol; _; domain; project ] ->
                        { state with  
                            LastWasPackage = false
                            SourceFiles = { Commit = String.Empty
                                            Owner = domain
                                            Origin = HttpLink(state.RemoteUrl.Value)
                                            Project = project
                                            Dependencies = Set.empty
                                            Name = details } :: state.SourceFiles }
                    | Some (protocol :: _ :: domain :: project :: moredetails) ->
                        { state with  
                            LastWasPackage = false
                            SourceFiles = { Commit = String.Empty
                                            Owner = domain
                                            Origin = HttpLink(state.RemoteUrl.Value)
                                            Project = project + "/" + String.Join("/",moredetails)
                                            Dependencies = Set.empty
                                            Name = details } :: state.SourceFiles }
                    | _ ->  failwith "invalid remote details.")


/// Allows to parse and analyze paket.lock files.
type LockFile(fileName:string,options:InstallOptions,resolution:PackageResolution,remoteFiles:ResolvedSourceFile list) =

    let dependenciesByPackageLazy = lazy (
        let allDependenciesOf package =
            let usedPackages = HashSet<_>()

            let rec addPackage packageName =
                let identity = NormalizedPackageName packageName
                match resolution.TryFind identity with
                | Some package ->
                    if usedPackages.Add packageName then
                        if not options.Strict then
                            for d,_,_ in package.Dependencies do
                                addPackage d
                | None -> ()

            addPackage package

            usedPackages

        resolution
        |> Map.map (fun _ package -> allDependenciesOf package.Name))

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
        match this.GetAllDependenciesOfSafe package with
        | Some packages -> packages
        | None ->
            let (PackageName name) = package
            failwithf "Package %s was referenced, but it was not found in the paket.lock file." name

    /// Gets all dependencies of the given package
    member this.GetAllDependenciesOfSafe(package) =
        dependenciesByPackageLazy.Value
        |> Map.tryFind (NormalizedPackageName package)

    member this.GetAllNormalizedDependenciesOf(package:PackageName) = 
        this.GetAllDependenciesOf(package)
        |> Seq.map NormalizedPackageName
        |> Set.ofSeq

    member this.GetTransitiveDependencies() =
        let fromNuGets =
            this.ResolvedPackages 
            |> Seq.map (fun d -> d.Value.Dependencies |> Seq.map (fun (n,_,_) -> n))
            |> Seq.concat
            |> Set.ofSeq

        let fromSourceFiles =
            this.SourceFiles 
            |> Seq.map (fun d -> d.Dependencies |> Seq.map fst)
            |> Seq.concat
            |> Set.ofSeq

        Set.union fromNuGets fromSourceFiles

    member this.GetTopLevelDependencies() = 
        let transitive = 
            this.GetTransitiveDependencies() 
            |> Seq.map NormalizedPackageName 
            |> Set.ofSeq

        this.ResolvedPackages
        |> Map.filter (fun name _ -> transitive.Contains name |> not)

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
        |> fun state -> 
            LockFile(
                lockFileName, 
                state.Options,
                state.Packages |> Seq.fold (fun map p -> Map.add (NormalizedPackageName p.Name) p map) Map.empty, 
                List.rev state.SourceFiles)

    member this.GetPackageHull(referencesFile:ReferencesFile) =
        let usedPackages = Dictionary<_,_>()

        for p in referencesFile.NugetPackages do
            if usedPackages.ContainsKey p.Name then
                failwithf "Package %s is referenced more than once in %s" (p.Name.ToString()) referencesFile.FileName
            usedPackages.Add(p.Name,p)

        referencesFile.NugetPackages
        |> List.iter (fun package -> 
            try
                for d in this.GetAllDependenciesOf(package.Name) do
                    if usedPackages.ContainsKey d |> not then
                        usedPackages.Add(d,package)
            with exn -> failwithf "%s - in %s" exn.Message referencesFile.FileName)

        usedPackages

    member this.GetDependencyLookupTable() = 
        this.ResolvedPackages
        |> Map.map (fun name package -> 
                        (this.GetAllDependenciesOf package.Name)
                        |> Set.ofSeq
                        |> Set.remove package.Name)

    member this.GetPackageHullSafe referencesFile =
        referencesFile.NugetPackages
        |> Seq.map (fun package ->
            this.GetAllDependenciesOfSafe(package.Name)
            |> failIfNone (ReferenceNotFoundInLockFile(referencesFile.FileName, package.Name)))
        |> collect
        |> lift (Seq.concat >> Set.ofSeq)
