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

type LockFileGroup = {
    Name: string
    Options:InstallOptions
    Resolution:PackageResolution
    RemoteFiles:ResolvedSourceFile list
}

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
              match options.Settings.CopyLocal with
              | Some x -> yield "COPY-LOCAL: " + x.ToString().ToUpper()
              | None -> ()

              match options.Settings.ImportTargets with
              | Some x -> yield "IMPORT-TARGETS: " + x.ToString().ToUpper()
              | None -> ()

              match options.Settings.OmitContent with
              | Some true -> yield "CONTENT: NONE"
              | Some false -> yield "CONTENT: TRUE"
              | None -> ()

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

            [ for (owner,project,origin), files in files |> List.groupBy (fun f -> f.Owner, f.Project, f.Origin) do
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
        { GroupName : string
          RepositoryType : string option
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

    let private (|Remote|NugetPackage|NugetDependency|SourceFile|RepositoryType|Group|InstallOption|) (state, line:string) =
        match (state.RepositoryType, line.Trim()) with
        | _, "HTTP" -> RepositoryType "HTTP"
        | _, "GIST" -> RepositoryType "GIST"
        | _, "NUGET" -> RepositoryType "NUGET"
        | _, "GITHUB" -> RepositoryType "GITHUB"
        | _, String.StartsWith "remote:" trimmed -> Remote(PackageSource.Parse("source " + trimmed.Trim()).ToString())
        | _, String.StartsWith "GROUP:" trimmed -> Group(trimmed.Replace("GROUP:","").Trim())
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
        | Some _, _ -> failwithf "unknown repository type %s." line
        | _ -> failwithf "unknown lock file format %s" line

    let Parse(lockFileLines) =
        let remove textToRemove (source:string) = source.Replace(textToRemove, "")
        let removeBrackets = remove "(" >> remove ")"
        ([{ GroupName = Constants.MainDependencyGroup; RepositoryType = None; RemoteUrl = None; Packages = []; SourceFiles = []; Options = InstallOptions.Default; LastWasPackage = false }], lockFileLines)
        ||> Seq.fold(fun state line ->
            match state with
            | [] -> failwithf "error"
            | currentGroup::otherGroups ->
                if String.IsNullOrWhiteSpace line || line.Trim().StartsWith("specs:") then currentGroup::otherGroups else
                match (currentGroup, line) with
                | Remote(url) -> { currentGroup with RemoteUrl = Some url }::otherGroups
                | Group(groupName) -> { GroupName = groupName; RepositoryType = None; RemoteUrl = None; Packages = []; SourceFiles = []; Options = InstallOptions.Default; LastWasPackage = false } :: currentGroup :: otherGroups
                | InstallOption (ReferencesMode(mode)) -> { currentGroup with Options = {currentGroup.Options with Strict = mode} }::otherGroups
                | InstallOption (Redirects(mode)) -> { currentGroup with Options = {currentGroup.Options with Redirects = mode} }::otherGroups
                | InstallOption (ImportTargets(mode)) -> { currentGroup with Options = {currentGroup.Options with Settings = { currentGroup.Options.Settings with ImportTargets = Some mode} } }::otherGroups
                | InstallOption (CopyLocal(mode)) -> { currentGroup with Options = {currentGroup.Options with Settings = { currentGroup.Options.Settings with CopyLocal = Some mode}} }::otherGroups
                | InstallOption (FrameworkRestrictions(r)) -> { currentGroup with Options = {currentGroup.Options with Settings = { currentGroup.Options.Settings with FrameworkRestrictions = r}} }::otherGroups
                | InstallOption (OmitContent(omit)) -> { currentGroup with Options = {currentGroup.Options with Settings = { currentGroup.Options.Settings with OmitContent = Some omit} }}::otherGroups
                | RepositoryType repoType -> { currentGroup with RepositoryType = Some repoType }::otherGroups
                | NugetPackage details ->
                    match currentGroup.RemoteUrl with
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

                        { currentGroup with 
                            LastWasPackage = true
                            Packages = 
                                    { Source = PackageSource.Parse(remote, None)
                                      Name = PackageName parts'.[0]
                                      Dependencies = Set.empty
                                      Unlisted = false
                                      Settings = InstallSettings.Parse(optionsString)
                                      Version = SemVer.Parse version } :: currentGroup.Packages }::otherGroups
                    | None -> failwith "no source has been specified."
                | NugetDependency (name, v) ->
                    let parts = v.Split([|" - "|],StringSplitOptions.None)
                    let version = parts.[0]
                    if currentGroup.LastWasPackage then                 
                        match currentGroup.Packages with
                        | currentPackage :: otherPackages -> 
                            { currentGroup with
                                    Packages = { currentPackage with
                                                    Dependencies = Set.add (PackageName name, DependenciesFileParser.parseVersionRequirement version, []) currentPackage.Dependencies
                                                } :: otherPackages } ::otherGroups                   
                        | [] -> failwith "cannot set a dependency - no package has been specified."
                    else
                        match currentGroup.SourceFiles with
                        | currentFile :: rest -> 
                            { currentGroup with
                                    SourceFiles = 
                                        { currentFile with
                                                    Dependencies = Set.add (PackageName name, VersionRequirement.AllReleases) currentFile.Dependencies
                                                } :: rest }  ::otherGroups                  
                        | [] -> failwith "cannot set a dependency - no remote file has been specified."
                | SourceFile(origin, details) ->
                    match origin with
                    | GitHubLink | GistLink ->
                        match currentGroup.RemoteUrl |> Option.map(fun s -> s.Split '/') with
                        | Some [| owner; project |] ->
                            let path, commit = match details.Split ' ' with
                                                | [| filePath; commit |] -> filePath, commit |> removeBrackets                                       
                                                | _ -> failwith "invalid file source details."
                            { currentGroup with  
                                LastWasPackage = false
                                SourceFiles = { Commit = commit
                                                Owner = owner
                                                Origin = origin
                                                Project = project
                                                Dependencies = Set.empty
                                                Name = path } :: currentGroup.SourceFiles }::otherGroups
                        | _ -> failwith "invalid remote details."
                    | HttpLink x ->
                        match currentGroup.RemoteUrl |> Option.map(fun s -> s.Split '/' |> Array.toList) with
                        | Some [ protocol; _; domain; ] ->
                            let name, path = 
                                match details.Split ' ' with
                                | [| filePath; path |] -> filePath, path |> removeBrackets
                                | _ -> failwith "invalid file source details."

                            let sourceFile =
                                { Commit = path
                                  Owner = domain
                                  Origin = HttpLink(currentGroup.RemoteUrl.Value)
                                  Project = ""
                                  Dependencies = Set.empty
                                  Name = name } 

                            { currentGroup with  
                                LastWasPackage = false
                                SourceFiles = sourceFile :: currentGroup.SourceFiles }::otherGroups
                        | Some [ protocol; _; domain; project ] ->
                            { currentGroup with  
                                LastWasPackage = false
                                SourceFiles = { Commit = String.Empty
                                                Owner = domain
                                                Origin = HttpLink(currentGroup.RemoteUrl.Value)
                                                Project = project
                                                Dependencies = Set.empty
                                                Name = details } :: currentGroup.SourceFiles }::otherGroups
                        | Some (protocol :: _ :: domain :: project :: moredetails) ->
                            { currentGroup with  
                                LastWasPackage = false
                                SourceFiles = { Commit = String.Empty
                                                Owner = domain
                                                Origin = HttpLink(currentGroup.RemoteUrl.Value)
                                                Project = project + "/" + String.Join("/",moredetails)
                                                Dependencies = Set.empty
                                                Name = details } :: currentGroup.SourceFiles }::otherGroups
                        | _ ->  failwithf "invalid remote details %A" currentGroup.RemoteUrl )


/// Allows to parse and analyze paket.lock files.
type LockFile(fileName:string,groups: Map<string,LockFileGroup>) =
    let mainGroup = groups.[Constants.MainDependencyGroup]

    
    member __.Groups = groups
    member __.FileName = fileName

    /// Gets all dependencies of the given package
    member this.GetAllNormalizedDependenciesOf(groupName,package:NormalizedPackageName) = 
        let group = groups.[groupName]
        let usedPackages = HashSet<_>()

        let rec addPackage (identity:NormalizedPackageName) =
            match group.Resolution.TryFind identity with
            | Some package ->
                if usedPackages.Add identity then
                    if not group.Options.Strict then
                        for d,_,_ in package.Dependencies do
                            addPackage(NormalizedPackageName d)
            | None -> failwithf "Package %O was referenced, but it was not found in the paket.lock file in group %s." identity groupName

        addPackage package

        usedPackages

    /// Gets all dependencies of the given package
    member this.GetAllDependenciesOf(groupName,package) =
        match this.GetAllDependenciesOfSafe(groupName,package) with
        | Some packages -> packages
        | None ->
            let (PackageName name) = package
            failwithf "Package %s was referenced, but it was not found in the paket.lock file in group %s." name groupName

    /// Gets all dependencies of the given package in the given group.
    member this.GetAllDependenciesOfSafe(groupName,package) =
        let group = groups.[groupName]
        let allDependenciesOf package =
            let usedPackages = HashSet<_>()

            let rec addPackage packageName =
                let identity = NormalizedPackageName packageName
                match group.Resolution.TryFind identity with
                | Some package ->
                    if usedPackages.Add packageName then
                        if not group.Options.Strict then
                            for d,_,_ in package.Dependencies do
                                addPackage d
                | None -> ()

            addPackage package

            usedPackages

        match group.Resolution |> Map.tryFind (NormalizedPackageName package) with
        | Some v -> Some(allDependenciesOf v.Name)
        | None -> None
        

    member this.GetAllNormalizedDependenciesOf(groupName,package:PackageName) = 
        this.GetAllDependenciesOf(groupName,package)
        |> Seq.map NormalizedPackageName
        |> Set.ofSeq

    member this.GetTransitiveDependencies(groupName) =
        let group = groups.[groupName]
        let fromNuGets =
            group.Resolution 
            |> Seq.map (fun d -> d.Value.Dependencies |> Seq.map (fun (n,_,_) -> n))
            |> Seq.concat
            |> Set.ofSeq

        let fromSourceFiles =
            group.RemoteFiles
            |> Seq.map (fun d -> d.Dependencies |> Seq.map fst)
            |> Seq.concat
            |> Set.ofSeq

        Set.union fromNuGets fromSourceFiles

    member this.GetTopLevelDependencies(groupName) = 
        let group = groups.[groupName]
        let transitive = 
            this.GetTransitiveDependencies(groupName) 
            |> Seq.map NormalizedPackageName 
            |> Set.ofSeq

        group.Resolution
        |> Map.filter (fun name _ -> transitive.Contains name |> not)

    member this.GetCompleteResolution() : PackageResolution =
        this.Groups
        |> Seq.map (fun kv -> kv.Value.Resolution |> Seq.map (fun kv -> kv.Key,kv.Value))
        |> Seq.concat
        |> Seq.distinctBy fst // TODO: this is not good. Try to get rid of the whole function
        |> Map.ofSeq

    /// Checks if the first package is a dependency of the second package
    member this.IsDependencyOf(dependentPackage,package) =
        this.GetAllDependenciesOf(package).Contains dependentPackage
    
    override __.ToString() =
        String.Join
            (Environment.NewLine,
             [|let mainGroup = groups.[Constants.MainDependencyGroup]
               yield LockFileSerializer.serializePackages mainGroup.Options mainGroup.Resolution
               yield LockFileSerializer.serializeSourceFiles mainGroup.RemoteFiles
               for g in groups do 
                if g.Key <> Constants.MainDependencyGroup then
                    yield "GROUP: " + g.Key
                    yield LockFileSerializer.serializePackages g.Value.Options g.Value.Resolution
                    yield LockFileSerializer.serializeSourceFiles g.Value.RemoteFiles|])


    /// Updates the paket.lock file with the analyzed dependencies from the paket.dependencies file.
    member this.Save() =
        let output = this.ToString()

        let hasChanged =
            if File.Exists fileName then
                let text = File.ReadAllText(fileName)
                normalizeLineEndings output <> normalizeLineEndings text
            else true

        if hasChanged then
            File.WriteAllText(fileName, output)
            tracefn "Locked version resolution written to %s" fileName
        else
            tracefn "%s is already up-to-date" fileName        

    /// Creates a paket.lock file at given location
    static member Create (lockFileName: string, installOptions: InstallOptions, resolvedPackages: PackageResolver.Resolution, resolvedSourceFiles: ModuleResolver.ResolvedSourceFile list) : LockFile =
        let resolvedPackages = resolvedPackages.GetModelOrFail()
        let mainGroup = { Name = Constants.MainDependencyGroup; Options = installOptions; Resolution = resolvedPackages; RemoteFiles = resolvedSourceFiles }
        let groups = [Constants.MainDependencyGroup, mainGroup] |> Map.ofSeq
        let lockFile = LockFile(lockFileName, groups)
        lockFile.Save()
        lockFile

    /// Parses a paket.lock file from file
    static member LoadFrom(lockFileName) : LockFile =        
        LockFile.Parse(lockFileName, File.ReadAllLines lockFileName)

    /// Parses a paket.lock file from lines
    static member Parse(lockFileName,lines) : LockFile = 
        let groups =
            LockFileParser.Parse lines
            |> List.map (fun state ->
                state.GroupName,
                { Name = state.GroupName
                  Options = state.Options
                  Resolution = state.Packages |> Seq.fold (fun map p -> Map.add (NormalizedPackageName p.Name) p map) Map.empty
                  RemoteFiles = List.rev state.SourceFiles })
            |> Map.ofList

        LockFile(lockFileName, groups)

    member this.GetPackageHull(referencesFile:ReferencesFile) =
        let usedPackages = Dictionary<_,_>()

        for p in referencesFile.NugetPackages do
            if usedPackages.ContainsKey p.Name then
                failwithf "Package %s is referenced more than once in %s" (p.Name.ToString()) referencesFile.FileName
            usedPackages.Add(p.Name,p)

        referencesFile.NugetPackages
        |> List.iter (fun package -> 
            try
                for g in this.Groups do // TODO: Match group from references file
                    for d in this.GetAllDependenciesOf(g.Key,package.Name) do
                        if usedPackages.ContainsKey d |> not then
                            usedPackages.Add(d,package)
            with exn -> failwithf "%s - in %s" exn.Message referencesFile.FileName)

        usedPackages

    member this.GetDependencyLookupTable(groupName) = 
        let group = groups.[groupName]
        group.Resolution
        |> Map.map (fun name package ->            
                        this.GetAllDependenciesOf(groupName,package.Name)
                        |> Set.ofSeq
                        |> Set.remove package.Name)

    member this.GetPackageHullSafe referencesFile =
        referencesFile.NugetPackages
        |> Seq.map (fun package -> 
            // TODO: use group from references file
            this.GetAllDependenciesOfSafe(Constants.MainDependencyGroup,package.Name)
            |> failIfNone (ReferenceNotFoundInLockFile(referencesFile.FileName, package.Name)))
        |> collect
        |> lift (Seq.concat >> Set.ofSeq)
