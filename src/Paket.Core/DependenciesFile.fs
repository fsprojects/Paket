namespace Paket

open System
open System.IO
open Paket
open Paket.Logging
open Paket.Requirements
open Paket.ModuleResolver
open Paket.PackageResolver
open Paket.PackageSources

/// [omit]
type InstallOptions = 
    { Strict : bool 
      OmitContent : bool }

    static member Default = { Strict = false; OmitContent = false}

/// [omit]
module DependenciesFileParser = 

    let private basicOperators = ["~>";"==";"<=";">=";"=";">";"<"]
    let private operators = basicOperators @ (basicOperators |> List.map (fun o -> "!" + o))

    let parseResolverStrategy (text : string) = if text.StartsWith "!" then ResolverStrategy.Min else ResolverStrategy.Max

    let twiddle(minimum:string) =                    
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
        let parsePrerelease(texts:string seq) =
            let texts = texts |> Seq.filter ((<>) "")
            if Seq.isEmpty texts then PreReleaseStatus.No else
            if Seq.head(texts).ToLower() = "prerelease" then PreReleaseStatus.All else
            PreReleaseStatus.Concrete(texts |> Seq.toList)

        if text = "" || text = null then VersionRequirement(VersionRange.AtLeast("0"),PreReleaseStatus.No) else

        match text.Split(' ') |> Array.toList with
        |  ">=" :: v1 :: "<" :: v2 :: rest -> VersionRequirement(VersionRange.Range(VersionRangeBound.Including,SemVer.Parse v1,SemVer.Parse v2,VersionRangeBound.Excluding),parsePrerelease rest)
        |  ">=" :: v1 :: "<=" :: v2 :: rest -> VersionRequirement(VersionRange.Range(VersionRangeBound.Including,SemVer.Parse v1,SemVer.Parse v2,VersionRangeBound.Including),parsePrerelease rest)
        |  "~>" :: v1 :: ">=" :: v2 :: rest -> VersionRequirement(VersionRange.Range(VersionRangeBound.Including,SemVer.Parse v2,SemVer.Parse(twiddle v1),VersionRangeBound.Excluding),parsePrerelease rest)
        |  "~>" :: v1 :: ">" :: v2 :: rest -> VersionRequirement(VersionRange.Range(VersionRangeBound.Excluding,SemVer.Parse v2,SemVer.Parse(twiddle v1),VersionRangeBound.Excluding),parsePrerelease rest)
        |  ">" :: v1 :: "<" :: v2 :: rest -> VersionRequirement(VersionRange.Range(VersionRangeBound.Excluding,SemVer.Parse v1,SemVer.Parse v2,VersionRangeBound.Excluding),parsePrerelease rest)
        |  ">" :: v1 :: "<=" :: v2 :: rest -> VersionRequirement(VersionRange.Range(VersionRangeBound.Excluding,SemVer.Parse v1,SemVer.Parse v2,VersionRangeBound.Including),parsePrerelease rest)
        | _ -> 
            let splitVersion (text:string) =            
                match basicOperators |> List.tryFind(text.StartsWith) with
                | Some token -> token, text.Replace(token + " ", "").Split(' ') |> Array.toList
                | None -> "=", text.Split(' ') |> Array.toList

            try
                match splitVersion text with
                | "==", version :: rest -> VersionRequirement(VersionRange.OverrideAll(SemVer.Parse version),parsePrerelease rest)
                | ">=", version :: rest -> VersionRequirement(VersionRange.AtLeast(version),parsePrerelease rest)
                | ">", version :: rest -> VersionRequirement(VersionRange.GreaterThan(SemVer.Parse version),parsePrerelease rest)
                | "<", version :: rest -> VersionRequirement(VersionRange.LessThan(SemVer.Parse version),parsePrerelease rest)
                | "<=", version :: rest -> VersionRequirement(VersionRange.Maximum(SemVer.Parse version),parsePrerelease rest)
                | "~>", minimum :: rest -> VersionRequirement(VersionRange.Between(minimum,twiddle minimum),parsePrerelease rest)
                | _, version :: rest -> VersionRequirement(VersionRange.Exactly(version),parsePrerelease rest)
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

    let private (|Remote|Package|Blank|ReferencesMode|OmitContent|SourceFile|) (line:string) =
        match line.Trim() with
        | _ when String.IsNullOrWhiteSpace line -> Blank
        | trimmed when trimmed.StartsWith "source" ->
            let parts = trimmed.Split ' '
            let source = parts.[1].Replace("\"","")
            Remote (source,PackageSourceParser.parseAuth trimmed source)
        | trimmed when trimmed.StartsWith "nuget" -> 
            let parts = trimmed.Replace("nuget","").Trim().Replace("\"", "").Split([|' '|],StringSplitOptions.RemoveEmptyEntries) |> Seq.toList

            let isVersion(text:string) = 
                match Int32.TryParse(text.[0].ToString()) with
                | true,_ -> true
                | _ -> false
           
            match parts with
            | name :: operator1 :: version1  :: operator2 :: version2 :: rest
                when List.exists ((=) operator1) operators && List.exists ((=) operator2) operators -> Package(name,operator1 + " " + version1 + " " + operator2 + " " + version2 + " " + String.Join(" ",rest))
            | name :: operator :: version  :: rest 
                when List.exists ((=) operator) operators -> Package(name,operator + " " + version + " " + String.Join(" ",rest))
            | name :: version :: rest when isVersion version -> 
                Package(name,version + " " + String.Join(" ",rest))
            | name :: rest -> Package(name,">= 0 " + String.Join(" ",rest))
            | name :: [] -> Package(name,">= 0")
            | _ -> failwithf "could not retrieve nuget package from %s" trimmed
        | trimmed when trimmed.StartsWith "references" -> ReferencesMode(trimmed.Replace("references","").Trim() = "strict")
        | trimmed when trimmed.StartsWith "content" -> OmitContent(trimmed.Replace("content","").Trim() = "none")
        | trimmed when trimmed.StartsWith "github" ->
            let parts = parseDependencyLine trimmed
            let getParts (projectSpec:string) =
                match projectSpec.Split [|':'; '/'|] with
                | [| owner; project |] -> owner, project, None
                | [| owner; project; commit |] -> owner, project, Some commit
                | _ -> failwithf "invalid github specification:%s     %s" Environment.NewLine trimmed
            match parts with
            | [| _; projectSpec; fileSpec |] -> SourceFile(getParts projectSpec, fileSpec)
            | [| _; projectSpec;  |] -> SourceFile(getParts projectSpec, "FULLPROJECT")
            | _ -> failwithf "invalid github specification:%s     %s" Environment.NewLine trimmed
        | _ -> Blank
    
    let parseDependenciesFile fileName (lines:string seq) = 
        ((0, InstallOptions.Default, [], [], []), lines)
        ||> Seq.fold(fun (lineNo, options, sources: PackageSource list, packages, sourceFiles: UnresolvedSourceFile list) line ->
            let lineNo = lineNo + 1
            try
                match line with
                | Remote(newSource,auth) -> lineNo, options, sources @ [PackageSource.Parse(newSource.TrimEnd([|'/'|]),auth)], packages, sourceFiles
                | Blank -> lineNo, options, sources, packages, sourceFiles
                | ReferencesMode mode -> lineNo, { options with Strict = mode }, sources, packages, sourceFiles
                | OmitContent omit -> lineNo, { options with OmitContent = omit }, sources, packages, sourceFiles
                | Package(name,version) ->
                    lineNo, options, sources, 
                        { Sources = sources
                          Name = name
                          ResolverStrategy = parseResolverStrategy version
                          Parent = DependenciesFile fileName
                          VersionRequirement = parseVersionRequirement(version.Trim '!') } :: packages, sourceFiles
                | SourceFile((owner,project, commit), path) ->
                    lineNo, options, sources, packages, { Owner = owner; Project = project; Commit = commit; Name = path } :: sourceFiles
                    
            with
            | exn -> failwithf "Error in paket.dependencies line %d%s  %s" lineNo Environment.NewLine exn.Message)
        |> fun (_,options,_,packages,remoteFiles) ->
            fileName,
            options,
            packages |> List.rev,
            remoteFiles |> List.rev

module DependenciesFileSerializer = 
    let formatVersionRange strategy (version : VersionRequirement) : string =          
        let prefix = 
            if strategy = ResolverStrategy.Min then "!"
            else ""

        let preReleases = 
            match version.PreReleases with
            | No -> ""
            | PreReleaseStatus.All -> "prerelease"
            | Concrete list -> String.Join(" ",list)
            
        let version = 
            match version.Range with
            | Minimum x when strategy = ResolverStrategy.Max && x = SemVer.Parse "0" -> ""
            | Minimum x -> ">= " + x.ToString()
            | GreaterThan x -> "> " + x.ToString()
            | Specific x -> x.ToString()
            | VersionRange.Range(_, from, _, _) 
                    when DependenciesFileParser.parseVersionRequirement ("~> " + from.ToString() + preReleases) = version -> 
                        "~> " + from.ToString()
            | _ -> version.ToString()
            
        let text = prefix + version         
        if text <> "" && preReleases <> "" then text + " " + preReleases else text + preReleases

/// Allows to parse and analyze paket.dependencies files.
type DependenciesFile(fileName,options,packages : PackageRequirement list, remoteFiles : UnresolvedSourceFile list) = 
    let packages = packages |> Seq.toList
    let dependencyMap = Map.ofSeq (packages |> Seq.map (fun p -> p.Name, p.VersionRequirement))
    
    let sources =
        packages 
        |> Seq.map (fun p -> p.Sources)
        |> Seq.concat
        |> Set.ofSeq
        |> Set.toList
            
    member __.DirectDependencies = dependencyMap
    member __.Packages = packages
    member __.HasPackage (name : string) = packages |> List.exists (fun p -> p.Name.ToLower() = name.ToLower())
    member __.RemoteFiles = remoteFiles
    member __.Options = options
    member __.FileName = fileName
    member __.Sources = sources
    member this.Resolve(force) = 
        let getSha1 owner repo branch = GitHub.getSHA1OfBranch owner repo branch |> Async.RunSynchronously
        this.Resolve(getSha1,Nuget.GetVersions,Nuget.GetPackageDetails force)

    member __.Resolve(getSha1,getVersionF, getPackageDetailsF) =
        let resolveSourceFile(file:ResolvedSourceFile) : PackageRequirement list =
            GitHub.downloadDependenciesFile(Path.GetDirectoryName fileName, file)
            |> Async.RunSynchronously
            |> DependenciesFile.FromCode
            |> fun df -> df.Packages

        let remoteFiles = ModuleResolver.Resolve(resolveSourceFile,getSha1,remoteFiles)
        
        let remoteDependencies = 
            remoteFiles
            |> List.map (fun f -> f.Dependencies)
            |> List.fold (fun set current -> Set.union set current) Set.empty
            |> Seq.map (fun (n, v) -> 
                   let p = packages |> Seq.last
                   { p with Name = n
                            VersionRequirement = v })
            |> Seq.toList

        { ResolvedPackages = PackageResolver.Resolve(getVersionF, getPackageDetailsF, remoteDependencies @ packages)
          ResolvedSourceFiles = remoteFiles }        

    member __.AddAdditionionalPackage(packageName:string,version:string) =
        let versionRange = DependenciesFileParser.parseVersionRequirement (version.Trim '!')
        let sources = 
            match packages |> List.rev with
            | lastPackage::_ -> lastPackage.Sources
            | [] -> [PackageSources.DefaultNugetSource]

        let newPackage = 
            { Name = packageName
              VersionRequirement = versionRange
              Sources = sources
              ResolverStrategy = DependenciesFileParser.parseResolverStrategy version
              Parent = PackageRequirementSource.DependenciesFile fileName }

        DependenciesFile(fileName,options,packages @ [newPackage], remoteFiles)

    member __.AddFixedPackage(packageName:string,version:string) =
        let versionRange = DependenciesFileParser.parseVersionRequirement (version.Trim '!')
        let sources = 
            match packages |> List.rev with
            | lastPackage::_ -> lastPackage.Sources
            | [] -> [PackageSources.DefaultNugetSource]

        let strategy = 
            match packages |> List.tryFind (fun p -> p.Name.ToLower() = packageName.ToLower()) with
            | Some package -> package.ResolverStrategy
            | None -> DependenciesFileParser.parseResolverStrategy version

        let newPackage = 
            { Name = packageName
              VersionRequirement = versionRange
              Sources = sources
              ResolverStrategy = strategy
              Parent = PackageRequirementSource.DependenciesFile fileName }

        DependenciesFile(fileName,options,(packages |> List.filter (fun p -> p.Name.ToLower() <> packageName.ToLower())) @ [newPackage], remoteFiles)

    member __.RemovePackage(packageName:string) =
        let newPackages = 
            packages
            |> List.filter (fun p -> p.Name.ToLower() <> packageName.ToLower())

        DependenciesFile(fileName,options,newPackages,remoteFiles)

    member this.Add(packageName,version:string) =
        if this.HasPackage packageName then 
            traceWarnfn "%s contains package %s already. ==> Ignored" fileName packageName
            this
        else
            if version = "" then
                tracefn "Adding %s to %s" packageName fileName
            else
                tracefn "Adding %s %s to %s" packageName version fileName
            this.AddAdditionionalPackage(packageName,version)

    member this.Remove(packageName) =
        if this.HasPackage packageName then         
            tracefn "Removing %s from %s" packageName fileName
            this.RemovePackage(packageName)
        else
            traceWarnfn "%s doesn't contain package %s. ==> Ignored" fileName packageName
            this

    member this.UpdatePackageVersion(packageName, version) =
        if this.HasPackage(packageName) then
            let versionRequirement = DependenciesFileParser.parseVersionRequirement version
            tracefn "Updating %s version to %s in %s" packageName version fileName
            let packages = 
                this.Packages |> List.map (fun p -> 
                                     if p.Name.ToLower() = packageName.ToLower() then 
                                         { p with VersionRequirement = versionRequirement }
                                     else p)
            DependenciesFile(this.FileName, this.Options, packages, this.RemoteFiles)
        else
            traceWarnfn "%s doesn't contain package %s. ==> Ignored" fileName packageName
            this

    override __.ToString() =        
        let sources = 
            packages
            |> Seq.map (fun package -> package.Sources,package)
            |> Seq.groupBy fst

        let all =
            let hasReportedSource = ref false
            let hasReportedFirst = ref false
            let hasReportedSecond = ref false
            [ if options.Strict then yield "references strict"
              if options.OmitContent then yield "content none"
              for sources, packages in sources do
                  for source in sources do
                      hasReportedSource := true
                      match source with
                      | Nuget source -> 
                        match source.Auth with
                        | None -> yield "source " + source.Url 
                        | Some auth -> yield sprintf "source %s username: \"%s\" password: \"%s\"" source.Url <| auth.Username.Original <| auth.Password.Original
                        
                      | LocalNuget source -> yield "source " + source
                  
                  for _,package in packages do
                      if (not !hasReportedFirst) && !hasReportedSource  then
                          yield ""
                          hasReportedFirst := true

                      let version = DependenciesFileSerializer.formatVersionRange package.ResolverStrategy package.VersionRequirement
                      yield sprintf "nuget %s%s" package.Name (if version <> "" then " " + version else "")
                     
              for remoteFile in remoteFiles do
                  if (not !hasReportedSecond) && !hasReportedFirst then
                      yield ""
                      hasReportedSecond := true

                  yield sprintf "github %s" (remoteFile.ToString())]
                  
        String.Join(Environment.NewLine, all)                                 

    member this.Save() =
        File.WriteAllText(fileName, this.ToString())
        tracefn "Dependencies files saved to %s" fileName

    static member FromCode(code:string) : DependenciesFile = 
        DependenciesFile(DependenciesFileParser.parseDependenciesFile "" <| code.Replace("\r\n","\n").Replace("\r","\n").Split('\n'))

    static member ReadFromFile fileName : DependenciesFile = 
        verbosefn "Parsing %s" fileName
        DependenciesFile(DependenciesFileParser.parseDependenciesFile fileName <| File.ReadAllLines fileName)

    /// Find the matching lock file to a dependencies file
    static member FindLockfile(dependenciesFileName) =
        let fi = FileInfo(dependenciesFileName)
        FileInfo(Path.Combine(fi.Directory.FullName, fi.Name.Replace(fi.Extension,"") + ".lock"))

    /// Find the matching lock file to a dependencies file
    member this.FindLockfile() = DependenciesFile.FindLockfile this.FileName