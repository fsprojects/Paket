namespace Paket

open System
open System.IO
open Paket.Domain
open Paket.Requirements
open Logging
open PlatformMatching

// An unparsed file in the nuget package -> still need to inspect the path for further information. After parsing an entry will be part of a "LibFolder" for example.
type UnparsedPackageFile =
    { FullPath : string
      PathWithinPackage : string }

[<RequireQualifiedAccess>]
type Reference =
    | Library of string
    | TargetsFile of string
    | FrameworkAssemblyReference of string

    member this.LibName =
        match this with
        | Reference.Library lib ->
            let fi = FileInfo(normalizePath lib)
            Some(fi.Name.Replace(fi.Extension, ""))
        | _ -> None

    member this.FrameworkReferenceName =
        match this with
        | Reference.FrameworkAssemblyReference name -> Some name
        | _ -> None

    member this.ReferenceName =
        match this with
        | Reference.FrameworkAssemblyReference name -> name
        | Reference.TargetsFile targetsFile ->
            let fi = FileInfo(normalizePath targetsFile)
            fi.Name.Replace(fi.Extension, "")
        | Reference.Library lib ->
            let fi = FileInfo(normalizePath lib)
            fi.Name.Replace(fi.Extension, "")

    member this.Path =
        match this with
        | Reference.Library path -> path
        | Reference.TargetsFile path -> path
        | Reference.FrameworkAssemblyReference path -> path

type InstallFiles =
    { References : Reference Set
      ContentFiles : string Set }

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module InstallFiles =
    let empty =
        { References = Set.empty
          ContentFiles = Set.empty }

    let addReference lib (installFiles:InstallFiles) =
        { installFiles with References = Set.add (Reference.Library lib) installFiles.References }

    let singleton lib = empty |> addReference lib

    let addTargetsFile targetsFile (installFiles:InstallFiles) =
        { installFiles with References = Set.add (Reference.TargetsFile targetsFile) installFiles.References }

    let addFrameworkAssemblyReference assemblyName  (installFiles:InstallFiles) =
        { installFiles with References = Set.add (Reference.FrameworkAssemblyReference assemblyName) installFiles.References }

    let getFrameworkAssemblies (installFiles:InstallFiles) =
        installFiles.References
        |> Set.map (fun r -> r.FrameworkReferenceName)
        |> Seq.choose id

    let mergeWith (that:InstallFiles) (installFiles:InstallFiles) =
        { installFiles with
            References = Set.union that.References installFiles.References
            ContentFiles = Set.union that.ContentFiles installFiles.ContentFiles }

type InstallFiles with
    member this.AddReference lib = InstallFiles.addReference  lib this
    member this.AddTargetsFile targetsFile = InstallFiles.addTargetsFile targetsFile this
    member this.AddFrameworkAssemblyReference assemblyName = InstallFiles.addFrameworkAssemblyReference assemblyName this
    member this.GetFrameworkAssemblies() = InstallFiles.getFrameworkAssemblies this
    member this.MergeWith that = InstallFiles.mergeWith that this

type RuntimeIdentifier =
    { Rid: string }
    static member Any = { Rid = "any" }
/// Represents a subfolder of a nuget package that provides files (content, references, etc) for one or more Target Profiles.  This is a logical representation of the 'net45' folder in a NuGet package, for example.
type LibFolder =
    { Path : ParsedPlatformPath
      Targets : TargetProfile list
      Files : InstallFiles }

    member this.GetSinglePlatforms() =
        this.Targets
        |> List.choose (function SinglePlatform t -> Some t | _ -> None)


type AnalyzerLanguage =
    | Any | CSharp | FSharp | VisualBasic

    static member FromDirectoryName(str : string) =
        match str with
        | "cs" -> CSharp
        | "vb" -> VisualBasic
        | "fs" -> FSharp
        | _ -> Any

    static member FromDirectory(dir : DirectoryInfo) =
        AnalyzerLanguage.FromDirectoryName(dir.Name)

type AnalyzerLib =
    {
        /// Path of the analyzer dll
        Path : string
        /// Target language for the analyzer
        Language : AnalyzerLanguage }
    static member FromFile(file : FileInfo) =
        {
            Path = file.FullName
            Language = AnalyzerLanguage.FromDirectory(file.Directory)
        }

/// Represents the contents of a particular package at a particular version.  Any install-specific actions like Content files, References, Roslyn Analyzers, MsBuild targets are represented here.
type InstallModel =
    { PackageName : PackageName
      PackageVersion : SemVerInfo
      CompileLibFolders : LibFolder list
      CompileRefFolders : LibFolder list
      RuntimeLibFolders : LibFolder list
      TargetsFileFolders : LibFolder list
      Analyzers: AnalyzerLib list
      LicenseUrl: string option }

module FolderScanner =
    // Stolen and modifed to our needs from http://www.fssnip.net/4I/title/sscanf-parsing-with-format-strings
    open System.Text.RegularExpressions
    open Microsoft.FSharp.Reflection

    type ParseResult<'t> =
        | ParseSucceeded of 't
        | ParseError of string
    module ParseResult =
        let bind f r =
            match r with
            | ParseSucceeded res -> f res
            | ParseError err -> ParseError err
        let map f r =
            r |> bind (fun r -> ParseSucceeded (f r))
        let box r =
            r |> map box

    let toParseResult error (wasSuccess, result) =
        if wasSuccess then ParseSucceeded result
        else ParseError error

    let check errorMsg f x =
        if f x then ParseSucceeded x
        else ParseError (errorMsg)

    let parseDecimal x = Decimal.TryParse(x, Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture)

    let parsers = dict [
                     'b', Boolean.TryParse >> toParseResult "Could not parse bool (b)" >> ParseResult.box
                     'd', Int32.TryParse >> toParseResult "Could not parse int (d)" >> ParseResult.box
                     'i', Int32.TryParse >> toParseResult "Could not parse int (i)" >> ParseResult.box
                     's', (fun s -> ParseSucceeded s) >> ParseResult.box
                     'u', UInt32.TryParse >> toParseResult "could not parse uint (u)" >> ParseResult.map int >> ParseResult.box
                     'x', check "could not parse int (x)" (String.forall Char.IsLower) >> ParseResult.map ((+) "0x") >> ParseResult.bind (Int32.TryParse >> toParseResult "Could not parse int (0x via x)") >> ParseResult.box
                     'X', check "could not parse int (X)" (String.forall Char.IsUpper) >> ParseResult.map ((+) "0x") >> ParseResult.bind (Int32.TryParse >> toParseResult "Could not parse int (0x via X)") >> ParseResult.box
                     'o', ((+) "0o") >> Int32.TryParse >> toParseResult "Could not parse int (0o)" >> ParseResult.box
                     'e', Double.TryParse >> toParseResult "Could not parse float (e)" >> ParseResult.box // no check for correct format for floats
                     'E', Double.TryParse >> toParseResult "Could not parse float (e)" >> ParseResult.box
                     'f', Double.TryParse >> toParseResult "Could not parse float (e)" >> ParseResult.box
                     'F', Double.TryParse >> toParseResult "Could not parse float (e)" >> ParseResult.box
                     'g', Double.TryParse >> toParseResult "Could not parse float (e)" >> ParseResult.box
                     'G', Double.TryParse >> toParseResult "Could not parse float (e)" >> ParseResult.box
                     'M', parseDecimal >> toParseResult "Could not parse decimal (m)" >> ParseResult.box
                     'c', check "Could not parse character (c)" (String.length >> (=) 1) >> ParseResult.map char >> ParseResult.box
                     'A', (fun s -> ParseSucceeded s) >> ParseResult.box
                    ]
    type AdvancedScanner =
      { Name : string
        Parser : string -> ParseResult<obj> }

    // array of all possible formatters, i.e. [|"%b"; "%d"; ...|]
    let separators =
       parsers.Keys
       |> Seq.map (fun c -> "%" + sprintf "%c" c)
       |> Seq.toArray

    // Creates a list of formatter characters from a format string,
    // for example "(%s,%d)" -> ['s', 'd']
    let rec getFormatters xs =
       match xs with
       | '%'::'%'::xr -> getFormatters xr
       | '%'::x::xr -> if parsers.ContainsKey x then x::getFormatters xr
                       else failwithf "Unknown formatter %%%c" x
       | x::xr -> getFormatters xr
       | [] -> []
    type private ScanResult =
       | ScanSuccess of obj[]
       | ScanRegexFailure of stringToScan:string * regex:string
       | ScanParserFailure of error:string
    type ScanOptions =
        { IgnoreCase : bool }
        static member Default = { IgnoreCase = false }
    let private sscanfHelper (opts:ScanOptions) (pf:PrintfFormat<_,_,_,_,'t>) s : ScanResult =
        let formatStr = pf.Value.Replace("%%", "%")
        let constants = formatStr.Split(separators, StringSplitOptions.None)
        let regexString = "^" + String.Join("(.*?)", constants |> Array.map Regex.Escape) + "$"
        let regex = Regex(regexString, if opts.IgnoreCase then RegexOptions.IgnoreCase else RegexOptions.None)
        let formatters = pf.Value.ToCharArray() // need original string here (possibly with "%%"s)
                        |> Array.toList |> getFormatters
        let matchres = regex.Match(s)
        if not matchres.Success then ScanRegexFailure(s, regexString)
        else
            let groups =
                matchres.Groups
                |> Seq.cast<Group>
                |> Seq.skip 1
            let results =
                (groups, formatters)
                ||> Seq.map2 (fun g f -> g.Value |> parsers.[f])
                |> Seq.toArray
            match results |> Seq.choose (fun r -> match r with ParseError error -> Some error | _ -> None) |> Seq.tryHead with
            | Some error ->
                ScanParserFailure error
            | None ->
                ScanSuccess (results |> Array.map (function ParseSucceeded res -> res | ParseError _ -> failwithf "Should not happen here"))

    let inline toGenericTuple<'t> (matches:obj[]) =
        if matches.Length = 1 then matches.[0] :?> 't
        else if matches.Length = 0 then Unchecked.defaultof<'t>
        else FSharpValue.MakeTuple(matches, typeof<'t>) :?> 't

    let trySscanf opts (pf:PrintfFormat<_,_,_,_,'t>) s : 't option =
        //raise <| FormatException(sprintf "Unable to scan string '%s' with regex '%s'" s regexString)
        match sscanfHelper opts pf s with
        | ScanSuccess matches -> toGenericTuple matches |> Some
        | _ -> None

    let inline private handleErrors s r =
        match r with
        | ScanSuccess matches -> toGenericTuple matches
        | ScanRegexFailure (s, regexString) -> raise <| FormatException(sprintf "Unable to scan string '%s' with regex '%s'" s regexString)
        | ScanParserFailure e -> raise <| FormatException(sprintf "Unable to parse string '%s' with parser: %s" s e)

    let sscanf opts (pf:PrintfFormat<_,_,_,_,'t>) s : 't =
        sscanfHelper opts pf s
        |> handleErrors s

    let private findSpecifiers = Regex(@"%(?<formatSpec>.)({(?<inside>.*?)})?")

    // Extends the syntax of the format string with %A{scanner}, and uses the corresponding named scanner from the advancedScanners parameter.
    let private sscanfExtHelper (advancedScanners:AdvancedScanner seq) opts (pf:PrintfFormat<_,_,_,_,'t>) s : ScanResult =
        let scannerMap =
            advancedScanners
            |> Seq.map (fun s -> s.Name, s)
            |> dict
        // replace advanced scanning formatters "%A{name}"
        let matches =
            findSpecifiers.Matches(pf.Value)
            |> Seq.cast<Match>
            |> Seq.map (fun m ->
                let formatSpec = m.Groups.["formatSpec"].Value
                let scannerName = m.Groups.["inside"].Value
                formatSpec, scannerName, m.Value, m.Index)
            |> Seq.toList
        let advancedFormatters =
            matches
            |> Seq.filter (fun (formatSpec, scannerName, _, _) ->
                formatSpec <> "%")
            |> Seq.map (fun (formatSpec, scannerName, _, _) ->
                if formatSpec = "A" then
                    if System.String.IsNullOrWhiteSpace scannerName then
                        None
                    else Some scannerMap.[scannerName]
                else None)

        let replacedFormatString =
            matches
            |> List.rev // start replacing on the back, this way indices are correct
            |> Seq.fold (fun (currentFormatterString:string) (formatSpec, scannerName, originalValue, index) ->
                let replacement =
                    match formatSpec with
                    | "A" -> "%A"
                    | _ -> originalValue
                currentFormatterString.Substring(0, index) + replacement + currentFormatterString.Substring(index + originalValue.Length)) pf.Value

        match sscanfHelper opts (PrintfFormat<_,_,_,_,'t> replacedFormatString) s with
        | ScanSuccess objResults ->
            let results =
                (objResults, advancedFormatters)
                ||> Seq.map2 (fun r a -> match a with Some p -> p.Parser (string r) | None -> ParseSucceeded r)
                |> Seq.toArray
            match results |> Seq.choose (fun r -> match r with ParseError error -> Some error | _ -> None) |> Seq.tryHead with
            | Some error ->
                ScanParserFailure error
            | None ->
                ScanSuccess (results |> Array.map (function ParseSucceeded res -> res | ParseError _ -> failwithf "Should not happen here"))
        | _ as s -> s

    let trySscanfExt advancedScanners opts (pf:PrintfFormat<_,_,_,_,'t>) s : 't option =
        //raise <| FormatException(sprintf "Unable to scan string '%s' with regex '%s'" s regexString)
        match sscanfExtHelper advancedScanners opts pf s with
        | ScanSuccess matches -> toGenericTuple matches |> Some
        | _ -> None

    let sscanfExt advancedScanners opts (pf:PrintfFormat<_,_,_,_,'t>) s : 't =
        sscanfExtHelper advancedScanners opts pf s
        |> handleErrors s

    // some basic testing
    //let (a,b) = sscanf "(%%%s,%M)" "(%hello, 4.53)"
    //let (x,y,z) = sscanf "%s-%s-%s" "test-this-string"
    //let (c,d,e,f,g,h,i) = sscanf "%b-%d-%i,%u,%x,%X,%o" "false-42--31,13,ff,FF,42"
    //let (j,k,l,m,n,o,p) = sscanf "%f %F %g %G %e %E %c" "1 2.1 3.4 .3 43.2e32 0 f"
    //let t = trySscanf "test%s" "test123"
    //let t2 = sscanf "test%s" "invalid"
    //let (blub:Guid option) = trySscanfExt [ { Name = "tbf"; Parser = fun s -> Guid.NewGuid() |> box } ] "test%A{tbf}" "6"
    //let (blub2:Guid) = sscanfExt [ { Name = "tbf"; Parser = fun s -> Guid.NewGuid() |> box } ] "test%A{tbf}" "6"
    //let testParserError = trySscanf "test%d" "testasd"

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module InstallModel =
    // A lot of insights can be gained from https://github.com/NuGet/NuGet.Client/blob/85731166154d0818d79a19a6d2417de6aa851f39/src/NuGet.Core/NuGet.Packaging/ContentModel/ManagedCodeConventions.cs#L385-L505
    // if you read this update the hash ;)
    open Logging
    open PlatformMatching

    let emptyModel packageName packageVersion =
        { PackageName = packageName
          PackageVersion = packageVersion
          CompileLibFolders = []
          CompileRefFolders = []
          RuntimeLibFolders = []
          TargetsFileFolders = []
          Analyzers = []
          LicenseUrl = None }

    type Tfm = PlatformMatching.ParsedPlatformPath
    type Rid = RuntimeIdentifier
    let scanners =
        [ { FolderScanner.AdvancedScanner.Name = "noSeperator";
            FolderScanner.AdvancedScanner.Parser = FolderScanner.check "seperator not allowed" (fun s -> not (s.Contains "/" || s.Contains "\\")) >> FolderScanner.ParseResult.box }
          { FolderScanner.AdvancedScanner.Name = "tfm";
            FolderScanner.AdvancedScanner.Parser = PlatformMatching.extractPlatforms >> FolderScanner.ParseResult.ParseSucceeded >> FolderScanner.ParseResult.box }
          { FolderScanner.AdvancedScanner.Name = "rid";
            FolderScanner.AdvancedScanner.Parser = (fun rid -> { Rid = rid }) >> FolderScanner.ParseResult.ParseSucceeded >> FolderScanner.ParseResult.box }]
    let trySscanf pf s =
        FolderScanner.trySscanfExt scanners { FolderScanner.ScanOptions.Default with IgnoreCase = true } pf s

    type FrameworkDependentFile =
      { Path : Tfm
        File : UnparsedPackageFile
        Runtime : RuntimeIdentifier}

    let getCompileRefAssembly (p:UnparsedPackageFile) =
        (trySscanf "ref/%A{tfm}/%A{noSeperator}" p.PathWithinPackage : (Tfm * string) option)
        |> Option.map (fun (l,_) -> { Path = l; File = p; Runtime = RuntimeIdentifier.Any })

    let getRuntimeAssembly (p:UnparsedPackageFile) =
        (trySscanf "lib/%A{tfm}/%A{noSeperator}" p.PathWithinPackage : (Tfm * string) option)
        |> Option.map (fun (l,_) -> { Path = l; File = p; Runtime = RuntimeIdentifier.Any })
        |> Option.orElseWith (fun _ ->
            (trySscanf "runtimes/%A{rid}/lib/%A{tfm}/%A{noSeperator}" p.PathWithinPackage : (Rid * Tfm * string) option)
            |> Option.map (fun (rid, l, _) -> { Path = l; File = p; Runtime = rid }))
        |> Option.orElseWith (fun _ ->
            (trySscanf "lib/%A{noSeperator}" p.PathWithinPackage : string option)
            |> Option.map (fun (_) -> { Path = Tfm.Empty; File = p; Runtime = RuntimeIdentifier.Any }))

    let getCompileLibAssembly (p:UnparsedPackageFile) =
        // %s because 'native' uses subfolders...
        (trySscanf "lib/%A{tfm}/%s" p.PathWithinPackage : (Tfm * string) option)
        |> Option.map (fun (l,path) ->
            if l.Name = "native" && l.Platforms = [ FrameworkIdentifier.Native(NoBuildMode,NoPlatform) ] then
                // We need some special logic to detect the platform
                let path = path.ToLowerInvariant()
                let newPlatform =
                    if path.Contains "/x86/" then Win32 else
                    if path.Contains "/arm/" then Arm else
                    if path.Contains "/x64/" then X64 else
                    if path.Contains "/address-model-32" then UnknownPlatform "address-model-32" else
                    if path.Contains "/address-model-64" then UnknownPlatform "address-model-64" else
                    NoPlatform
                let newBuildMode =
                    if path.Contains "/release/" then Release else
                    if path.Contains "/debug/" then Debug else
                    NoBuildMode
                { Path = { l with Platforms = [ FrameworkIdentifier.Native(newBuildMode,newPlatform) ]}; File = p; Runtime = RuntimeIdentifier.Any }
            else
            { Path = l; File = p; Runtime = RuntimeIdentifier.Any })
        |> Option.orElseWith (fun _ ->
            (trySscanf "lib/%A{noSeperator}" p.PathWithinPackage : string option)
            |> Option.map (fun (_) -> { Path = Tfm.Empty; File = p; Runtime = RuntimeIdentifier.Any }))

    let getRuntimeLibrary (p:UnparsedPackageFile) =
        (trySscanf "runtimes/%A{rid}/nativeassets/%A{tfm}/%A{noSeperator}" p.PathWithinPackage : (Rid * Tfm * string) option)
        |> Option.map (fun (rid, l,_) -> { Path = l; File = p; Runtime = rid })
        |> Option.orElseWith (fun _ ->
            (trySscanf "runtimes/%A{rid}/native/%A{noSeperator}" p.PathWithinPackage : (Rid * string) option)
            |> Option.map (fun (rid, _) -> { Path = Tfm.Empty; File = p; Runtime = rid }))

    let getMsbuildFile (p:UnparsedPackageFile) =
        (trySscanf "build/%A{tfm}/%A{noSeperator}" p.PathWithinPackage : (Tfm * string) option)
        |> Option.map (fun (l,_) -> { Path = l; File = p; Runtime = RuntimeIdentifier.Any })
        |> Option.orElseWith (fun _ ->
            (trySscanf "build/%A{noSeperator}" p.PathWithinPackage : string option)
            |> Option.map (fun (_) -> { Path = Tfm.Empty; File = p; Runtime = RuntimeIdentifier.Any }))

    let mapFolders mapfn (installModel:InstallModel) =
        { installModel with
            CompileLibFolders = List.map mapfn installModel.CompileLibFolders
            CompileRefFolders = List.map mapfn installModel.CompileRefFolders
            RuntimeLibFolders = List.map mapfn installModel.RuntimeLibFolders
            TargetsFileFolders   = List.map mapfn installModel.TargetsFileFolders  }

    let mapFiles mapfn (installModel:InstallModel) =
        installModel
        |> mapFolders (fun folder -> { folder with Files = mapfn folder.Files })

    let private getFileFolders (target:TargetProfile)  folderType choosefn =
        match Seq.tryFind (fun lib -> Seq.exists ((=) target) lib.Targets) folderType with
        | Some folder -> folder.Files.References |> Seq.choose choosefn
        | None -> Seq.empty

    /// This is for library references, which at the same time can be used for references (old world - pre dotnetcore)
    let getLegacyReferences (target : TargetProfile) (installModel:InstallModel) =
        getFileFolders target (installModel.CompileLibFolders) (function Reference.Library lib -> Some lib | _ -> None)
        |> Seq.cache

    [<Obsolete("usually this should not be used")>]
    let getCompileLibFolders (installModel: InstallModel) = installModel.CompileLibFolders

    /// This is for reference assemblies (new dotnetcore world)
    let getCompileReferences (target: TargetProfile) (installModel : InstallModel) =
        let results =
            getFileFolders target (installModel.CompileRefFolders) (function Reference.Library lib -> Some lib | _ -> None)
            |> Seq.cache
        if results |> Seq.isEmpty then
            // Fallback for old packages
            getLegacyReferences target installModel
        else results

    let getRuntimeLibraries (target : TargetProfile) (installModel:InstallModel) =
        getFileFolders target (installModel.RuntimeLibFolders) (function Reference.Library lib -> Some lib | _ -> None)
        |> Seq.cache

    let getTargetsFiles (target : TargetProfile) (installModel:InstallModel) =
        getFileFolders target installModel.TargetsFileFolders
            (function Reference.TargetsFile targetsFile -> Some targetsFile | _ -> None)

    /// This is for library references, which at the same time can be used for references (old world - pre dotnetcore)
    let getLegacyPlatformReferences frameworkIdentifier installModel =
        getLegacyReferences (SinglePlatform frameworkIdentifier) installModel

    /// This is for framework references, those do not exist anymore (old world - pre dotnetcore)
    let getLegacyFrameworkAssembliesLazy (installModel:InstallModel) =
        lazy ([ for lib in installModel.CompileLibFolders do
                    yield! lib.Files.GetFrameworkAssemblies()]
              |> Set.ofList)

    /// This is for library references, which at the same time can be used for references (old world - pre dotnetcore)
    let getLegacyReferencesLazy installModel =
        lazy ([ for lib in installModel.CompileLibFolders do
                    yield! lib.Files.References]
              |> Set.ofList)

    /// This is for reference assemblies (new dotnetcore world)
    let getReferencesLazy (installModel:InstallModel) =
        lazy ([ for lib in installModel.CompileRefFolders do
                    yield! lib.Files.References]
              |> Set.ofList)
    /// This is for runtime assemblies (new dotnetcore world)
    let getRuntimeAssembliesLazy (installModel:InstallModel) =
        lazy ([ for lib in installModel.RuntimeLibFolders do
                    yield! lib.Files.References]
              |> Set.ofList)

    let getTargetsFilesLazy (installModel:InstallModel) =
        lazy ([ for lib in installModel.TargetsFileFolders do
                    yield! lib.Files.References]
              |> Set.ofList)

    let removeIfCompletelyEmpty (this:InstallModel) =
        if Set.isEmpty (getLegacyFrameworkAssembliesLazy this |> force)
         && Set.isEmpty (getLegacyReferencesLazy this |> force)
         && Set.isEmpty (getReferencesLazy this |> force)
         && Set.isEmpty (getRuntimeAssembliesLazy this |> force)
         && Set.isEmpty (getTargetsFilesLazy this |> force)
         && List.isEmpty this.Analyzers then
            emptyModel this.PackageName this.PackageVersion
        else
            this

    let calcLibFoldersG (parsePackage : UnparsedPackageFile -> FrameworkDependentFile option) (libs:UnparsedPackageFile list) =
        libs
        |> List.choose parsePackage
        |> List.map (fun p -> p.Path)
        |> List.distinct //By (fun f -> f.Platforms)
        |> List.sort
        |> PlatformMatching.getSupportedTargetProfiles
        |> Seq.map (fun entry -> { Path = entry.Key; Targets = entry.Value; Files = InstallFiles.empty })
        |> Seq.toList

    let calcLegacyReferenceLibFolders = calcLibFoldersG getCompileLibAssembly
    let calcReferenceFolders = calcLibFoldersG getCompileRefAssembly
    let calcRuntimeFolders = calcLibFoldersG getRuntimeAssembly
    //let calcRefFolders = calcLibFoldersG extractRefFolder

    let addFileToFolder (path:LibFolder) (file:UnparsedPackageFile) (folders:LibFolder list) (addfn: string -> InstallFiles -> InstallFiles) =
        folders
        |> List.map (fun p ->
            if p.Path <> path.Path then p else
            { p with Files = addfn file.FullPath p.Files })

    let private addPackageLegacyLibFile references (path:LibFolder) (file:UnparsedPackageFile) (this:InstallModel) : InstallModel =
        let install =
            match references with
            | NuspecReferences.All -> true
            | NuspecReferences.Explicit list -> List.exists file.FullPath.EndsWith list

        if not install then this else
        { this with
            CompileLibFolders = addFileToFolder path file this.CompileLibFolders InstallFiles.addReference }

    let private addPackageRefFile references (path:LibFolder) (file:UnparsedPackageFile) (this:InstallModel) : InstallModel =
        let install =
            match references with
            | NuspecReferences.All -> true
            | NuspecReferences.Explicit list -> List.exists file.FullPath.EndsWith list

        if not install then this else
        { this with
            CompileRefFolders = addFileToFolder path file this.CompileRefFolders InstallFiles.addReference }

    let private addPackageRuntimeFile references (path:LibFolder) (file:UnparsedPackageFile) (this:InstallModel) : InstallModel =
        let install =
            match references with
            | NuspecReferences.All -> true
            | NuspecReferences.Explicit list -> List.exists file.FullPath.EndsWith list

        if not install then this else
        { this with
            RuntimeLibFolders = addFileToFolder path file this.RuntimeLibFolders InstallFiles.addReference }

    let private addItem libs extract addFunc getFolder initialState =
        List.fold (fun (model:InstallModel) file ->
            match extract file with
            | Some (folderName:FrameworkDependentFile) ->
                match List.tryFind (fun (folder:LibFolder) -> folder.Path = folderName.Path) (getFolder model) with
                | Some path -> addFunc path file model
                | _ -> model
            | None -> model) initialState libs

    let addLibReferences (libs:UnparsedPackageFile seq) references (installModel:InstallModel) : InstallModel =
        let libs = libs |> Seq.toList
        let legacyLibFolders = calcLegacyReferenceLibFolders libs
        let refFolders = calcReferenceFolders libs
        let runtimeFolders = calcRuntimeFolders libs

        { installModel with
            CompileLibFolders = legacyLibFolders
            CompileRefFolders = refFolders
            RuntimeLibFolders = runtimeFolders }
        |> addItem libs getCompileLibAssembly (addPackageLegacyLibFile references) (fun i -> i.CompileLibFolders)
        |> addItem libs getCompileRefAssembly (addPackageRefFile references) (fun i -> i.CompileRefFolders)
        |> addItem libs getRuntimeAssembly (addPackageRuntimeFile references) (fun i -> i.RuntimeLibFolders)

    let addAnalyzerFiles (analyzerFiles:UnparsedPackageFile seq) (installModel:InstallModel)  : InstallModel =
        let analyzerLibs =
            analyzerFiles
            |> Seq.map (fun file -> FileInfo file.FullPath |> AnalyzerLib.FromFile)
            |> List.ofSeq

        { installModel with Analyzers = installModel.Analyzers @ analyzerLibs}


    let rec addTargetsFile (path:LibFolder)  (file:UnparsedPackageFile) (installModel:InstallModel) :InstallModel =
        { installModel with
            TargetsFileFolders = addFileToFolder path file installModel.TargetsFileFolders InstallFiles.addTargetsFile
        }

    let addFrameworkAssemblyReference (installModel:InstallModel) (reference:FrameworkAssemblyReference) : InstallModel =
        let referenceApplies (folder : LibFolder) =
            match reference.FrameworkRestrictions |> getRestrictionList with
            | [] -> true
            | restrictions ->
                restrictions
                |> List.exists (fun restriction ->
                      match restriction with
                      | FrameworkRestriction.Portable _ ->
                            folder.Targets
                            |> List.exists (fun target ->
                                match target with
                                | SinglePlatform _ -> false
                                | _ -> true)
                      | FrameworkRestriction.Exactly target ->
                            folder.GetSinglePlatforms()
                            |> List.exists ((=) target)
                        | FrameworkRestriction.AtLeast target ->
                            folder.GetSinglePlatforms()
                            |> List.exists (fun t -> t >= target && t.IsSameCategoryAs(target))
                        | FrameworkRestriction.Between(min,max) ->
                            folder.GetSinglePlatforms()
                            |> List.exists (fun t -> t >= min && t < max && t.IsSameCategoryAs(min)))

        let model =
            if List.isEmpty installModel.CompileLibFolders then
                // TODO: Ask forki about this wtf
                let folders = calcLegacyReferenceLibFolders [{ FullPath ="lib/Default.dll"; PathWithinPackage = "lib/Default.dll" }]
                { installModel with CompileLibFolders = folders }
            else
                installModel

        model |> mapFolders(fun folder ->
            if referenceApplies folder then
                { folder with Files = folder.Files.AddFrameworkAssemblyReference reference.AssemblyName }
            else
                folder)

    let addFrameworkAssemblyReferences references (installModel:InstallModel) : InstallModel =
        references |> Seq.fold addFrameworkAssemblyReference (installModel:InstallModel)

    let filterExcludes excludes (installModel:InstallModel) =
        let excluded e reference =
            match reference with
            | Reference.Library x -> x.Contains e
            | Reference.TargetsFile x -> x.Contains e
            | Reference.FrameworkAssemblyReference x -> x.Contains e

        excludes
        |> List.fold (fun (model:InstallModel) fileName ->
                mapFiles (fun files -> { files with References = Set.filter (excluded fileName >> not) files.References }) model)
                installModel

    let filterBlackList (installModel:InstallModel) =

        let includeReferences = function
            | Reference.Library lib -> not (String.endsWithIgnoreCase ".dll" lib || String.endsWithIgnoreCase ".exe" lib || String.endsWithIgnoreCase ".so" lib || String.endsWithIgnoreCase ".dylib" lib )
            | Reference.TargetsFile targetsFile ->
                (not (String.endsWithIgnoreCase ".props" targetsFile|| String.endsWithIgnoreCase ".targets" targetsFile))
            | _ -> false

        let excludeSatelliteAssemblies = function
            | Reference.Library lib -> lib.EndsWith ".resources.dll"
            | _ -> false

        let blackList =
            [ includeReferences
              excludeSatelliteAssemblies]

        blackList
        |> List.map (fun f -> f >> not) // inverse
        |> List.fold (fun (model:InstallModel) f ->
                mapFiles (fun files -> { files with References = Set.filter f files.References }) model)
                installModel

    let applyFrameworkRestrictions (restrictions:FrameworkRestriction list) (installModel:InstallModel) =
        match restrictions with
        | [] -> installModel
        | restrictions ->
            let applRestriction folder =
                { folder with Targets = applyRestrictionsToTargets restrictions folder.Targets}

            { installModel with
                CompileLibFolders =
                    installModel.CompileLibFolders
                    |> List.map applRestriction
                    |> List.filter (fun folder -> folder.Targets <> [])

                CompileRefFolders =
                    installModel.CompileRefFolders
                    |> List.map applRestriction
                    |> List.filter (fun folder -> folder.Targets <> [])

                RuntimeLibFolders =
                    installModel.RuntimeLibFolders
                    |> List.map applRestriction
                    |> List.filter (fun folder -> folder.Targets <> [])

                TargetsFileFolders =
                    installModel.TargetsFileFolders
                    |> List.map applRestriction
                    |> List.filter (fun folder -> folder.Targets <> [])  }

    let rec addTargetsFiles (targetsFiles:UnparsedPackageFile list) (this:InstallModel) : InstallModel =
        let targetsFileFolders =
            calcLibFoldersG getMsbuildFile targetsFiles
        { this with TargetsFileFolders = targetsFileFolders }
            |> addItem targetsFiles getMsbuildFile (addTargetsFile) (fun i -> i.TargetsFileFolders)

    let filterReferences references (this:InstallModel) =
        let inline mapfn (files:InstallFiles) =
            { files with
                References = files.References |> Set.filter (fun reference -> Set.contains reference.ReferenceName references |> not)
            }
        mapFiles mapfn this

    let addLicense url (model: InstallModel) =
        if String.IsNullOrWhiteSpace url then model
        else  { model with LicenseUrl = Some url }

    let createFromLibs packageName packageVersion frameworkRestrictions (libs:UnparsedPackageFile seq) targetsFiles analyzerFiles (nuspec:Nuspec) =
        emptyModel packageName packageVersion
        |> addLibReferences libs nuspec.References
        |> addTargetsFiles targetsFiles
        |> addAnalyzerFiles analyzerFiles
        |> addFrameworkAssemblyReferences nuspec.FrameworkAssemblyReferences
        |> filterBlackList
        |> applyFrameworkRestrictions frameworkRestrictions
        |> removeIfCompletelyEmpty
        |> addLicense nuspec.LicenseUrl

type InstallModel with

    static member EmptyModel (packageName, packageVersion) = InstallModel.emptyModel packageName packageVersion

    [<Obsolete("usually this should not be used")>]
    member this.GetReferenceFolders() = InstallModel.getCompileLibFolders this

    member this.MapFolders mapfn = InstallModel.mapFolders mapfn this

    member this.MapFiles mapfn = InstallModel.mapFiles mapfn this

    [<Obsolete("usually this should not be used, use GetLegacyReferences for the full .net and GetCompileReferences/GetRuntimeLibraries for dotnetcore")>]
    member this.GetLibReferences target = InstallModel.getLegacyReferences target this
    member this.GetLegacyReferences target = InstallModel.getLegacyReferences target this
    member this.GetCompileReferences target = InstallModel.getCompileReferences target this
    member this.GetRuntimeLibraries target = InstallModel.getRuntimeLibraries target this

    [<Obsolete("usually this should not be used, use GetLegacyReferences for the full .net and GetCompileReferences for dotnetcore")>]
    member this.GetLibReferences frameworkIdentifier = InstallModel.getLegacyPlatformReferences frameworkIdentifier this

    member this.GetTargetsFiles target = InstallModel.getTargetsFiles target this

    [<Obsolete("usually this should not be used, use GetLegacyFrameworkAssembliesLazy for the full .net and remove this call for dotnetcore (dnc has no reference assemblies)")>]
    member this.GetFrameworkAssembliesLazy =  InstallModel.getLegacyFrameworkAssembliesLazy this
    member this.GetLegacyFrameworkAssembliesLazy =  InstallModel.getLegacyFrameworkAssembliesLazy this

    [<Obsolete("usually this should not be used, use GetLegacyReferencesLazy for the full .net and GetCompileReferencesLazy for dotnetcore")>]
    member this.GetLibReferencesLazy = InstallModel.getLegacyReferencesLazy this
    member this.GetLegacyReferencesLazy = InstallModel.getLegacyReferencesLazy this
    member this.GetCompileReferencesLazy = InstallModel.getReferencesLazy this

    member this.GetTargetsFilesLazy =  InstallModel.getTargetsFilesLazy this

    [<Obsolete("usually this should not be used, use CalcLegacyReferencesFolders for the full .net and CalcReferencesFolders for dotnetcore")>]
    member this.CalcLibFolders libs = InstallModel.calcLegacyReferenceLibFolders libs
    member this.CalcLegacyReferencesFolders libs = InstallModel.calcLegacyReferenceLibFolders libs
    member this.CalcReferencesFolders libs = InstallModel.calcReferenceFolders libs


    member this.AddLibReferences (libs, references) = InstallModel.addLibReferences libs references this

    member this.AddReferences libs = InstallModel.addLibReferences libs NuspecReferences.All this

    member this.AddAnalyzerFiles analyzerFiles = InstallModel.addAnalyzerFiles analyzerFiles this

    member this.AddTargetsFile(path, file) = InstallModel.addTargetsFile path file this

    member this.AddTargetsFiles targetsFiles = InstallModel.addTargetsFiles targetsFiles this

    member this.AddFrameworkAssemblyReference reference = InstallModel.addFrameworkAssemblyReference this reference

    member this.AddFrameworkAssemblyReferences references = InstallModel.addFrameworkAssemblyReferences references this

    member this.FilterBlackList () = InstallModel.filterBlackList this

    member this.FilterExcludes excludes = InstallModel.filterExcludes excludes this

    member this.FilterReferences(references) = InstallModel.filterReferences references this

    member this.ApplyFrameworkRestrictions restrictions = InstallModel.applyFrameworkRestrictions restrictions this

    member this.RemoveIfCompletelyEmpty() = InstallModel.removeIfCompletelyEmpty this

    static member CreateFromLibs(packageName, packageVersion, frameworkRestrictions:FrameworkRestriction list, libs : UnparsedPackageFile seq, targetsFiles, analyzerFiles, nuspec : Nuspec) =
        InstallModel.createFromLibs packageName packageVersion frameworkRestrictions libs targetsFiles analyzerFiles nuspec
