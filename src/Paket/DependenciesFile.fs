namespace Paket

open System
open System.IO
open Paket
open Paket.Logging

/// [omit]
module DependenciesFileParser = 
    open System.Text.RegularExpressions

    let private basicOperators = ["~>";"<=";">=";"=";">";"<"]
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
        |  ">=" :: v1 :: "<" :: v2 :: rest -> VersionRequirement(VersionRange.Range(Bound.Including,SemVer.parse v1,SemVer.parse v2,Bound.Excluding),parsePrerelease rest)
        |  ">=" :: v1 :: "<=" :: v2 :: rest -> VersionRequirement(VersionRange.Range(Bound.Including,SemVer.parse v1,SemVer.parse v2,Bound.Including),parsePrerelease rest)
        |  "~>" :: v1 :: ">=" :: v2 :: rest -> VersionRequirement(VersionRange.Range(Bound.Including,SemVer.parse v2,SemVer.parse(twiddle v1),Bound.Excluding),parsePrerelease rest)
        |  "~>" :: v1 :: ">" :: v2 :: rest -> VersionRequirement(VersionRange.Range(Bound.Excluding,SemVer.parse v2,SemVer.parse(twiddle v1),Bound.Excluding),parsePrerelease rest)
        |  ">" :: v1 :: "<" :: v2 :: rest -> VersionRequirement(VersionRange.Range(Bound.Excluding,SemVer.parse v1,SemVer.parse v2,Bound.Excluding),parsePrerelease rest)
        |  ">" :: v1 :: "<=" :: v2 :: rest -> VersionRequirement(VersionRange.Range(Bound.Excluding,SemVer.parse v1,SemVer.parse v2,Bound.Including),parsePrerelease rest)
        | _ -> 
            let splitVersion (text:string) =            
                match basicOperators |> List.tryFind(text.StartsWith) with
                | Some token -> token, text.Replace(token + " ", "").Split(' ') |> Array.toList
                | None -> "=", text.Split(' ') |> Array.toList

            try
                match splitVersion text with
                | ">=", version :: rest -> VersionRequirement(VersionRange.AtLeast(version),parsePrerelease rest)
                | ">", version :: rest -> VersionRequirement(VersionRange.GreaterThan(SemVer.parse version),parsePrerelease rest)
                | "<", version :: rest -> VersionRequirement(VersionRange.LessThan(SemVer.parse version),parsePrerelease rest)
                | "<=", version :: rest -> VersionRequirement(VersionRange.Maximum(SemVer.parse version),parsePrerelease rest)
                | "~>", minimum :: rest -> VersionRequirement(VersionRange.Between(minimum,twiddle minimum),parsePrerelease rest)
                | _, version :: rest -> VersionRequirement(VersionRange.Exactly(version),parsePrerelease rest)
                | _ -> failwithf "could not parse version range \"%s\"" text
            with
            | _ -> failwithf "could not parse version range \"%s\"" text


    let userNameRegex = new Regex("username[:][ ]*[\"]?([^\"]+)[\"]?", RegexOptions.IgnoreCase);
    let passwordRegex = new Regex("password[:][ ]*[\"]?([^\"]+)[\"]?", RegexOptions.IgnoreCase);
    let parseAuth(text:string) =
        if userNameRegex.IsMatch(text) && passwordRegex.IsMatch(text) then
            Some { Username = userNameRegex.Match(text).Groups.[1].Value; Password = passwordRegex.Match(text).Groups.[1].Value }
        else
            None

    let private (|Remote|Package|Blank|ReferencesMode|SourceFile|) (line:string) =
        match line.Trim() with
        | _ when String.IsNullOrWhiteSpace line -> Blank
        | trimmed when trimmed.StartsWith "source" ->
            let parts = trimmed.Split ' '
            Remote (parts.[1].Replace("\"",""),parseAuth trimmed)
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
        | trimmed when trimmed.StartsWith "github" ->
            let parts = trimmed.Replace("\"", "").Trim().Split([|' '|],StringSplitOptions.RemoveEmptyEntries)
            let getParts (projectSpec:string) =
                match projectSpec.Split [|':'; '/'|] with
                | [| owner; project |] -> owner, project, None
                | [| owner; project; commit |] -> owner, project, Some commit
                | _ -> failwithf "invalid github specification:%s     %s" Environment.NewLine trimmed
            match parts with
            | [| _; projectSpec; fileSpec |] -> SourceFile(getParts projectSpec, fileSpec)
            | _ -> failwithf "invalid github specification:%s     %s" Environment.NewLine trimmed
        | _ -> Blank
    
    let parseDependenciesFile fileName (lines:string seq) = 
        ((0, false, [], [], []), lines)
        ||> Seq.fold(fun (lineNo, referencesMode, sources: PackageSource list, packages, sourceFiles: UnresolvedSourceFile list) line ->
            let lineNo = lineNo + 1
            try
                match line with
                | Remote(newSource,auth) -> lineNo, referencesMode, (PackageSource.Parse(newSource.TrimEnd([|'/'|]),auth) :: sources), packages, sourceFiles
                | Blank -> lineNo, referencesMode, sources, packages, sourceFiles
                | ReferencesMode mode -> lineNo, mode, sources, packages, sourceFiles
                | Package(name,version) ->
                    lineNo, referencesMode, sources, 
                        { Sources = sources
                          Name = name
                          ResolverStrategy = parseResolverStrategy version
                          IsRoot = true
                          VersionRequirement = parseVersionRequirement(version.Trim '!') } :: packages, sourceFiles
                | SourceFile((owner,project, commit), path) ->
                    lineNo, referencesMode, sources, packages, { Owner = owner; Project = project; Commit = commit; Name = path } :: sourceFiles
                    
            with
            | exn -> failwithf "Error in paket.dependencies line %d%s  %s" lineNo Environment.NewLine exn.Message)
        |> fun (_,mode,_,packages,remoteFiles) ->
            fileName,
            mode,
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
            | Minimum x when strategy = ResolverStrategy.Max && x = SemVer.parse "0" -> ""
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
type DependenciesFile(fileName,strictMode,packages : UnresolvedPackage list, remoteFiles : UnresolvedSourceFile list) = 
    let packages = packages |> Seq.toList
    let dependencyMap = Map.ofSeq (packages |> Seq.map (fun p -> p.Name, p.VersionRequirement))
    member __.DirectDependencies = dependencyMap
    member __.Packages = packages
    member __.HasPackage (name : string) = packages |> List.exists (fun p -> p.Name.ToLower() = name.ToLower())
    member __.RemoteFiles = remoteFiles
    member __.Strict = strictMode
    member __.FileName = fileName
    member this.Resolve(force) = 
        let getSha1 owner repo branch = GitHub.getSHA1OfBranch owner repo branch |> Async.RunSynchronously
        this.Resolve(getSha1,Nuget.GetVersions,Nuget.GetPackageDetails force)
    member __.Resolve(getSha1,getVersionF, getPackageDetailsF) =
        let resolveSourceFile(file:ResolvedSourceFile) : UnresolvedPackage list =
            GitHub.downloadDependenciesFile(Path.GetDirectoryName fileName, file)
            |> Async.RunSynchronously
            |> DependenciesFile.FromCode
            |> fun df -> df.Packages

        let remoteFiles = ModuleResolver.Resolve(resolveSourceFile,getSha1,remoteFiles)

        let dependencies = 
            remoteFiles 
            |> List.map (fun f -> f.Dependencies)
            |> List.concat

        { ResolvedPackages = PackageResolver.Resolve(getVersionF, getPackageDetailsF, dependencies @ packages)
          ResolvedSourceFiles = remoteFiles }        

    member this.Add(packageName,version:string) =
        if this.HasPackage packageName then failwithf "%s has already package %s" Constants.DependenciesFile packageName
        let versionRange = DependenciesFileParser.parseVersionRequirement (version.Trim '!')
        let sources = 
            match packages |> List.rev with
            | lastPackage::_ -> lastPackage.Sources
            | [] -> [Constants.DefaultNugetSource]
        let newPackage = {Name = packageName; VersionRequirement = versionRange; Sources = sources; ResolverStrategy = DependenciesFileParser.parseResolverStrategy version; IsRoot = true }
        tracefn "Adding %s %s to paket.dependencies" packageName (versionRange.ToString())
        DependenciesFile(fileName,strictMode,packages @ [newPackage], remoteFiles)

    override __.ToString() =        
        let sources = 
            packages
            |> Seq.map (fun package -> package.Sources,package)
            |> Seq.groupBy fst

        let all =
            let hasReportedSource = ref false
            let hasReportedFirst = ref false
            let hasReportedSecond = ref false
            [ if strictMode then
                  yield "references strict"
              for sources, packages in sources do
                  for source in sources do
                      hasReportedSource := true
                      match source with
                      | Nuget source -> 
                        match source.Auth with
                        | None -> yield "source " + source.Url 
                        | Some auth -> yield sprintf "source %s username: \"%s\" password: \"%s\"" source.Url auth.Username auth.Password
                        
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
        tracefn "Parsing %s" fileName
        DependenciesFile(DependenciesFileParser.parseDependenciesFile fileName <| File.ReadAllLines fileName)

    /// Find the matching lock file to a dependencies file
    static member FindLockfile(dependenciesFileName) =
        let fi = FileInfo(dependenciesFileName)
        FileInfo(Path.Combine(fi.Directory.FullName, fi.Name.Replace(fi.Extension,"") + ".lock"))

    /// Find the matching lock file to a dependencies file
    member this.FindLockfile() = DependenciesFile.FindLockfile this.FileName