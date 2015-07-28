namespace Paket

open System
open System.IO
open Paket
open Paket.Domain
open Paket.Logging
open Paket.Requirements
open Paket.ModuleResolver
open Paket.PackageResolver
open Paket.PackageSources

/// [omit]
type InstallOptions = 
    { Strict : bool 
      Redirects : bool
      Settings : InstallSettings }

    static member Default = { 
        Strict = false
        Redirects = false
        Settings = InstallSettings.Default }

type VersionStrategy = {
    VersionRequirement : VersionRequirement
    ResolverStrategy : ResolverStrategy }
            
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


    let private ``parse git source`` trimmed origin originTxt = 
        let parts = parseDependencyLine trimmed
        let getParts (projectSpec:string) =
            match projectSpec.Split [|':'; '/'|] with
            | [| owner; project |] -> owner, project, None
            | [| owner; project; commit |] -> owner, project, Some commit
            | _ -> failwithf "invalid %s specification:%s     %s" originTxt Environment.NewLine trimmed
        match parts with
        | [| _; projectSpec; fileSpec |] -> origin, getParts projectSpec, fileSpec
        | [| _; projectSpec;  |] -> origin, getParts projectSpec, Constants.FullProjectSourceFileName
        | _ -> failwithf "invalid %s specification:%s     %s" originTxt Environment.NewLine trimmed

    let private ``parse http source`` trimmed =
        let parts = parseDependencyLine trimmed
        let getParts (projectSpec:string) fileSpec =
            let projectSpec = projectSpec.TrimEnd('/')
            let ``project spec``, commit =
                match projectSpec.IndexOf('/', 8) with // 8 = "https://".Length
                | -1 -> projectSpec, "/"
                | pos ->  projectSpec.Substring(0, pos), projectSpec.Substring(pos)
            let splitted = projectSpec.TrimEnd('/').Split([|':'; '/'|], StringSplitOptions.RemoveEmptyEntries)
            let fileName = match String.IsNullOrEmpty fileSpec with
                            | true ->
                                let name = Seq.last splitted
                                if String.IsNullOrEmpty <| Path.GetExtension(name)
                                then name + ".fs" else name
                            | false -> fileSpec
            let owner =
                match ``project spec``.IndexOf("://") with
                | -1 -> ``project spec``
                | pos ->  ``project spec``.Substring(pos+3)
            HttpLink(``project spec``), (owner, "", Some commit), fileName
        match parts with
        | [| _; projectSpec; |] -> getParts projectSpec String.Empty
        | [| _; projectSpec; fileSpec |] -> getParts projectSpec fileSpec
        | _ -> failwithf "invalid http-reference specification:%s     %s" Environment.NewLine trimmed

    type private ParserOption =
    | ReferencesMode of bool
    | OmitContent of bool
    | FrameworkRestrictions of FrameworkRestrictions
    | ImportTargets of bool
    | CopyLocal of bool
    | Redirects of bool

    let private (|Remote|Package|Empty|ParserOptions|SourceFile|) (line:string) =
        match line.Trim() with
        | _ when String.IsNullOrWhiteSpace line -> Empty(line)
        | String.StartsWith "source" _ as trimmed -> Remote(PackageSource.Parse(trimmed))
        | String.StartsWith "nuget" trimmed -> 
            let parts = trimmed.Trim().Replace("\"", "").Split([|' '|],StringSplitOptions.RemoveEmptyEntries) |> Seq.toList

            let isVersion(text:string) = 
                match Int32.TryParse(text.[0].ToString()) with
                | true,_ -> true
                | _ -> false
           
            match parts with
            | name :: operator1 :: version1  :: operator2 :: version2 :: rest
                when List.exists ((=) operator1) operators && List.exists ((=) operator2) operators -> 
                Package(name,operator1 + " " + version1 + " " + operator2 + " " + version2, String.Join(" ",rest))
            | name :: operator :: version  :: rest 
                when List.exists ((=) operator) operators ->
                Package(name,operator + " " + version, String.Join(" ",rest))
            | name :: version :: rest when isVersion version -> 
                Package(name,version,String.Join(" ",rest))
            | name :: rest -> Package(name,">= 0", String.Join(" ",rest))
            | [name] -> Package(name,">= 0","")
            | _ -> failwithf "could not retrieve nuget package from %s" trimmed
        | String.StartsWith "references" trimmed -> ParserOptions(ParserOption.ReferencesMode(trimmed.Replace(":","").Trim() = "strict"))
        | String.StartsWith "redirects" trimmed -> ParserOptions(ParserOption.Redirects(trimmed.Replace(":","").Trim() = "on"))
        | String.StartsWith "framework" trimmed -> ParserOptions(ParserOption.FrameworkRestrictions(trimmed.Replace(":","").Trim() |> Requirements.parseRestrictions))
        | String.StartsWith "content" trimmed -> ParserOptions(ParserOption.OmitContent(trimmed.Replace(":","").Trim() = "none"))
        | String.StartsWith "import_targets" trimmed -> ParserOptions(ParserOption.ImportTargets(trimmed.Replace(":","").Trim() = "true"))
        | String.StartsWith "copy_local" trimmed -> ParserOptions(ParserOption.CopyLocal(trimmed.Replace(":","").Trim() = "true"))
        | String.StartsWith "gist" _ as trimmed ->
            SourceFile(``parse git source`` trimmed SingleSourceFileOrigin.GistLink "gist")
        | String.StartsWith "github" _ as trimmed  ->
            SourceFile(``parse git source`` trimmed SingleSourceFileOrigin.GitHubLink "github")
        | String.StartsWith "http" _ as trimmed  ->
            SourceFile(``parse http source`` trimmed)
        | String.StartsWith "//" _ -> Empty(line)
        | String.StartsWith "#" _ -> Empty(line)
        | _ -> failwithf "Unrecognized token: %s" line
    
    let parsePackage(sources,parent,name,version,rest:string) =
        let prereleases,optionsText =
            if rest.Contains ":" then
                // boah that's reaaaally ugly, but keeps backwards compat
                let pos = rest.IndexOf ':'
                let s = rest.Substring(0,pos).TrimEnd()
                let pos' = s.LastIndexOf(' ')
                let prereleases = if pos' > 0 then s.Substring(0,pos') else ""
                let s' = if prereleases <> "" then rest.Replace(prereleases,"") else rest
                prereleases,s'
            else
                rest,""

        if operators |> Seq.exists (fun x -> prereleases.Contains x) || prereleases.Contains("!") then
            failwithf "Invalid prerelease version %s" prereleases

        { Sources = sources
          Name = PackageName name
          ResolverStrategy = parseResolverStrategy version
          Parent = parent
          Settings = InstallSettings.Parse(optionsText)
          VersionRequirement = parseVersionRequirement((version + " " + prereleases).Trim '!') } 

    let parsePackageLine(sources,parent,line:string) =
        match line with 
        | Package(name,version,rest) -> parsePackage(sources,parent,name,version,rest)
        | _ -> failwithf "Not a package line: %s" line

    let parseDependenciesFile fileName (lines:string seq) =
        let lines = lines |> Seq.toArray
         
        ((0, InstallOptions.Default, [], [], []), lines)
        ||> Seq.fold(fun (lineNo, options, sources: PackageSource list, packages, sourceFiles: UnresolvedSourceFile list) line ->
            let lineNo = lineNo + 1
            try
                match line with
                | Empty(_) -> lineNo, options, sources , packages, sourceFiles
                | Remote(newSource) -> lineNo, options, sources @ [newSource], packages, sourceFiles
                | ParserOptions(ParserOption.ReferencesMode mode) -> lineNo, { options with Strict = mode }, sources, packages, sourceFiles
                | ParserOptions(ParserOption.Redirects mode) -> lineNo, { options with Redirects = mode }, sources, packages, sourceFiles
                | ParserOptions(ParserOption.CopyLocal mode) -> lineNo, { options with Settings = { options.Settings with CopyLocal = Some mode }}, sources, packages, sourceFiles
                | ParserOptions(ParserOption.ImportTargets mode) -> lineNo, { options with Settings = { options.Settings with ImportTargets = Some mode }}, sources, packages, sourceFiles
                | ParserOptions(ParserOption.FrameworkRestrictions r) -> lineNo, { options with Settings = { options.Settings with FrameworkRestrictions = r }}, sources, packages, sourceFiles
                | ParserOptions(ParserOption.OmitContent omit) -> lineNo, { options with Settings = { options.Settings with  OmitContent = Some omit }}, sources, packages, sourceFiles
                | Package(name,version,rest) ->
                    let package = parsePackage(sources,DependenciesFile fileName,name,version,rest)

                    lineNo, options, sources, package :: packages, sourceFiles
                | SourceFile(origin, (owner,project, commit), path) ->
                    lineNo, options, sources, packages, { Owner = owner; Project = project; Commit = commit; Name = path; Origin = origin} :: sourceFiles
                    
            with
            | exn -> failwithf "Error in paket.dependencies line %d%s  %s" lineNo Environment.NewLine exn.Message)
        |> fun (_,options,sources,packages,remoteFiles) ->
            fileName,
            options,
            sources,
            packages |> List.rev,
            remoteFiles |> List.rev,
            lines
    
    let parseVersionString (version : string) = 
        { VersionRequirement = parseVersionRequirement (version.Trim '!')
          ResolverStrategy = parseResolverStrategy version }

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
            | Specific x when strategy = ResolverStrategy.Min -> "= " + x.ToString()
            | Specific x -> x.ToString()
            | VersionRange.Range(_, from, _, _) 
                    when DependenciesFileParser.parseVersionRequirement ("~> " + from.ToString() + preReleases) = version -> 
                        "~> " + from.ToString()
            | _ -> version.ToString()
            
        let text = prefix + version         
        if text <> "" && preReleases <> "" then text + " " + preReleases else text + preReleases

    let sourceString source = "source " + source

    let packageString packageName versionRequirement resolverStrategy (settings:InstallSettings) =
        let (PackageName name) = packageName
        let version = formatVersionRange resolverStrategy versionRequirement
        let s = settings.ToString()

        sprintf "nuget %s%s%s" name (if version <> "" then " " + version else "") (if s <> "" then " " + s else s)        

/// Allows to parse and analyze paket.dependencies files.
type DependenciesFile(fileName,options,sources,packages : PackageRequirement list, remoteFiles : UnresolvedSourceFile list, textRepresentation:string []) = 
    let packages = packages |> Seq.toList
    let dependencyMap = Map.ofSeq (packages |> Seq.map (fun p -> p.Name, p.VersionRequirement))

    let isPackageLine name (l : string) = 
        let splitted = l.Split(' ') |> Array.map (fun s -> s.ToLower())
        splitted |> Array.exists ((=) "nuget") && splitted |> Array.exists ((=) name)          

    let tryFindPackageLine (packageName:PackageName) =
        let name = packageName.ToString().ToLower()
        textRepresentation
        |> Array.tryFindIndex (isPackageLine name)
            
    member __.DirectDependencies = dependencyMap
    member __.Packages = packages
    member __.HasPackage (name : PackageName) = packages |> List.exists (fun p -> NormalizedPackageName p.Name = NormalizedPackageName name)
    member __.GetPackage (name : PackageName) = packages |> List.find (fun p -> NormalizedPackageName p.Name = NormalizedPackageName name)
    member __.RemoteFiles = remoteFiles
    member __.Options = options
    member __.FileName = fileName
    member __.Lines = textRepresentation
    member __.Sources = sources

    member this.Resolve(force,packages) =
        let getSha1 origin owner repo branch = RemoteDownload.getSHA1OfBranch origin owner repo branch |> Async.RunSynchronously
        let root = Path.GetDirectoryName this.FileName
        this.Resolve(getSha1,NuGetV2.GetVersions root,NuGetV2.GetPackageDetails root force,packages)   

    member this.Resolve(force) = 
        this.Resolve(force,packages)

    member __.Resolve(getSha1,getVersionF, getPackageDetailsF,rootDependencies) =
        let rootDependencies =
            match rootDependencies with
            | [] -> packages
            | _ -> rootDependencies

        let resolveSourceFile(file:ResolvedSourceFile) : PackageRequirement list =
            let parserF text =
                try
                    DependenciesFile.FromCode(text) |> ignore
                    true
                with 
                | _ -> false

            RemoteDownload.downloadDependenciesFile(Path.GetDirectoryName fileName,parserF, file)
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

        { ResolvedPackages = PackageResolver.Resolve(getVersionF, getPackageDetailsF, options.Settings.FrameworkRestrictions, remoteDependencies @ rootDependencies, Set.ofList packages)
          ResolvedSourceFiles = remoteFiles }        

    member __.Resolve(getSha1,getVersionF, getPackageDetailsF) =
        __.Resolve(getSha1,getVersionF,getPackageDetailsF,packages)

    member __.AddAdditionalPackage(packageName:PackageName,versionRequirement,resolverStrategy,settings,?pinDown) =
        let pinDown = defaultArg pinDown false
        let packageString = DependenciesFileSerializer.packageString packageName versionRequirement resolverStrategy settings

        // Try to find alphabetical matching position to insert the package
        let isPackageInLastSource (p:PackageRequirement) =
            match sources with
            | [] -> true
            | _ -> 
                let lastSource =  Seq.last sources
                p.Sources |> Seq.exists (fun s -> s = lastSource)

        let smaller = Seq.takeWhile (fun (p:PackageRequirement) -> p.Name <= packageName || not (isPackageInLastSource p)) packages |> List.ofSeq

        let newLines =
            let list = new System.Collections.Generic.List<_>()
            list.AddRange textRepresentation

            match tryFindPackageLine packageName with                        
            | Some pos -> 
                let package = DependenciesFileParser.parsePackageLine(sources,PackageRequirementSource.DependenciesFile fileName,list.[pos])

                if versionRequirement.Range.IsIncludedIn(package.VersionRequirement.Range) then
                    list.[pos] <- packageString
                else
                    list.Insert(pos + 1, packageString)
            | None -> 
                if pinDown then 
                    list.Add(packageString) 
                else
                    match smaller with
                    | [] -> 
                        match packages with
                        | [] ->
                            if remoteFiles <> [] then
                                list.Insert(0,"")
                    
                            match sources with
                            | [] -> 
                                list.Insert(0,packageString)
                                list.Insert(0,"")
                                list.Insert(0,DependenciesFileSerializer.sourceString Constants.DefaultNugetStream)
                            | _ -> 
                                list.Add("")
                                list.Add(packageString)
                        | p::_ -> 
                            match tryFindPackageLine p.Name with
                            | None -> list.Add packageString
                            | Some pos -> list.Insert(pos,packageString)
                    | _ -> 
                        let p = Seq.last smaller

                        match tryFindPackageLine p.Name with
                        | None -> list.Add packageString
                        | Some found -> 
                            let pos = ref (found + 1)
                            let skipped = ref false
                            while !pos < textRepresentation.Length - 1 && (String.IsNullOrWhiteSpace textRepresentation.[!pos] || textRepresentation.[!pos].ToLower().StartsWith("source")) do
                                if textRepresentation.[!pos].ToLower().StartsWith("source") then
                                    skipped := true
                                pos := !pos + 1
                            
                            if !skipped then
                                list.Insert(!pos,packageString)
                            else
                                list.Insert(found + 1,packageString)
            
            list |> Seq.toArray

        DependenciesFile(DependenciesFileParser.parseDependenciesFile fileName newLines)


    member this.AddAdditionalPackage(packageName:PackageName,version:string,settings) =
        let vr = DependenciesFileParser.parseVersionString version

        this.AddAdditionalPackage(packageName,vr.VersionRequirement,vr.ResolverStrategy,settings)

    member this.AddFixedPackage(packageName:PackageName,version:string,settings) =
        let vr = DependenciesFileParser.parseVersionString version

        let resolverStrategy,versionRequirement = 
            match packages |> List.tryFind (fun p -> NormalizedPackageName p.Name = NormalizedPackageName packageName) with
            | Some package -> 
                package.ResolverStrategy,
                match package.VersionRequirement.Range with
                | OverrideAll(_) -> package.VersionRequirement
                | _ -> vr.VersionRequirement
            | None -> vr.ResolverStrategy,vr.VersionRequirement

        this.AddAdditionalPackage(packageName,versionRequirement,resolverStrategy,settings,true)

    member this.AddFixedPackage(packageName:PackageName,version:string) =
        this.AddFixedPackage(packageName,version,InstallSettings.Default)

    member this.RemovePackage(packageName:PackageName) =
        match tryFindPackageLine packageName with
        | None -> this
        | Some pos ->
            let removeElementAt index myArr = // TODO: Replace this in F# 4.0
                [|  for i = 0 to Array.length myArr - 1 do 
                       if i <> index then yield myArr.[ i ] |]

            let newLines = removeElementAt pos textRepresentation
            DependenciesFile(DependenciesFileParser.parseDependenciesFile fileName newLines)

    static member add (dependenciesFile : DependenciesFile) (packageName,version,installSettings) =
        dependenciesFile.Add(packageName,version,installSettings)

    member this.Add(packageName,version:string,?installSettings : InstallSettings) =
        let installSettings = defaultArg installSettings InstallSettings.Default
        let (PackageName name) = packageName
        if this.HasPackage packageName && String.IsNullOrWhiteSpace version then 
            traceWarnfn "%s contains package %s already. ==> Ignored" fileName name
            this
        else
            if version = "" then
                tracefn "Adding %s to %s" name fileName
            else
                tracefn "Adding %s %s to %s" name version fileName
            this.AddAdditionalPackage(packageName,version,installSettings)

    member this.Remove(packageName) =
        let (PackageName name) = packageName
        if this.HasPackage packageName then         
            tracefn "Removing %s from %s" name fileName
            this.RemovePackage(packageName)
        else
            traceWarnfn "%s doesn't contain package %s. ==> Ignored" fileName name
            this

    member this.UpdatePackageVersion(packageName, version:string) = 
        let (PackageName name) = packageName
        if this.HasPackage(packageName) then
            let vr = DependenciesFileParser.parseVersionString version

            tracefn "Updating %s to version %s in %s" name version fileName
            let newLines = 
                this.Lines |> Array.map (fun l -> 
                                  let name = packageName.ToString().ToLower()
                                  if isPackageLine name l then 
                                      let p = this.GetPackage packageName
                                      DependenciesFileSerializer.packageString packageName vr.VersionRequirement vr.ResolverStrategy p.Settings
                                  else l)

            DependenciesFile(DependenciesFileParser.parseDependenciesFile this.FileName newLines)
        else 
            traceWarnfn "%s doesn't contain package %s. ==> Ignored" fileName name
            this

    member this.GetAllPackageSources() = 
        this.Packages
        |> List.collect (fun package -> package.Sources)
        |> Seq.distinct
        |> Seq.toList

    member this.RootPath =
        let fi = FileInfo(fileName)
        fi.Directory.FullName

    override __.ToString() = String.Join(Environment.NewLine, textRepresentation)

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