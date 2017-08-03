namespace Paket

module DependenciesFileParser =
    open System
    open System.IO
    open Pri.LongPath
    open Requirements
    open ModuleResolver
    open Domain
    open PackageSources
    open Logging

    let private operators =
        VersionRange.BasicOperators
        @ (VersionRange.BasicOperators |> List.map (fun o -> VersionRange.StrategyOperators |> List.map (fun s -> string s + o)) |> List.concat)

    let (|NuGetStrategy|PaketStrategy|NoStrategy|) (text : string) =
        match text |> Seq.tryHead with
        | Some '!' -> NuGetStrategy
        | Some '@' -> PaketStrategy
        | _ -> NoStrategy

    let parseResolverStrategy (text : string) = 
        match text with
        | NuGetStrategy -> Some ResolverStrategy.Min
        | PaketStrategy -> Some ResolverStrategy.Max
        | NoStrategy -> None

    let twiddle (minimum:string) =
        let promote index (values:string array) =
            let parsed, number = Int32.TryParse values.[index]
            if parsed then values.[index] <- (number + 1).ToString()
            if values.Length > 1 then values.[values.Length - 1] <- "0"
            values

        let parts = minimum.Split '.'
        let penultimateItem = Math.Max(parts.Length - 2, 0)
        let promoted = parts |> promote penultimateItem
        String.Join(".", promoted)

    let parseVersionRequirement (text : string) : VersionRequirement =
        try
            let inline parsePrerelease (versions:SemVerInfo list) (texts : string list) = 
                match texts |> List.filter ((<>) "") with
                | [] -> 
                    versions
                    |> List.collect (function { PreRelease = Some x } -> [x.Name] | _ -> [])
                    |> List.distinct
                    |> function [] -> PreReleaseStatus.No | xs -> PreReleaseStatus.Concrete xs
                | [x] when String.equalsIgnoreCase x "prerelease" -> PreReleaseStatus.All
                | _ -> PreReleaseStatus.Concrete texts

            if String.IsNullOrWhiteSpace text then VersionRequirement(VersionRange.AtLeast "0",PreReleaseStatus.No) else

            match text.Split([|' '|],StringSplitOptions.RemoveEmptyEntries) |> Array.toList with
            |  ">=" :: v1 :: "<" :: v2 :: rest ->
                let v1 = SemVer.Parse v1
                let v2 = SemVer.Parse v2
                VersionRequirement(VersionRange.Range(VersionRangeBound.Including,v1,v2,VersionRangeBound.Excluding),parsePrerelease [v1; v2] rest)
            |  ">=" :: v1 :: "<=" :: v2 :: rest ->
                let v1 = SemVer.Parse v1
                let v2 = SemVer.Parse v2
                VersionRequirement(VersionRange.Range(VersionRangeBound.Including,v1,v2,VersionRangeBound.Including),parsePrerelease [v1; v2] rest)
            |  "~>" :: v1 :: ">=" :: v2 :: rest -> 
                let v1 = SemVer.Parse(twiddle v1)
                let v2 = SemVer.Parse v2
                VersionRequirement(VersionRange.Range(VersionRangeBound.Including,v2,v1,VersionRangeBound.Excluding),parsePrerelease [v1; v2] rest)
            |  "~>" :: v1 :: ">" :: v2 :: rest ->
                let v1 = SemVer.Parse(twiddle v1)
                let v2 = SemVer.Parse v2
                VersionRequirement(VersionRange.Range(VersionRangeBound.Excluding,v2,v1,VersionRangeBound.Excluding),parsePrerelease [v1; v2] rest)
            |  ">" :: v1 :: "<" :: v2 :: rest -> 
                let v1 = SemVer.Parse v1
                let v2 = SemVer.Parse v2
                VersionRequirement(VersionRange.Range(VersionRangeBound.Excluding,v1,v2,VersionRangeBound.Excluding),parsePrerelease [v1; v2] rest)
            |  ">" :: v1 :: "<=" :: v2 :: rest ->
                let v1 = SemVer.Parse v1
                let v2 = SemVer.Parse v2
                VersionRequirement(VersionRange.Range(VersionRangeBound.Excluding,v1,v2,VersionRangeBound.Including),parsePrerelease [v1; v2] rest)
            | _ -> 
                let splitVersion (text:string) =
                    match VersionRange.BasicOperators |> List.tryFind(text.StartsWith) with
                    | Some token -> token, text.Replace(token + " ", "").Split ' ' |> Array.toList
                    | None -> "=", text.Split ' ' |> Array.toList

            
                match splitVersion text with
                | "==", version :: rest -> 
                    let v = SemVer.Parse version
                    VersionRequirement(VersionRange.OverrideAll v,parsePrerelease [v] rest)
                | ">=", version :: rest -> 
                    let v = SemVer.Parse version
                    VersionRequirement(VersionRange.Minimum v,parsePrerelease [v] rest)
                | ">", version :: rest -> 
                    let v = SemVer.Parse version
                    VersionRequirement(VersionRange.GreaterThan v,parsePrerelease [v] rest)
                | "<", version :: rest -> 
                    let v = SemVer.Parse version
                    VersionRequirement(VersionRange.LessThan v,parsePrerelease [v] rest)
                | "<=", version :: rest -> 
                    let v = SemVer.Parse version
                    VersionRequirement(VersionRange.Maximum v,parsePrerelease [v] rest)
                | "~>", minimum :: rest -> 
                    let v1 = SemVer.Parse minimum
                    VersionRequirement(VersionRange.Between(minimum,twiddle minimum),parsePrerelease [v1] rest)
                | _, version :: rest -> 
                    let v = SemVer.Parse version
                    VersionRequirement(VersionRange.Specific v,parsePrerelease [v] rest)
                | _ -> failwithf "could not parse version range \"%s\"" text
        with
        | _ -> failwithf "could not parse version range \"%s\"" text

    let parseDependencyLine (line:string) =
        let rec parseDepLine start acc =
            if start >= line.Length then acc
            else
                match line.[start] with
                | ' ' -> parseDepLine (start+1) acc
                | '"' ->
                    match line.IndexOf('"', start+1) with
                    | -1  -> failwithf "Unclosed quote in line '%s'" line
                    | ind -> parseDepLine (ind+1) (line.Substring(start+1, ind-start-1)::acc)
                | _ ->
                    match line.IndexOf(' ', start+1) with
                    | -1  -> line.Substring(start)::acc
                    | ind -> parseDepLine (ind+1) (line.Substring(start, ind-start)::acc)

        parseDepLine 0 []
        |> List.rev
        |> List.toArray


    let private parseGitSource trimmed origin originTxt = 
        let parts = parseDependencyLine trimmed
        
        let getParts (projectSpec : string) = 
            match projectSpec.Split [| ':'; '/' |] with
            | [| owner; project |] -> owner, project, None
            | [| owner; project; commit |] -> owner, project, Some commit
            | _ -> failwithf "invalid %s specification:%s     %s" originTxt Environment.NewLine trimmed
        match parts with
        | [| _; projectSpec; fileSpec; authKey |] -> origin, getParts projectSpec, fileSpec, (Some authKey)
        | [| _; projectSpec; fileSpec |] -> origin, getParts projectSpec, fileSpec, None
        | [| _; projectSpec |] -> origin, getParts projectSpec, Constants.FullProjectSourceFileName, None
        | _ -> failwithf "invalid %s specification:%s     %s" originTxt Environment.NewLine trimmed


    let private parseHttpSource trimmed = 
        let parts = parseDependencyLine trimmed
        
        let getParts (projectSpec : string) fileSpec projectName authKey = 
            let projectSpec = projectSpec.TrimEnd '/'
            
            let projectSpec', commit =
                let start = 
                    match projectSpec.IndexOf "://" with
                    | -1 -> 8 // 8 = "https://".Length
                    | pos -> pos + 3
             
                match projectSpec.IndexOf('/', start) with 
                | -1 -> projectSpec, "/"
                | pos -> projectSpec.Substring(0, pos), projectSpec.Substring pos
            
            let splitted = projectSpec.TrimEnd('/').Split([| ':'; '/' |], StringSplitOptions.RemoveEmptyEntries)
            
            let removeQueryString (s:string) = 
                match s.IndexOf '?' with
                | -1 -> s
                | pos -> s.Substring(0, pos)
             
            let fileName = 
                if String.IsNullOrEmpty fileSpec then
                    let name = splitted |> Seq.last |> removeQueryString
                    if String.IsNullOrEmpty <| Path.GetExtension name then name + ".fs"
                    else name
                else fileSpec
            
            let owner = 
                match projectSpec'.IndexOf "://" with
                | -1 -> projectSpec'
                | pos -> projectSpec'.Substring(pos + 3) |> removeInvalidChars
            
            HttpLink(projectSpec'), (owner, projectName, Some commit), fileName, authKey

        match parts with
        | [| _spec; url |] -> getParts url "" "" None
        | [| _spec; url; fileSpec |] -> getParts url fileSpec "" None
        | [| _spec; url; fileSpec; authKey |] -> getParts url fileSpec "" (Some authKey)
        | _ -> failwithf "invalid http-reference specification:%s     %s" Environment.NewLine trimmed

    type private ParserOption =
    | ReferencesMode of bool
    | OmitContent of ContentCopySettings
    | FrameworkRestrictions of FrameworkRestrictions
    | AutodetectFrameworkRestrictions
    | ImportTargets of bool
    | CopyLocal of bool
    | SpecificVersion of bool
    | CopyContentToOutputDir of CopyToOutputDirectorySettings
    | GenerateLoadScripts of bool option
    | ReferenceCondition of string
    | Redirects of bool option
    | ResolverStrategyForTransitives of ResolverStrategy option
    | ResolverStrategyForDirectDependencies of ResolverStrategy option

    type RemoteParserOption =
    | PackageSource of PackageSource
    | Cache of Cache

    let private (|Remote|_|) (line:string) =
        match line.Trim()  with
        | String.RemovePrefix "source" _ as trimmed -> 
            try 
                let source = PackageSource.Parse trimmed
                Some (Remote (RemoteParserOption.PackageSource source))
            with e -> 
                traceWarnfn "could not parse package source %s (%s)" trimmed e.Message
                reraise ()
        | String.RemovePrefix "cache" _ as trimmed -> Some (Remote (RemoteParserOption.Cache (Cache.Parse trimmed)))
        | _ -> None
        
    let private (|Package|_|) (line:string) =        
        match line.Trim() with
        | String.RemovePrefix "nuget" trimmed -> 
            let parts = trimmed.Trim().Replace("\"", "").Split([|' '|],StringSplitOptions.RemoveEmptyEntries) |> Seq.toList

            let isVersion(text:string) = 
                let (result,_) = Int32.TryParse(text.[0].ToString()) in result
           
            match parts with
            | name :: operator1 :: version1  :: operator2 :: version2 :: rest
                when List.exists ((=) operator1) operators && List.exists ((=) operator2) operators -> 
                Some (Package(name,operator1+" "+version1+" "+operator2+" "+version2, String.Join(" ",rest) |> removeComment))
            | name :: operator :: version  :: rest 
                when List.exists ((=) operator) operators ->
                Some (Package(name,operator + " " + version, String.Join(" ",rest) |> removeComment))
            | name :: version :: rest when isVersion version -> 
                Some (Package(name,version,String.Join(" ",rest) |> removeComment))
            | name :: rest -> Some (Package(name,">= 0", String.Join(" ",rest) |> removeComment))
            | [name] -> Some (Package(name,">= 0",""))
            | _ -> failwithf "could not retrieve NuGet package from %s" trimmed
        | _ -> None

    let private (|CliTool|_|) (line:string) =        
        match line.Trim() with
        | String.RemovePrefix "clitool" trimmed -> 
            let parts = trimmed.Trim().Replace("\"", "").Split([|' '|],StringSplitOptions.RemoveEmptyEntries) |> Seq.toList

            let isVersion(text:string) = 
                let (result,_) = Int32.TryParse(text.[0].ToString()) in result
           
            match parts with
            | name :: operator1 :: version1  :: operator2 :: version2 :: rest
                when List.exists ((=) operator1) operators && List.exists ((=) operator2) operators -> 
                Some (CliTool(name,operator1+" "+version1+" "+operator2+" "+version2, String.Join(" ",rest) |> removeComment))
            | name :: operator :: version  :: rest 
                when List.exists ((=) operator) operators ->
                Some (CliTool(name,operator + " " + version, String.Join(" ",rest) |> removeComment))
            | name :: version :: rest when isVersion version -> 
                Some (CliTool(name,version,String.Join(" ",rest) |> removeComment))
            | name :: rest -> Some (CliTool(name,">= 0", String.Join(" ",rest) |> removeComment))
            | [name] -> Some (CliTool(name,">= 0",""))
            | _ -> failwithf "could not retrieve cli tool from %s" trimmed
        | _ -> None
    
    let private (|Empty|_|) (line:string) =
        match line.Trim()  with
        | _ when String.IsNullOrWhiteSpace line -> Some (Empty line)
        | String.RemovePrefix "version" _ as trimmed -> Some (Empty trimmed) // Parsed by the boostrapper, not paket itself
        | String.RemovePrefix "//" _ -> Some (Empty line)
        | String.RemovePrefix "#" _ -> Some (Empty line)
        | _ -> None
        
    let private (|ParserOptions|_|) (line:string) =
        match line.Trim() with
        | String.RemovePrefix "references" trimmed -> Some (ParserOptions (ParserOption.ReferencesMode (trimmed.Replace(":","").Trim() = "strict")))
        | String.RemovePrefix "redirects" trimmed ->
            let setting =
                match trimmed.Replace(":","").Trim() with
                | String.EqualsIC "on" -> Some true
                | String.EqualsIC "off" -> Some false
                | _ -> None

            Some (ParserOptions (ParserOption.Redirects setting))
        | String.RemovePrefix "strategy" trimmed -> 
            let setting =
                match trimmed.Replace(":","").Trim() with
                | String.EqualsIC "max" -> Some ResolverStrategy.Max
                | String.EqualsIC "min" -> Some ResolverStrategy.Min
                | _ -> None

            Some (ParserOptions (ParserOption.ResolverStrategyForTransitives setting))
        | String.RemovePrefix "lowest_matching" trimmed -> 
            let setting =
                match trimmed.Replace(":","").Trim() with
                | String.EqualsIC "false" -> Some ResolverStrategy.Max
                | String.EqualsIC "true" -> Some ResolverStrategy.Min
                | _ -> None

            Some (ParserOptions (ParserOption.ResolverStrategyForDirectDependencies setting))
        | String.RemovePrefix "framework" trimmed -> 
            let text = trimmed.Replace(":", "").Trim()
            
            if text = "auto-detect" then 
                Some (ParserOptions (ParserOption.AutodetectFrameworkRestrictions))
            else 
                let restrictions = Requirements.parseRestrictionsLegacy true text
                if String.IsNullOrWhiteSpace text |> not && restrictions = FrameworkRestriction.NoRestriction then 
                    failwithf "Could not parse framework restriction \"%s\"" text

                let options = ParserOption.FrameworkRestrictions (ExplicitRestriction restrictions)
                Some (ParserOptions options)
        | String.RemovePrefix "restriction" trimmed -> 
            let text = trimmed.Replace(":", "").Trim()
            
            if text = "auto-detect" then 
                Some (ParserOptions (ParserOption.AutodetectFrameworkRestrictions))
            else 
                let restrictions = Requirements.parseRestrictions true text
                if String.IsNullOrWhiteSpace text |> not && restrictions = FrameworkRestriction.NoRestriction then 
                    failwithf "Could not parse framework restriction \"%s\"" text

                let options = ParserOption.FrameworkRestrictions (ExplicitRestriction restrictions)
                Some (ParserOptions options)
        | String.RemovePrefix "content" trimmed -> 
            let setting =
                match trimmed.Replace(":","").Trim() with
                | String.EqualsIC "none" -> ContentCopySettings.Omit
                | String.EqualsIC "once" -> ContentCopySettings.OmitIfExisting
                | _ -> ContentCopySettings.Overwrite

            Some (ParserOptions (ParserOption.OmitContent setting))
        | String.RemovePrefix "import_targets" trimmed -> Some (ParserOptions (ParserOption.ImportTargets(trimmed.Replace(":","").Trim() = "true")))
        | String.RemovePrefix "copy_local" trimmed -> Some (ParserOptions (ParserOption.CopyLocal(trimmed.Replace(":","").Trim() = "true")))
        | String.RemovePrefix "specific_version" trimmed -> Some (ParserOptions (ParserOption.SpecificVersion(trimmed.Replace(":","").Trim() = "true")))
        | String.RemovePrefix "copy_content_to_output_dir" trimmed -> 
            let setting =
                match trimmed.Replace(":","").Trim() with
                | String.EqualsIC "always" -> CopyToOutputDirectorySettings.Always
                | String.EqualsIC "never" -> CopyToOutputDirectorySettings.Never
                | String.EqualsIC "preserve_newest" -> CopyToOutputDirectorySettings.PreserveNewest
                | x -> failwithf "Unknown copy_content_to_output_dir settings: %A" x
                        
            Some (ParserOptions (ParserOption.CopyContentToOutputDir setting))
        | String.RemovePrefix "condition" trimmed -> Some (ParserOptions(ParserOption.ReferenceCondition(trimmed.Replace(":","").Trim().ToUpper())))
        | String.RemovePrefix "generate_load_scripts" trimmed ->
            let setting =
                match trimmed.Replace(":","").Trim() with
                | String.EqualsIC "on"  | String.EqualsIC "true"  -> Some true
                | String.EqualsIC "off" | String.EqualsIC "false" -> Some false
                | _ -> None
            Some (ParserOptions (ParserOption.GenerateLoadScripts setting))
        | _ -> None
        
    let private (|SourceFile|_|) (line:string) =        
        match line.Trim() with
        | String.RemovePrefix "gist" _ as trimmed ->
            Some (SourceFile (parseGitSource trimmed Origin.GistLink "gist"))
        | String.RemovePrefix "github" _ as trimmed  ->
            Some (SourceFile (parseGitSource trimmed Origin.GitHubLink "github"))
        | String.RemovePrefix "http" _ as trimmed  ->
            Some (SourceFile (parseHttpSource trimmed))
        | _ -> None 

    let private (|Git|_|) (line:string) =        
        match line.Trim() with
        | String.RemovePrefix "git" _ as trimmed  ->
            Some (Git(trimmed.Substring 4))
        | String.RemovePrefix "file:" _ as trimmed  ->
            Some (Git trimmed)
        | _ -> None
        
    let private (|Group|_|) (line:string) =        
        match line.Trim()  with
        | String.RemovePrefix "group" _ as trimmed -> Some (Group (trimmed.Replace("group ","")))
        | _ -> None

    let parsePackage (sources,parent,name,version,isCliTool,rest:string) =
        let prereleases,optionsText =
            if rest.Contains ":" then
                // boah that's reaaaally ugly, but keeps backwards compat
                let pos = rest.IndexOf ':'
                let s = rest.Substring(0,pos).TrimEnd()
                let pos' = s.LastIndexOf ' '
                let prereleases = if pos' > 0 then s.Substring(0,pos') else ""
                let s' = if prereleases <> "" then rest.Replace(prereleases,"") else rest
                prereleases,s'
            else
                rest,""

        if operators |> Seq.exists prereleases.Contains || prereleases.Contains("!") then
            failwithf "Invalid prerelease version %s" prereleases

        let packageName = PackageName name

        let vr = (version + " " + prereleases).Trim(VersionRange.StrategyOperators |> Array.ofList)
        let versionRequirement = parseVersionRequirement vr

        { Name = packageName
          ResolverStrategyForTransitives = 
            if optionsText.Contains "strategy" then 
                let kvPairs = parseKeyValuePairs optionsText
                match kvPairs.TryGetValue "strategy" with
                | true, "max" -> Some ResolverStrategy.Max 
                | true, "min" -> Some ResolverStrategy.Min
                | _ -> parseResolverStrategy version
            else parseResolverStrategy version 
          ResolverStrategyForDirectDependencies = 
            if optionsText.Contains "lowest_matching" then 
                let kvPairs = parseKeyValuePairs optionsText
                match kvPairs.TryGetValue "lowest_matching" with
                | true, "false" -> Some ResolverStrategy.Max 
                | true, "true" -> Some ResolverStrategy.Min
                | _ -> None
            else None 
          Parent = parent
          Graph = Set.empty
          Sources = sources
          Settings = InstallSettings.Parse(optionsText).AdjustWithSpecialCases packageName
          TransitivePrereleases = versionRequirement.PreReleases <> PreReleaseStatus.No
          VersionRequirement = versionRequirement 
          IsCliTool = isCliTool } 

    let parsePackageLine(sources,parent,line:string) =
        match line with 
        | Package(name,version,rest) -> parsePackage(sources,parent,name,version,false,rest)
        | CliTool(name,version,rest) -> parsePackage(sources,parent,name,version,true,rest)
        | _ -> failwithf "Not a package line: %s" line

    let private parseOptions (current  : DependenciesGroup) options =
        match options with 
        | ReferencesMode mode                            -> { current.Options with Strict = mode } 
        | Redirects mode                                 -> { current.Options with Redirects = mode }
        | ResolverStrategyForTransitives strategy        -> { current.Options with ResolverStrategyForTransitives = strategy }
        | ResolverStrategyForDirectDependencies strategy -> { current.Options with ResolverStrategyForDirectDependencies = strategy }
        | CopyLocal mode                                 -> { current.Options with Settings = { current.Options.Settings with CopyLocal = Some mode } }
        | SpecificVersion mode                           -> { current.Options with Settings = { current.Options.Settings with SpecificVersion = Some mode } }
        | CopyContentToOutputDir mode                    -> { current.Options with Settings = { current.Options.Settings with CopyContentToOutputDirectory = Some mode } }
        | ImportTargets mode                             -> { current.Options with Settings = { current.Options.Settings with ImportTargets = Some mode } }
        | FrameworkRestrictions r                        -> { current.Options with Settings = { current.Options.Settings with FrameworkRestrictions = r } }
        | AutodetectFrameworkRestrictions                -> { current.Options with Settings = { current.Options.Settings with FrameworkRestrictions = AutoDetectFramework } }
        | OmitContent omit                               -> { current.Options with Settings = { current.Options.Settings with OmitContent = Some omit } }
        | ReferenceCondition condition                   -> { current.Options with Settings = { current.Options.Settings with ReferenceCondition = Some condition } }
        | GenerateLoadScripts mode                       -> { current.Options with Settings = { current.Options.Settings with GenerateLoadScripts = mode }}

    let private parseLine fileName checkDuplicates (lineNo, state: DependenciesGroup list) line =
        match state with
        | current::other ->
            let lineNo = lineNo + 1
            try
                match line with
                | Group newGroupName -> 
                    let newGroups =
                        let newGroupName = GroupName newGroupName
                        if current.Name = newGroupName then current::other else
                        match other |> List.tryFind (fun g -> g.Name = newGroupName) with
                        | Some g -> g::current::(other |> List.filter (fun g -> g.Name <> newGroupName))
                        | None -> DependenciesGroup.New(newGroupName)::current::other
                    lineNo,newGroups
                | Empty _ -> lineNo, current::other
                | Remote (RemoteParserOption.PackageSource newSource) -> lineNo, { current with Sources = current.Sources @ [newSource] |> List.distinct }::other
                | Remote (RemoteParserOption.Cache newCache) ->                    
                    let newCache =
                        if String.IsNullOrWhiteSpace fileName then
                            newCache
                        else
                            let fi = FileInfo fileName
                            newCache.BaseOnRoot(fi.Directory.FullName)
                    let caches = current.Caches @ [newCache] |> List.distinct
                    let sources = current.Sources @ [LocalNuGet(newCache.Location,Some newCache)] |> List.distinct
                    lineNo, { current with Caches = caches; Sources = sources }::other
                | ParserOptions options ->
                    lineNo,{ current with Options = parseOptions current options} ::other

                | Package(name,version,rest) ->
                    let package = parsePackage(current.Sources,DependenciesFile fileName,name,version,false,rest) 
                    if checkDuplicates && current.Packages |> List.exists (fun p -> p.Name = package.Name) then
                        traceWarnfn "Package %O is defined more than once in group %O of %s" package.Name current.Name fileName
                    
                    lineNo, { current with Packages = current.Packages @ [package] }::other

                | CliTool(name,version,rest) ->
                    let package = parsePackage(current.Sources,DependenciesFile fileName,name,version,true,rest) 
                    if checkDuplicates && current.Packages |> List.exists (fun p -> p.Name = package.Name) then
                        traceWarnfn "Package %O is defined more than once in group %O of %s" package.Name current.Name fileName
                    
                    lineNo, { current with Packages = current.Packages @ [package] }::other

                | SourceFile(origin, (owner,project, vr), path, authKey) ->
                    let remoteFile : UnresolvedSource = { 
                        Owner = owner
                        Project = project
                        Version = 
                            match vr with
                            | None -> VersionRestriction.NoVersionRestriction
                            | Some x -> VersionRestriction.Concrete x
                        Name = path
                        Origin = origin
                        Command = None
                        OperatingSystemRestriction = None
                        PackagePath = None
                        AuthKey = authKey 
                    }
                    lineNo, { current with RemoteFiles = current.RemoteFiles @ [remoteFile] }::other
                | Git gitConfig ->
                    let owner,vr,project,origin,buildCommand,operatingSystemRestriction,packagePath = Git.Handling.extractUrlParts gitConfig
                    let remoteFile : UnresolvedSource = { 
                        Owner = owner
                        Project = project
                        Version = 
                          match vr with
                          | None -> VersionRestriction.NoVersionRestriction
                          | Some x -> 
                              try 
                                  let vr = parseVersionRequirement x
                                  VersionRestriction.VersionRequirement vr
                              with 
                              | _ -> VersionRestriction.Concrete x
                        Command = buildCommand
                        OperatingSystemRestriction = operatingSystemRestriction
                        PackagePath = packagePath
                        Name = ""
                        Origin = GitLink origin
                        AuthKey = None 
                    }
                    let sources = 
                        match packagePath with
                        | None -> current.Sources
                        | Some path -> 
                            let root = ""
                            let fullPath = remoteFile.ComputeFilePath(root,current.Name,path)
                            let relative = (createRelativePath root fullPath).Replace("\\","/")
                            LocalNuGet(relative,None) :: current.Sources |> List.distinct 
                    lineNo, { current with RemoteFiles = current.RemoteFiles @ [remoteFile]; Sources = sources }::other
                | _ -> failwithf "Unrecognized token: %s" line
            with
            | exn -> failwithf "Error in paket.dependencies line %d%s  %s" lineNo Environment.NewLine exn.Message
        | [] -> failwithf "Error in paket.dependencies line %d" lineNo

    let parseDependenciesFile fileName checkDuplicates lines =
        let groups = 
            lines
            |> Array.fold (parseLine fileName checkDuplicates) (0, [DependenciesGroup.New Constants.MainDependencyGroup])
            |> snd
            |> List.rev
            |> List.fold (fun m g ->
                match Map.tryFind g.Name m with
                | Some group -> Map.add g.Name (g.CombineWith group) m
                | None -> Map.add g.Name g m) Map.empty

        fileName, groups, lines
    
    let parseVersionString (version : string) = {   
        VersionRequirement = parseVersionRequirement (version.Trim(VersionRange.StrategyOperators |> Array.ofList))
        ResolverStrategy = parseResolverStrategy version 
    }



