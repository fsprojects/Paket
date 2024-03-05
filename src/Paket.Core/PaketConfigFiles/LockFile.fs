namespace Paket

open System
open System.Collections.Generic
open System.IO
open Paket.Domain
open Paket.Git.Handling
open Paket.Logging
open Paket.PackageResolver
open Paket.ModuleResolver
open Paket.PackageSources
open Paket.Requirements
open Chessie.ErrorHandling

type LockFileGroup =
  { Name: GroupName
    Options:InstallOptions
    Resolution:PackageResolution
    RemoteFiles:ResolvedSourceFile list }
    member x.GetPackage name =
        PackageInfo.from x.Resolution.[name] x.Options.Settings
    member x.TryFind name =
        match x.Resolution.TryFind name with
        | Some r ->
            Some (PackageInfo.from r x.Options.Settings)
        | None -> None

module LockFileSerializer =
    let packageNames = System.Collections.Concurrent.ConcurrentDictionary<string,string>()
    let writePackageName (name:PackageName) =
        let packageName = name.ToString()
        match packageNames.TryGetValue(packageName.ToLower()) with
        | true, x -> x
        | _ -> packageName

    /// [omit]
    let serializeOptionsAsLines options = [
        if options.Strict then yield "REFERENCES: STRICT"
        match options.Settings.GenerateLoadScripts with
        | Some true -> yield "GENERATE-LOAD-SCRIPTS: ON"
        | Some false -> yield "GENERATE-LOAD-SCRIPTS: OFF"
        | None -> ()
        match options.Redirects with
        | Some BindingRedirectsSettings.On -> yield "REDIRECTS: ON"
        | Some BindingRedirectsSettings.Force -> yield "REDIRECTS: FORCE"
        | Some BindingRedirectsSettings.Off -> yield "REDIRECTS: OFF"
        | None -> ()
        match options.Settings.StorageConfig with
        | Some PackagesFolderGroupConfig.NoPackagesFolder -> yield "STORAGE: NONE"
        | Some PackagesFolderGroupConfig.SymbolicLink -> yield "STORAGE: SYMLINK"
        | Some PackagesFolderGroupConfig.DefaultPackagesFolder -> yield "STORAGE: PACKAGES"
        | Some (PackagesFolderGroupConfig.GivenPackagesFolder f) -> failwithf "Not implemented yet."
        | None -> ()
        match options.ResolverStrategyForTransitives with
        | Some ResolverStrategy.Min -> yield "STRATEGY: MIN"
        | Some ResolverStrategy.Max -> yield "STRATEGY: MAX"
        | None -> ()
        match options.ResolverStrategyForDirectDependencies with
        | Some ResolverStrategy.Min -> yield "LOWEST_MATCHING: TRUE"
        | Some ResolverStrategy.Max -> yield "LOWEST_MATCHING: FALSE"
        | None -> ()
        match options.Settings.CopyLocal with
        | Some x -> yield "COPY-LOCAL: " + x.ToString().ToUpper()
        | None -> ()
        match options.Settings.SpecificVersion with
        | Some x -> yield "SPECIFIC-VERSION: " + x.ToString().ToUpper()
        | None -> ()
        match options.Settings.CopyContentToOutputDirectory with
        | Some CopyToOutputDirectorySettings.Always -> yield "COPY-CONTENT-TO-OUTPUT-DIR: ALWAYS"
        | Some CopyToOutputDirectorySettings.Never -> yield "COPY-CONTENT-TO-OUTPUT-DIR: NEVER"
        | Some CopyToOutputDirectorySettings.PreserveNewest -> yield "COPY-CONTENT-TO-OUTPUT-DIR: PRESERVE_NEWEST"
        | None -> ()

        match options.Settings.ImportTargets with
        | Some x -> yield "IMPORT-TARGETS: " + x.ToString().ToUpper()
        | None -> ()

        match options.Settings.LicenseDownload with
        | Some x -> yield "LICENSE-DOWNLOAD: " + x.ToString().ToUpper()
        | None -> ()

        match options.Settings.OmitContent with
        | Some ContentCopySettings.Omit -> yield "CONTENT: NONE"
        | Some ContentCopySettings.Overwrite -> yield "CONTENT: TRUE"
        | Some ContentCopySettings.OmitIfExisting -> yield "CONTENT: ONCE"
        | None -> ()

        match options.Settings.ReferenceCondition with
        | Some condition -> yield "CONDITION: " + condition.ToUpper()
        | None -> ()

        match options.Settings.FrameworkRestrictions |> getExplicitRestriction with
        | FrameworkRestriction.HasNoRestriction -> ()
        | list  -> yield "RESTRICTION: " + list.ToString() ]

    /// [omit]
    let serializePackages options (resolved : PackageResolution) =
        let sources =
            resolved
            |> Seq.map (fun kv ->
                    let package = kv.Value
                    match package.Source with
                    | NuGetV2 source -> source.Url,source.Authentication,package
                    | NuGetV3 source -> source.Url,source.Authentication,package
                    // TODO: Add credentials provider...
                    | LocalNuGet(path,_) -> path,AuthService.GetGlobalAuthenticationProvider path,package
                )
            |> Seq.groupBy (fun (a,b,_) -> a)

        let all =
            let hasReported = ref false
            [ yield! serializeOptionsAsLines options

              for source, packages in sources do
                  if not !hasReported then
                    yield "NUGET"
                    hasReported := true

                  yield "  remote: " + String.quoted source

                  for _,_,package in packages |> Seq.sortBy (fun (_,_,p) -> p.Name) do
                      let versionStr =
                          let s'' = package.Version.ToString()
                          let s' =
                            if source.Contains "nuget.org" && options.Settings.IncludeVersionInPath <> Some true && package.Settings.IncludeVersionInPath <> Some true then
                                package.Version.NormalizeToShorter()
                            else
                                s''

                          let s = if s''.Length > s'.Length then s' else s''
                          if s = "" then s else "(" + s + ")"

                      let settings =
                        if package.Settings.FrameworkRestrictions = options.Settings.FrameworkRestrictions then
                            { package.Settings with FrameworkRestrictions = ExplicitRestriction FrameworkRestriction.NoRestriction }
                        else
                            package.Settings

                      let s =
                        // add "clitool"
                        match package.Kind, settings.ToString(options.Settings,false).ToLower()  with
                        | ResolvedPackageKind.DotnetCliTool, "" -> "clitool: true"
                        | ResolvedPackageKind.DotnetCliTool, s -> s + ", clitool: true"
                        | ResolvedPackageKind.Package, s -> s

                      let s =
                        // add "isRuntimeDependency"
                        match package.IsRuntimeDependency, s with
                        | true, "" -> "isRuntimeDependency: true"
                        | true, s -> s + ", isRuntimeDependency: true"
                        | _, s -> s


                      if s = "" then
                          yield sprintf "    %s %s" (writePackageName package.Name) versionStr
                      else
                          yield sprintf "    %s %s - %s" (writePackageName package.Name) versionStr s

                      for name,v,restrictions in package.Dependencies do
                          let versionStr =
                              let s = v.ToString()
                              if s = "" then s else "(" + s + ")"

                          let restrictions = filterRestrictions options.Settings.FrameworkRestrictions restrictions |> getExplicitRestriction
                          if FrameworkRestriction.NoRestriction = restrictions || restrictions = getExplicitRestriction options.Settings.FrameworkRestrictions then
                            yield sprintf "      %s %s" (writePackageName name) versionStr
                          else
                            yield sprintf "      %s %s - restriction: %O" (writePackageName name) versionStr restrictions]

        String.Join(Environment.NewLine, all |> List.map (fun s -> s.TrimEnd()))

    let serializeSourceFiles (files:ResolvedSourceFile list) =
        let all =
            let updateHasReported = new List<Origin>()

            [ for (owner,project,origin), files in files |> List.groupBy (fun f -> f.Owner, f.Project, f.Origin) do
                match origin with
                | GitHubLink ->
                    if not (updateHasReported.Contains(GitHubLink)) then
                        yield "GITHUB"
                        updateHasReported.Remove (HttpLink "") |> ignore
                        updateHasReported.Remove (GitLink (RemoteGitOrigin"")) |> ignore
                        updateHasReported.Remove GistLink |> ignore
                        updateHasReported.Add GitHubLink
                    yield sprintf "  remote: %s/%s" owner project
                | GitLink (LocalGitOrigin  url)
                | GitLink (RemoteGitOrigin url) ->
                    if not (updateHasReported.Contains(GitLink(RemoteGitOrigin""))) then
                        yield "GIT"
                        updateHasReported.Remove GitHubLink |> ignore
                        updateHasReported.Remove GistLink |> ignore
                        updateHasReported.Remove (HttpLink "") |> ignore
                        updateHasReported.Add (GitLink (RemoteGitOrigin""))
                    yield sprintf "  remote: " + url

                | GistLink ->
                    if not (updateHasReported.Contains(GistLink)) then
                        yield "GIST"
                        updateHasReported.Remove GitHubLink |> ignore
                        updateHasReported.Remove (HttpLink "") |> ignore
                        updateHasReported.Remove (GitLink (RemoteGitOrigin"")) |> ignore
                        updateHasReported.Add GistLink
                    yield sprintf "  remote: %s/%s" owner project
                | HttpLink url ->
                    if not (updateHasReported.Contains(HttpLink(""))) then
                        yield "HTTP"
                        updateHasReported.Remove GitHubLink |> ignore
                        updateHasReported.Remove GistLink |> ignore
                        updateHasReported.Remove (GitLink (RemoteGitOrigin"")) |> ignore
                        updateHasReported.Add (HttpLink "")
                    yield sprintf "  remote: " + url

                for file in files |> Seq.sortBy (fun f -> f.Owner.ToLower(),f.Project.ToLower(),f.Name.ToLower())  do

                    let path =
                        file.Name.TrimStart '/'
                        |> fun s ->
                            if System.Text.RegularExpressions.Regex.IsMatch (s, "\s") then
                                String.Concat ("\"", s, "\"")
                            else
                                s
                    match String.IsNullOrEmpty(file.Commit) with
                    | false ->
                        match file.AuthKey with
                        | Some authKey ->
                            yield sprintf "    %s (%s) %s" path file.Commit authKey
                        | None ->
                            yield sprintf "    %s (%s)" path file.Commit
                    | true ->
                        match file.AuthKey with
                        | Some authKey -> yield sprintf "    %s %s" path authKey
                        | None -> yield sprintf "    %s" path

                    match file.Command with
                    | None -> ()
                    | Some command -> yield "      build: " + command

                    match file.PackagePath with
                    | None -> ()
                    | Some path -> yield "      path: " + path

                    match file.OperatingSystemRestriction with
                    | None -> ()
                    | Some filter -> yield "      os: " + filter

                    for name,v in file.Dependencies do
                        let versionStr =
                            let s = v.ToString()
                            if s = "" then s else "(" + s + ")"
                        yield sprintf "      %O %s" name versionStr]

        String.Join(Environment.NewLine, all |> List.map (fun s -> s.TrimEnd()))

module LockFileParser =
    type ParseState = {
        GroupName : GroupName
        RepositoryType : string option
        RemoteUrl :string option
        Packages : ResolvedPackage list
        SourceFiles : ResolvedSourceFile list
        LastWasPackage : bool
        Options: InstallOptions
    }

    type private ParserOption =
    | ReferencesMode of bool
    | OmitContent of ContentCopySettings
    | ImportTargets of bool
    | LicenseDownload of bool
    | GenerateLoadScripts of bool option
    | FrameworkRestrictions of FrameworkRestrictions
    | CopyLocal of bool
    | SpecificVersion of bool
    | CopyContentToOutputDir of CopyToOutputDirectorySettings
    | Redirects of BindingRedirectsSettings option
    | StorageConfig of PackagesFolderGroupConfig option
    | ReferenceCondition of string
    | DirectDependenciesResolverStrategy of ResolverStrategy option
    | TransitiveDependenciesResolverStrategy of ResolverStrategy option
    | Command of string
    | PackagePath of string
    | OperatingSystemRestriction of string

    let private (|Remote|NugetPackage|NugetDependency|SourceFile|RepositoryType|Group|InstallOption|) (state, line:string) =
        match (state.RepositoryType, line.Trim()) with
        | _, "HTTP" -> RepositoryType "HTTP"
        | _, "GIST" -> RepositoryType "GIST"
        | _, "GIT" -> RepositoryType "GIT"
        | _, "NUGET" -> RepositoryType "NUGET"
        | _, "GITHUB" -> RepositoryType "GITHUB"
        | Some "NUGET", String.RemovePrefix "remote:" trimmed -> Remote(PackageSource.Parse("source " + trimmed.Trim()).ToString())
        | _, String.RemovePrefix "remote:" trimmed -> Remote(trimmed.Trim())
        | _, String.RemovePrefix "GROUP" trimmed -> Group(trimmed.Replace("GROUP","").Trim())
        | _, String.RemovePrefix "REFERENCES:" trimmed -> InstallOption(ReferencesMode(trimmed.Trim() = "STRICT"))
        | _, String.RemovePrefix "REDIRECTS:" trimmed ->
            let setting =
                match trimmed.Trim() with
                | String.EqualsIC "on" -> Some BindingRedirectsSettings.On
                | String.EqualsIC "force" -> Some BindingRedirectsSettings.Force
                | String.EqualsIC "off" -> Some BindingRedirectsSettings.Off
                | _ -> None

            InstallOption (Redirects setting)
        | _, String.RemovePrefix "STORAGE:" trimmed ->
            let setting =
                match trimmed.Trim() with
                | String.EqualsIC "NONE" -> Some PackagesFolderGroupConfig.NoPackagesFolder
                | String.EqualsIC "SYMLINK" -> Some PackagesFolderGroupConfig.SymbolicLink
                | String.EqualsIC "PACKAGES" -> Some PackagesFolderGroupConfig.DefaultPackagesFolder
                | _ -> None

            InstallOption (StorageConfig setting)
        | _, String.RemovePrefix "IMPORT-TARGETS:" trimmed -> InstallOption(ImportTargets(trimmed.Trim() = "TRUE"))
        | _, String.RemovePrefix "LICENSE-DOWNLOAD:" trimmed -> InstallOption(LicenseDownload(trimmed.Trim() = "TRUE"))
        | _, String.RemovePrefix "COPY-LOCAL:" trimmed -> InstallOption(CopyLocal(trimmed.Trim() = "TRUE"))
        | _, String.RemovePrefix "SPECIFIC-VERSION:" trimmed -> InstallOption(SpecificVersion(trimmed.Trim() = "TRUE"))
        | _, String.RemovePrefix "GENERATE-LOAD-SCRIPTS:" trimmed ->
            let setting =
                match trimmed.Trim() with
                | String.EqualsIC "on" -> Some true
                | String.EqualsIC "off" -> Some false
                | _ -> None

            InstallOption (GenerateLoadScripts setting)
        | _, String.RemovePrefix "COPY-CONTENT-TO-OUTPUT-DIR:" trimmed ->
            let setting =
                match trimmed.Replace(":","").Trim().ToLowerInvariant() with
                | "always" -> CopyToOutputDirectorySettings.Always
                | "never" -> CopyToOutputDirectorySettings.Never
                | "preserve_newest" -> CopyToOutputDirectorySettings.PreserveNewest
                | x -> failwithf "Unknown copy_content_to_output_dir settings: %A" x

            InstallOption (CopyContentToOutputDir setting)
        | _, String.RemovePrefix "FRAMEWORK:" trimmed -> InstallOption(FrameworkRestrictions(ExplicitRestriction (trimmed.Trim() |> Requirements.parseRestrictionsLegacy true |> fst)))
        | _, String.RemovePrefix "RESTRICTION:" trimmed -> InstallOption(FrameworkRestrictions(ExplicitRestriction (trimmed.Trim() |> Requirements.parseRestrictionsSimplified |> fst)))
        | _, String.RemovePrefix "CONDITION:" trimmed -> InstallOption(ReferenceCondition(trimmed.Trim().ToUpper()))
        | _, String.RemovePrefix "CONTENT:" trimmed ->
            let setting =
                match trimmed.Trim().ToLowerInvariant() with
                | "none" -> ContentCopySettings.Omit
                | "once" -> ContentCopySettings.OmitIfExisting
                | _ -> ContentCopySettings.Overwrite

            InstallOption (OmitContent setting)
        | _, String.RemovePrefix "STRATEGY:" trimmed ->
            let setting =
                match trimmed.Trim() with
                | String.EqualsIC "min" -> Some ResolverStrategy.Min
                | String.EqualsIC "max" -> Some ResolverStrategy.Max
                | _ -> None

            InstallOption(TransitiveDependenciesResolverStrategy(setting))
        | _, String.RemovePrefix "LOWEST_MATCHING:" trimmed ->
            let setting =
                match trimmed.Trim() with
                | String.EqualsIC "true" -> Some ResolverStrategy.Min
                | String.EqualsIC "false" -> Some ResolverStrategy.Max
                | _ -> None

            InstallOption(DirectDependenciesResolverStrategy(setting))
        | _, String.RemovePrefix "build: " trimmed ->
            InstallOption(Command trimmed)
        | _, String.RemovePrefix "path: " trimmed ->
            InstallOption(PackagePath trimmed)
        | _, String.RemovePrefix "os: " trimmed ->
            InstallOption(OperatingSystemRestriction trimmed)
        | _, trimmed when line.StartsWith "      " ->
            let pos = trimmed.IndexOf " - "
            let namePart, settingsPart =
                if pos >= 0 then
                    trimmed.Substring(0, pos), trimmed.Substring (pos + 3)
                else
                    trimmed, ""
            let frameworkSettings =
                if not (String.IsNullOrEmpty settingsPart) then
                    try
                        InstallSettings.Parse(true, settingsPart)
                    with
                    | e ->
                        try InstallSettings.Parse(true, "framework: " + settingsPart) // backwards compatible
                        with e2 ->
                            raise <| AggregateException(sprintf "failed to parse line '%s'" line, e, e2)
                else
                    InstallSettings.Default
            if namePart.Contains "(" then
                let parts = namePart.Split '('
                let first = parts.[0]
                let rest = String.Join ("(", parts |> Seq.skip 1)
                let versionEndPos = rest.IndexOf(")")
                if versionEndPos < 0 then failwithf "Missing matching ') in line '%s'" line
                NugetDependency (parts.[0].Trim(),rest.Substring(0, versionEndPos).Trim(),frameworkSettings)
            else
                NugetDependency (namePart.Trim(),">= 0",frameworkSettings)
        | Some "NUGET", trimmed -> NugetPackage trimmed
        | Some "GITHUB", trimmed -> SourceFile(GitHubLink, trimmed)
        | Some "GIST", trimmed -> SourceFile(GistLink, trimmed)
        | Some "GIT", trimmed -> SourceFile(GitLink(RemoteGitOrigin""), trimmed)
        | Some "HTTP", trimmed  -> SourceFile(HttpLink(String.Empty), trimmed)
        | Some _, _ -> failwithf "unknown repository type %s." line
        | _ -> failwithf "unknown lock file format %s" line

    let private extractOption currentGroup option =
        match option with
        | ReferencesMode mode -> { currentGroup.Options with Strict = mode }
        | Redirects mode -> { currentGroup.Options with Redirects = mode }
        | StorageConfig mode -> { currentGroup.Options with Settings = { currentGroup.Options.Settings with StorageConfig = mode }}
        | ImportTargets mode -> { currentGroup.Options with Settings = { currentGroup.Options.Settings with ImportTargets = Some mode } }
        | LicenseDownload mode -> { currentGroup.Options with Settings = { currentGroup.Options.Settings with LicenseDownload = Some mode } }
        | CopyLocal mode -> { currentGroup.Options with Settings = { currentGroup.Options.Settings with CopyLocal = Some mode }}
        | SpecificVersion mode -> { currentGroup.Options with Settings = { currentGroup.Options.Settings with SpecificVersion = Some mode }}
        | CopyContentToOutputDir mode -> { currentGroup.Options with Settings = { currentGroup.Options.Settings with CopyContentToOutputDirectory = Some mode }}
        | FrameworkRestrictions r -> { currentGroup.Options with Settings = { currentGroup.Options.Settings with FrameworkRestrictions = r }}
        | OmitContent omit -> { currentGroup.Options with Settings = { currentGroup.Options.Settings with OmitContent = Some omit }}
        | GenerateLoadScripts mode -> { currentGroup.Options with Settings = { currentGroup.Options.Settings with GenerateLoadScripts = mode }}
        | ReferenceCondition condition -> { currentGroup.Options with Settings = { currentGroup.Options.Settings with ReferenceCondition = Some condition }}
        | DirectDependenciesResolverStrategy strategy -> { currentGroup.Options with ResolverStrategyForDirectDependencies = strategy }
        | TransitiveDependenciesResolverStrategy strategy -> { currentGroup.Options with ResolverStrategyForTransitives = strategy }
        | _ -> failwithf "Unknown option %A" option

    let Parse lockFileLines =
        let remove textToRemove (source:string) = source.Replace(textToRemove, "")
        let removeBrackets = remove "(" >> remove ")"
        let parsePackage (s : string) =
            let parts = s.Split([|" - "|],StringSplitOptions.None)
            let optionsString =
                if parts.Length < 2 then "" else
                if parts.[1] <> "" && parts.[1].Contains(":") |> not then
                    ("framework: " + parts.[1])
                else
                    parts.[1]

            let kind, optionsString =
                if optionsString.EndsWith ", clitool: true" then
                    ResolvedPackageKind.DotnetCliTool,optionsString.Replace(", clitool: true","")
                elif optionsString.EndsWith "clitool: true" then
                    ResolvedPackageKind.DotnetCliTool,optionsString.Replace("clitool: true","")
                else
                    ResolvedPackageKind.Package,optionsString

            let isRuntimeDependency, optionsString =
                if optionsString.EndsWith ", isRuntimeDependency: true" then
                    true, optionsString.Substring(0, optionsString.Length - ", isRuntimeDependency: true".Length)
                elif optionsString.EndsWith "isRuntimeDependency: true" then
                    assert (optionsString = "isRuntimeDependency: true")
                    true, ""
                else false, optionsString

            parts.[0],kind,isRuntimeDependency,InstallSettings.Parse(true, optionsString)

        ([{ GroupName = Constants.MainDependencyGroup; RepositoryType = None; RemoteUrl = None; Packages = []; SourceFiles = []; Options = InstallOptions.Default; LastWasPackage = false }], lockFileLines)
        ||> Seq.fold(fun state line ->
            match state with
            | [] -> failwithf "error"
            | currentGroup::otherGroups ->
                if String.IsNullOrWhiteSpace line || line.Trim().StartsWith("specs:") then currentGroup::otherGroups else
                match (currentGroup, line) with
                | Remote url -> { currentGroup with RemoteUrl = Some url }::otherGroups
                | Group groupName -> { GroupName = GroupName groupName; RepositoryType = None; RemoteUrl = None; Packages = []; SourceFiles = []; Options = InstallOptions.Default; LastWasPackage = false } :: currentGroup :: otherGroups
                | InstallOption(Command(command)) ->
                    let sourceFiles =
                        match currentGroup.SourceFiles with
                        | sourceFile::rest ->{ sourceFile with Command = Some command } :: rest
                        |  _ -> failwith "missing source file"
                    { currentGroup with SourceFiles = sourceFiles }::otherGroups
                | InstallOption(PackagePath(path)) ->
                    let sourceFiles =
                        match currentGroup.SourceFiles with
                        | sourceFile::rest ->{ sourceFile with PackagePath = Some path } :: rest
                        |  _ -> failwith "missing source file"
                    { currentGroup with SourceFiles = sourceFiles }::otherGroups
                | InstallOption(OperatingSystemRestriction(filter)) ->
                    let sourceFiles =
                        match currentGroup.SourceFiles with
                        | sourceFile::rest ->{ sourceFile with OperatingSystemRestriction = Some filter } :: rest
                        |  _ -> failwith "missing source file"
                    { currentGroup with SourceFiles = sourceFiles }::otherGroups
                | InstallOption option ->
                    { currentGroup with Options = extractOption currentGroup option }::otherGroups
                | RepositoryType repoType -> { currentGroup with RepositoryType = Some repoType }::otherGroups
                | NugetPackage details ->
                    match currentGroup.RemoteUrl with
                    | Some remote ->
                        let package,kind,isRuntimeDependency,settings = parsePackage details
                        let parts' = package.Split ' '
                        let version =
                            if parts'.Length < 2 then
                                failwithf "No version specified for package %O in group %O." package currentGroup.GroupName
                            parts'.[1] |> removeBrackets

                        let lockFilePackageName = parts'.[0]
                        let packageName = LockFileSerializer.packageNames.GetOrAdd(lockFilePackageName.ToLower(), fun _ -> lockFilePackageName)
                        { currentGroup with
                            LastWasPackage = true
                            Packages =
                                    { Source = PackageSource.Parse(remote, AuthService.GetGlobalAuthenticationProvider remote)
                                      Name = PackageName packageName
                                      Dependencies = Set.empty
                                      Unlisted = false
                                      Settings = settings
                                      Version = SemVer.Parse version
                                      Kind = kind
                                      // TODO: write stuff into the lockfile and read it here
                                      IsRuntimeDependency = isRuntimeDependency } :: currentGroup.Packages }::otherGroups
                    | None -> failwith "no source has been specified."
                | NugetDependency (name, v, frameworkSettings) ->
                    let version,_,isRuntimeDependency,settings = parsePackage v
                    assert (not isRuntimeDependency)
                    if currentGroup.LastWasPackage then
                        match currentGroup.Packages with
                        | currentPackage :: otherPackages ->
                            { currentGroup with
                                    Packages = { currentPackage with
                                                    Dependencies = Set.add (PackageName name, DependenciesFileParser.parseVersionRequirement version, frameworkSettings.FrameworkRestrictions) currentPackage.Dependencies
                                                } :: otherPackages } ::otherGroups
                        | [] -> failwithf "cannot set a dependency to %s %s - no package has been specified." name v
                    else
                        match currentGroup.SourceFiles with
                        | currentFile :: rest ->
                            { currentGroup with
                                    SourceFiles =
                                        { currentFile with
                                                    Dependencies = Set.add (PackageName name, DependenciesFileParser.parseVersionRequirement version) currentFile.Dependencies
                                                } :: rest }  ::otherGroups
                        | [] -> failwithf "cannot set a dependency to %s %s- no remote file has been specified." name v

                | SourceFile(origin, details) ->
                    match origin with
                    | GitHubLink | GistLink ->
                        match currentGroup.RemoteUrl |> Option.map(fun s -> s.Split '/') with
                        | Some [| owner; project |] ->
                            let pieces =
                                if details.Contains "\"" then
                                    let pathInfo =
                                        match details.IndexOf ('"', 1) with
                                        | idx when idx >= 0 -> Some (details.Substring (1, idx - 1), idx)
                                        | _ -> None
                                    match pathInfo with
                                    | Some (path, pathEndIdx) ->
                                        let commitAndAuthKey = details.Substring(pathEndIdx + 2).Split(' ')
                                        Array.append [| path |] commitAndAuthKey
                                    | None -> Array.empty
                                else
                                    details.Split ' '

                            let path, commit, authKey =
                                match pieces with
                                | [| filePath; commit; authKey |] -> filePath, commit |> removeBrackets, (Some authKey)
                                | [| filePath; commit |] -> filePath, commit |> removeBrackets, None
                                | _ -> failwith "invalid file source details."
                            { currentGroup with
                                LastWasPackage = false
                                SourceFiles = { Commit = commit
                                                Owner = owner
                                                Origin = origin
                                                Project = project
                                                Dependencies = Set.empty
                                                Name = path
                                                Command = None
                                                OperatingSystemRestriction = None
                                                PackagePath = None
                                                AuthKey = authKey } :: currentGroup.SourceFiles }::otherGroups
                        | _ -> failwith "invalid remote details."
                    | HttpLink _ ->
                        match currentGroup.RemoteUrl |> Option.map(fun s -> s.Split '/' |> Array.toList) with
                        | Some [ protocol; _; domain; ] ->
                            let project, name, path, authKey =
                                 match details.Split ' ' with
                                 | [| filePath; path |] -> "", filePath, path |> removeBrackets, None
                                 | [| filePath; path; authKey |] -> "", filePath, path |> removeBrackets, (Some authKey)
                                 | _ -> failwith "invalid file source details."

                            let removeInvalidChars (str:string) =
                                System.Text.RegularExpressions.Regex.Replace(str, "[:@\,]", "_")

                            let sourceFile =
                                { Commit = path
                                  Owner = domain |> removeInvalidChars
                                  Origin = HttpLink(currentGroup.RemoteUrl.Value)
                                  Project = project
                                  Dependencies = Set.empty
                                  Name = name
                                  Command = None
                                  OperatingSystemRestriction = None
                                  PackagePath = None
                                  AuthKey = authKey }

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
                                                Command = None
                                                OperatingSystemRestriction = None
                                                PackagePath = None
                                                Dependencies = Set.empty
                                                Name = details
                                                AuthKey = None } :: currentGroup.SourceFiles }::otherGroups
                        | Some (protocol :: _ :: domain :: project :: moredetails) ->
                            { currentGroup with
                                LastWasPackage = false
                                SourceFiles = { Commit = String.Empty
                                                Owner = domain
                                                Origin = HttpLink(currentGroup.RemoteUrl.Value)
                                                Project = project + "/" + String.Join("/",moredetails)
                                                Dependencies = Set.empty
                                                Command = None
                                                OperatingSystemRestriction = None
                                                PackagePath = None
                                                Name = details
                                                AuthKey = None } :: currentGroup.SourceFiles }::otherGroups
                        | _ ->  failwithf "invalid remote details %A" currentGroup.RemoteUrl
                    | GitLink _ ->
                        match currentGroup.RemoteUrl with
                        | Some cloneUrl ->
                            let owner,commit,project,origin,buildCommand,operatingSystemRestriction,packagePath = Git.Handling.extractUrlParts cloneUrl
                            { currentGroup with
                                LastWasPackage = false
                                SourceFiles = { Commit = details.Replace("(","").Replace(")","")
                                                Owner = owner
                                                Origin = GitLink origin
                                                Project = project
                                                Dependencies = Set.empty
                                                Command = buildCommand
                                                OperatingSystemRestriction = operatingSystemRestriction
                                                PackagePath = packagePath
                                                Name = ""
                                                AuthKey = None } :: currentGroup.SourceFiles }::otherGroups
                        | _ ->  failwithf "invalid remote details %A" currentGroup.RemoteUrl)


/// Allows to parse and analyze paket.lock files.
type LockFile (fileName:string, groups: Map<GroupName,LockFileGroup>) =
    let fileName = if isNull fileName then String.Empty else fileName

    let tryFindRemoteFile (remoteFiles:ResolvedSourceFile list) (name: string) =
        remoteFiles |> List.tryFind (fun x -> x.Name.EndsWith(name))
    let findRemoteFile referencesFile remoteFiles name =
        match tryFindRemoteFile remoteFiles name with
        | None -> failwithf "Remote file %O was referenced in %s, but not found in paket.lock." name referencesFile
        | Some lockRemote -> lockRemote
    let findGroup referencesFile groupName =
        match groups |> Map.tryFind groupName with
        | None -> failwithf "Group %O was referenced in %s, but not found in paket.lock." groupName referencesFile
        | Some lockGroup -> lockGroup

    member __.Groups = groups
    member __.FileName = fileName
    member __.RootPath =
        try FileInfo(fileName).Directory.FullName
        with _ -> String.Empty

    member this.GetGroup groupName =
        match this.Groups |> Map.tryFind groupName with
        | Some g -> g
        | None -> failwithf "Group %O was not found in %s." groupName fileName

    member this.CheckIfPackageExistsInAnyGroup (packageName:PackageName) =
        match groups |> Seq.tryFind (fun g -> g.Value.Resolution.ContainsKey packageName) with
        | Some group -> sprintf "%sHowever, %O was found in group %O." Environment.NewLine packageName group.Value.Name
        | None -> ""


    /// Gets all dependencies of the given package
    member this.GetAllNormalizedDependenciesOf(groupName,package:PackageName,context) =
        let group = groups.[groupName]
        let usedPackages = HashSet<_>()

        let rec addPackage (identity:PackageName) =
            match group.Resolution.TryFind identity with
            | Some package ->
                if usedPackages.Add(groupName,identity) then
                    if not group.Options.Strict then
                        for d,_,_ in package.Dependencies do
                            addPackage d
            | None ->
                failwithf "Package %O was referenced in %s, but it was not found in the paket.lock file in group %O.%s" identity context groupName (this.CheckIfPackageExistsInAnyGroup package)

        addPackage package

        usedPackages

    /// Gets all dependencies of the given package
    member this.GetAllDependenciesOf(groupName,package,context) =
        match this.GetAllDependenciesOfSafe(groupName,package) with
        | Some packages -> packages
        | None ->
            failwithf "Package %O was referenced in %s, but it was not found in the paket.lock file in group %O.%s" package context groupName (this.CheckIfPackageExistsInAnyGroup package)

    /// Gets all dependencies of the given package in the given group.
    member __.GetAllDependenciesOfSafe(groupName:GroupName,package) =
        let group = groups.[groupName]
        let allDependenciesOf package =
            let usedPackages = HashSet<_>()

            let rec addPackage packageName =
                let identity = packageName
                match group.Resolution.TryFind identity with
                | Some package ->
                    if usedPackages.Add packageName then
                        if not group.Options.Strict then
                            for d,_,_ in package.Dependencies do
                                addPackage d
                | None -> ()

            addPackage package

            usedPackages

        match group.Resolution |> Map.tryFind package with
        | Some v -> Some(allDependenciesOf v.Name)
        | None -> None


    member this.ResolveFrameworksForScriptGeneration () = lazy (
        this.Groups
        |> Seq.collect (fun kvp ->
            match kvp.Value.Options.Settings.FrameworkRestrictions with
            | Paket.Requirements.AutoDetectFramework -> failwithf "couldn't detect framework"
            | Paket.Requirements.ExplicitRestriction list ->
                list.RepresentedFrameworks |> Seq.choose (function TargetProfile.SinglePlatform tf -> Some tf | _ -> None)
        )
    )

    /// Gets only direct dependencies of the given package in the given group.
    member this.GetDirectDependenciesOfSafe(groupName:GroupName,package,context) =
        let group = groups.[groupName]

        match group.Resolution |> Map.tryFind package with
        | Some v ->
            let usedPackages = HashSet<_>()

            for d,_,_ in v.Dependencies do
                if group.Resolution.ContainsKey d then
                    usedPackages.Add d |> ignore

            usedPackages |> Set.ofSeq
        | None -> failwithf "Package %O was referenced in %s, but it was not found in the paket.lock file in group %O.%s" package context groupName (this.CheckIfPackageExistsInAnyGroup package)

    member __.GetTransitiveDependencies(groupName) =
        let collectDependenciesForGroup group =
            let fromNuGets =
                group.Resolution
                |> Seq.collect (fun d ->
                    d.Value.Dependencies
                    |> Seq.map (fun (n,_,_) -> n))
                |> Set.ofSeq

            let runtimeDeps =
                group.Resolution
                |> Seq.choose (fun d ->
                    if d.Value.IsRuntimeDependency then
                        Some d.Value.Name
                    else
                        None)
                |> Set.ofSeq

            let fromSourceFiles =
                group.RemoteFiles
                |> Seq.collect (fun d -> d.Dependencies |> Seq.map fst)
                |> Set.ofSeq

            fromSourceFiles
            |> Set.union fromNuGets
            |> Set.union runtimeDeps

        match groups.TryFind groupName with
        | None -> Set.empty
        | Some group -> collectDependenciesForGroup group

    member this.GetTopLevelDependencies(groupName) =
        match groups |> Map.tryFind groupName with
        | None -> Map.empty
        | Some group ->
            let transitive = this.GetTransitiveDependencies groupName

            group.Resolution
            |> Map.filter (fun name _ -> transitive.Contains name |> not)
            |> Map.map (fun name v -> PackageInfo.from v group.Options.Settings)

    member this.GetGroupedResolution () =
        this.Groups
        |> Seq.collect (fun kv -> kv.Value.Resolution |> Seq.map (fun kv' -> (kv.Key,kv'.Key),PackageInfo.from kv'.Value kv.Value.Options.Settings))
        |> Map.ofSeq


    member this.GetResolvedPackages () =
        groups |> Map.map (fun groupName lockGroup ->
           lockGroup.Resolution |> Seq.map (fun x -> PackageInfo.from x.Value lockGroup.Options.Settings) |> List.ofSeq
        )


    override __.ToString() =
        String.Join (Environment.NewLine,
            [|  let mainGroup = groups.[Constants.MainDependencyGroup]
                yield LockFileSerializer.serializePackages mainGroup.Options mainGroup.Resolution
                yield LockFileSerializer.serializeSourceFiles mainGroup.RemoteFiles
                for g in groups do
                    if g.Key <> Constants.MainDependencyGroup then
                        yield "GROUP " + g.Value.Name.ToString()
                        yield LockFileSerializer.serializePackages g.Value.Options g.Value.Resolution
                        yield LockFileSerializer.serializeSourceFiles g.Value.RemoteFiles
            |])


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
            verbosefn "Locked version resolution written to %s" fileName
        else
            verbosefn "%s is already up-to-date" fileName
        hasChanged

    /// Parses a paket.lock file from file
    static member LoadFrom(lockFileName) : LockFile =
        LockFile.Parse(lockFileName, File.ReadAllLines lockFileName)

    /// Parses a paket.lock file from lines
    static member Parse(lockFileName,lines) : LockFile =
        try
            let groups =
                LockFileParser.Parse lines
                |> List.map (fun state ->
                    state.GroupName,
                    { Name = state.GroupName
                      Options = state.Options
                      Resolution = state.Packages |> Seq.fold (fun map p -> Map.add p.Name p map) Map.empty
                      RemoteFiles = List.rev state.SourceFiles })
                |> Map.ofList

            LockFile(lockFileName, groups)
        with
        | exn ->
            raise (Exception (sprintf "Error during parsing of '%s'." lockFileName, exn))


    member this.GetPackageHull(referencesFile:ReferencesFile) =
        let usedPackages = Dictionary<_,_>()

        for g in referencesFile.Groups do
            let lockGroup = findGroup referencesFile.FileName g.Key
            for p in g.Value.NugetPackages do
                let k = g.Key,p.Name
                if usedPackages.ContainsKey k |> not then
                    usedPackages.Add(k,{p with Settings = lockGroup.Options.Settings})

            for r in g.Value.RemoteFiles do
                let lockRemote = findRemoteFile  referencesFile.FileName lockGroup.RemoteFiles r.Name
                for p,_ in lockRemote.Dependencies do
                    let k = g.Key,p
                    if usedPackages.ContainsKey k |> not then
                        usedPackages.Add(k,PackageInstallSettings.Default(p.ToString()))

        for g in referencesFile.Groups do
            for package in g.Value.NugetPackages do
                try
                    for d in this.GetAllDependenciesOf(g.Key,package.Name,referencesFile.FileName) do
                        let k = g.Key,d
                        if usedPackages.ContainsKey k |> not then
                            usedPackages.Add(k,package)
                with exn -> raise (Exception(sprintf "Error while getting all dependencies in '%s'" referencesFile.FileName, exn))

        usedPackages

    member __.GetRemoteReferencedPackages(referencesFile:ReferencesFile,installGroup:InstallGroup) =
        [for r in installGroup.RemoteFiles do
            let lockGroup = findGroup referencesFile.FileName installGroup.Name
            let lockRemote = findRemoteFile referencesFile.FileName lockGroup.RemoteFiles r.Name
            for p,_ in lockRemote.Dependencies do
                yield PackageInstallSettings.Default(p.ToString())]

    member this.GetOrderedPackageHull(groupName,referencesFile:ReferencesFile,targetProfileOpt) =
        let usedPackageKeys = HashSet<_>()
        let toVisit = ref []
        let cliTools = ref Set.empty
        let resolution =
            match this.Groups |> Map.tryFind groupName with
            | Some group -> group.Resolution
            | None -> failwithf "Error for %s: Group %O can't be found in paket.lock file." referencesFile.FileName groupName

        match referencesFile.Groups |> Map.tryFind groupName with
        | Some g ->
            for p in g.NugetPackages do
                let k = groupName,p.Name
                let package =
                    match resolution |> Map.tryFind p.Name with
                    | Some p -> p
                    | None -> failwithf "Error for %s: Package %O was not found in group %O of the paket.lock file." referencesFile.FileName p.Name groupName

                match package.Kind with
                | ResolvedPackageKind.DotnetCliTool ->
                    cliTools := Set.add package !cliTools
                | ResolvedPackageKind.Package ->
                    let restore =
                        match targetProfileOpt with
                        | None -> true
                        | Some targetProfile ->
                            match p.Settings.FrameworkRestrictions with
                            | Requirements.ExplicitRestriction restrictions ->
                                Requirements.isTargetMatchingRestrictions(restrictions, targetProfile)
                            | _ -> true

                    if not restore then () else
                    if usedPackageKeys.Contains k then
                        failwithf "Package %O is referenced more than once in %s within group %O." p.Name referencesFile.FileName groupName

                    usedPackageKeys.Add k |> ignore

                    let deps = this.GetDirectDependenciesOfSafe(groupName,p.Name,referencesFile.FileName)
                    toVisit := List.append toVisit.contents [(k,p,deps)]
        | None -> ()

        let visited = Dictionary<_,_>()

        while !toVisit <> List.empty do
            let current = toVisit.Value[0]
            toVisit := toVisit.Value.Tail

            let visitKey,p,deps = current
            if visited.ContainsKey(visitKey) then ()
            else
            visited.Add(visitKey,(p,HashSet(deps)))

            let groupName,_packageName = visitKey
            for dep in deps do
                let deps2 = this.GetDirectDependenciesOfSafe(groupName,dep,referencesFile.FileName)
                let packagageSettings = { p with Settings = { p.Settings with Aliases = Map.empty }}
                toVisit := List.append toVisit.contents [((groupName,dep),packagageSettings,deps2)]

        let emitted = HashSet<_>()
        [while visited.Count > 0 do
            let current =
                visited |> Seq.minBy (fun item ->
                    let _,deps = item.Value;
                    (deps.Count,item.Key))

            let groupName,packageName = current.Key

            visited.Remove(current.Key) |> ignore

            for item in visited do
                let itemGroup, _ = item.Key
                if itemGroup = groupName then
                    let _, itemDeps = item.Value
                    itemDeps.Remove(packageName) |> ignore
                else ()

            if emitted.Add current.Key then
                let settings,dependencies = current.Value
                let deps = Set.ofSeq dependencies
                yield (current.Key,settings,deps)
        ], !cliTools

    member this.GetOrderedPackageHull(groupName,referencesFile:ReferencesFile) =
        this.GetOrderedPackageHull(groupName,referencesFile,None)

    member this.GetPackageHull(groupName,referencesFile:ReferencesFile) =
        let usedPackages = Dictionary<_,_>()

        match referencesFile.Groups |> Map.tryFind groupName with
        | Some g ->
            for p in g.NugetPackages do
                let k = groupName,p.Name
                if usedPackages.ContainsKey k then
                    failwithf "Package %O is referenced more than once in %s within group %O." p.Name referencesFile.FileName groupName
                usedPackages.Add(k,p)

            for package in g.NugetPackages do
                try
                    for d in this.GetAllDependenciesOf(groupName,package.Name,referencesFile.FileName) do
                        let k = groupName,d
                        if usedPackages.ContainsKey k |> not then
                            usedPackages.Add(k,package)
                with exn -> raise (Exception(sprintf "Error while getting all dependencies in '%s'" referencesFile.FileName, exn))
        | None -> ()

        usedPackages

    member this.GetDependencyLookupTable () =
        groups
        |> Seq.collect (fun kv ->
            kv.Value.Resolution |> Seq.map (fun kv' ->
                (kv.Key,kv'.Key),
                this.GetAllDependenciesOf(kv.Key,kv'.Value.Name,this.FileName)
                    |> Set.ofSeq
                    |> Set.remove kv'.Value.Name
        ))
        |> Map.ofSeq


    member this.GetPackageHullSafe (referencesFile, groupName) =
        match referencesFile.Groups |> Map.tryFind groupName with
        | None -> Result.Succeed Set.empty
        | Some group ->
            group.NugetPackages
            |> Seq.map (fun package ->
                this.GetAllDependenciesOfSafe(groupName,package.Name)
                |> failIfNone (ReferenceNotFoundInLockFile(referencesFile.FileName, groupName.ToString(), package.Name)))
            |> collect
            |> lift (Seq.concat >> Set.ofSeq)


    member this.GetInstalledPackageModel (QualifiedPackageName(groupName, packageName)) =
        match this.Groups |> Map.tryFind groupName with
        | None -> failwithf "Group %O can't be found in paket.lock." groupName
        | Some group ->
            match group.TryFind(packageName) with
            | None -> failwithf "Package %O is not installed in group %O." packageName groupName
            | Some resolvedPackage ->
                let folder = resolvedPackage.Folder this.RootPath groupName
                let kind =
                    match resolvedPackage.Kind with
                    | ResolvedPackageKind.Package -> InstallModelKind.Package
                    | ResolvedPackageKind.DotnetCliTool -> InstallModelKind.DotnetCliTool

                InstallModel.CreateFromContent(
                    resolvedPackage.Name,
                    resolvedPackage.Version,
                    kind,
                    FrameworkRestriction.NoRestriction,
                    NuGet.GetContent(folder).Force())


    /// Returns a list of packages inside the lockfile with their group and version number
    member this.InstalledPackages =
        this.GetGroupedResolution () |> Seq.map (fun kv ->
            let groupName,packageName = kv.Key
            groupName, packageName, kv.Value.Version
        ) |> Seq.toList


