namespace Paket

open System
open System.IO
open Pri.LongPath
open Paket.Domain
open Paket.Requirements
open Logging
open PlatformMatching
open ProviderImplementation.AssemblyReader.Utils.SHA1
open NuGet

type UnparsedPackageFile = Paket.NuGet.UnparsedPackageFile
type Tfm = PlatformMatching.ParsedPlatformPath

//type Rid = Paket.Rid
type FrameworkDependentFile = {
    Path : Tfm
    File : UnparsedPackageFile
    Runtime : Rid option
}

type Library = {
    /// Usually the file name without extension, use for sorting and stuff.
    Name : string
    Path : string
    PathWithinPackage : string
}

module Library =
    let ofFile (f:FrameworkDependentFile) =
        let fi = FileInfo(normalizePath f.File.FullPath)
        // Extension can totally be an empty string, see https://github.com/fsprojects/Paket/issues/2405
        let name =
            let ext = fi.Extension
            if String.IsNullOrEmpty ext then fi.Name
            else fi.Name.Replace(ext, "")

        { Name = name
          Path = f.File.FullPath
          PathWithinPackage = f.File.PathWithinPackage }

type RuntimeLibrary = {
    Library : Library
    Rid : Rid option
}

module RuntimeLibrary =
    let ofFile (f:FrameworkDependentFile) =
        { Library = Library.ofFile f; Rid = f.Runtime }

type MsBuildFile = {
    Name : string
    Path : string
}

module MsBuildFile =
    let ofFile (f:FrameworkDependentFile) =
        let fi = FileInfo(normalizePath f.File.FullPath)
        let name = fi.Name.Replace(fi.Extension, "")
        { Name = name; Path = f.File.FullPath }

type FrameworkReference = {
    Name : string
}

module FrameworkReference =
    let ofName n = { FrameworkReference.Name = n }

type ReferenceOrLibraryFolder = {
    FrameworkReferences : FrameworkReference Set
    Libraries : Library Set
}

module ReferenceOrLibraryFolder =
   let empty = { FrameworkReferences = Set.empty; Libraries = Set.empty }
   let addLibrary item old =
      { old with ReferenceOrLibraryFolder.Libraries = Set.add item old.Libraries }
   let addFrameworkReference item old =
      { old with ReferenceOrLibraryFolder.FrameworkReferences = Set.add item old.FrameworkReferences }

/// Represents a subfolder of a nuget package that provides files (content, references, etc) for one or more Target Profiles.  This is a logical representation of the 'net45' folder in a NuGet package, for example.
type FrameworkFolder<'T> = {
    Path : ParsedPlatformPath
    Targets : TargetProfile Set
    FolderContents : 'T
} with
    member this.GetSinglePlatforms() =
        this.Targets
        |> Seq.choose (function SinglePlatform t -> Some t | _ -> None)

module FrameworkFolder =
    let map f (l:FrameworkFolder<_>) = {
        Path = l.Path
        Targets = l.Targets
        FolderContents = f l.FolderContents
    }

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

type AnalyzerLib = {
    /// Path of the analyzer dll
    Path : string
    /// Target language for the analyzer
    Language : AnalyzerLanguage
} with
    static member FromFile(file : FileInfo) = {
        Path = file.FullName
        Language = AnalyzerLanguage.FromDirectory(file.Directory)
    }

/// Represents the contents of a particular package at a particular version.  Any install-specific actions like Content files, References, Roslyn Analyzers, MsBuild targets are represented here.
type InstallModel = {
    PackageName : PackageName
    PackageVersion : SemVerInfo
    CompileLibFolders : FrameworkFolder<ReferenceOrLibraryFolder> list
    CompileRefFolders : FrameworkFolder<Library Set> list
    RuntimeAssemblyFolders : FrameworkFolder<RuntimeLibrary Set> list
    RuntimeLibFolders : FrameworkFolder<RuntimeLibrary Set> list
    TargetsFileFolders : FrameworkFolder<MsBuildFile Set> list
    Analyzers: AnalyzerLib list
    LicenseUrl: string option
}

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

    let choose errorMsg f x =
        match f x with
        | Some y -> ParseSucceeded y
        | None -> ParseError (errorMsg)

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

    type AdvancedScanner = {
        Name : string
        Parser : string -> ParseResult<obj>
    }

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

    type ScanOptions = {
        IgnoreCase : bool
    } with
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
    open NuGet

    let emptyModel packageName packageVersion = {
        PackageName = packageName
        PackageVersion = packageVersion
        CompileLibFolders = []
        CompileRefFolders = []
        RuntimeLibFolders = []
        RuntimeAssemblyFolders = []
        TargetsFileFolders = []
        Analyzers = []
        LicenseUrl = None
    }

    type Tfm = PlatformMatching.ParsedPlatformPath
    type Rid = Paket.Rid
    let scanners =
        [ { FolderScanner.AdvancedScanner.Name = "noSeperator";
            FolderScanner.AdvancedScanner.Parser = FolderScanner.check "seperator not allowed" (fun s -> not (s.Contains "/" || s.Contains "\\")) >> FolderScanner.ParseResult.box }
          { FolderScanner.AdvancedScanner.Name = "tfm";
            FolderScanner.AdvancedScanner.Parser = FolderScanner.choose "invalid tfm" PlatformMatching.extractPlatforms >> FolderScanner.ParseResult.box }
          { FolderScanner.AdvancedScanner.Name = "rid";
            FolderScanner.AdvancedScanner.Parser = (fun rid -> { Rid = rid }) >> FolderScanner.ParseResult.ParseSucceeded >> FolderScanner.ParseResult.box }]
    let trySscanf pf s =
        FolderScanner.trySscanfExt scanners { FolderScanner.ScanOptions.Default with IgnoreCase = true } pf s

    let getCompileRefAssembly (p:UnparsedPackageFile) =
        (trySscanf "ref/%A{tfm}/%A{noSeperator}" p.PathWithinPackage : (Tfm * string) option)
        |> Option.map (fun (l,_) -> { Path = l; File = p; Runtime = None })

    let getRuntimeAssembly (p:UnparsedPackageFile) =
        (trySscanf "lib/%A{tfm}/%A{noSeperator}" p.PathWithinPackage : (Tfm * string) option)
        |> Option.map (fun (l,_) -> { Path = l; File = p; Runtime = None })
        |> Option.orElseWith (fun _ ->
            (trySscanf "runtimes/%A{rid}/lib/%A{tfm}/%A{noSeperator}" p.PathWithinPackage : (Rid * Tfm * string) option)
            |> Option.map (fun (rid, l, _) -> { Path = l; File = p; Runtime = Some rid }))
        |> Option.orElseWith (fun _ ->
            (trySscanf "lib/%A{noSeperator}" p.PathWithinPackage : string option)
            |> Option.map (fun (_) -> { Path = Tfm.Empty; File = p; Runtime = None }))

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
                { Path = { l with Platforms = [ FrameworkIdentifier.Native(newBuildMode,newPlatform) ]}; File = p; Runtime = None }
            else
            { Path = l; File = p; Runtime = None })
        |> Option.orElseWith (fun _ ->
            (trySscanf "lib/%A{noSeperator}" p.PathWithinPackage : string option)
            |> Option.map (fun (_) -> { Path = Tfm.Empty; File = p; Runtime = None }))

    let getRuntimeLibrary (p:UnparsedPackageFile) =
        (trySscanf "runtimes/%A{rid}/nativeassets/%A{tfm}/%A{noSeperator}" p.PathWithinPackage : (Rid * Tfm * string) option)
        |> Option.map (fun (rid, l,_) -> { Path = l; File = p; Runtime = Some rid })
        |> Option.orElseWith (fun _ ->
            (trySscanf "runtimes/%A{rid}/native/%A{noSeperator}" p.PathWithinPackage : (Rid * string) option)
            |> Option.map (fun (rid, _) -> { Path = Tfm.Empty; File = p; Runtime = Some rid }))

    let getMsbuildFile (p:UnparsedPackageFile) =
        (trySscanf "build/%A{tfm}/%A{noSeperator}" p.PathWithinPackage : (Tfm * string) option)
        |> Option.map (fun (l,_) -> { Path = l; File = p; Runtime = None })
        |> Option.orElseWith (fun _ ->
            (trySscanf "build/%A{noSeperator}" p.PathWithinPackage : string option)
            |> Option.map (fun (_) -> { Path = Tfm.Empty; File = p; Runtime = None }))

    // Build up InstallModel

    let private getFileFolders (target:TargetProfile)  folderType choosefn =
        match Seq.tryFind (fun lib -> Seq.exists ((=) target) lib.Targets) folderType with
        | Some folder -> choosefn folder.FolderContents
        | None -> Seq.empty

    let private getFileFoldersByPath (path:Tfm) (folderType:seq<FrameworkFolder<_>>) choosefn =
        match Seq.tryFind (fun (lib:FrameworkFolder<_>) -> path = lib.Path) folderType with
        | Some folder -> choosefn folder.FolderContents
        | None -> Seq.empty

    let private getAllFiles folderType choosefn =
        folderType
        |> Seq.map (fun folder -> choosefn folder.FolderContents)
        |> Seq.concat

    /// This is for library references, which at the same time can be used for references (old world - pre dotnetcore)
    let getLegacyReferences (target : TargetProfile) (installModel:InstallModel) =
        getFileFolders target (installModel.CompileLibFolders) (fun f -> f.Libraries |> Set.toSeq)
        |> Seq.cache

    let getLegacyFrameworkReferences (target : TargetProfile) (installModel:InstallModel) =
        getFileFolders target (installModel.CompileLibFolders) (fun f -> f.FrameworkReferences |> Set.toSeq)
        |> Seq.cache

    let getAllLegacyFrameworkReferences (installModel:InstallModel) =
        getAllFiles installModel.CompileLibFolders (fun f -> f.FrameworkReferences |> Set.toSeq)
        |> Seq.cache

    let getAllLegacyReferences (installModel:InstallModel) =
        getAllFiles installModel.CompileLibFolders (fun f -> f.Libraries |> Set.toSeq)
        |> Seq.cache

    [<Obsolete("usually this should not be used")>]
    let getCompileLibFolders (installModel: InstallModel) = installModel.CompileLibFolders

    /// This is for reference assemblies (new dotnetcore world)
    let getCompileReferences (target: TargetProfile) (installModel : InstallModel) =
        let results =
            getFileFolders target (installModel.CompileRefFolders) (fun f -> f |> Set.toSeq )
            |> Seq.cache
        if results |> Seq.isEmpty then
            // Fallback for old packages
            getLegacyReferences target installModel
        else results

    let getTargetsFiles (target : TargetProfile) (installModel:InstallModel) =
        getFileFolders target installModel.TargetsFileFolders (fun f -> f |> Set.toSeq)

    /// This is for library references, which at the same time can be used for references (old world - pre dotnetcore)
    let getLegacyPlatformReferences frameworkIdentifier installModel =
        getLegacyReferences frameworkIdentifier installModel

    let isEmpty (lib: FrameworkFolder<Set<'T>> list) =
        lib
        |> Seq.map (fun l -> l.FolderContents)
        |> Seq.forall Set.isEmpty

    let removeIfCompletelyEmpty (this:InstallModel) =
        let foldersEmpty =
            isEmpty this.CompileRefFolders && isEmpty this.TargetsFileFolders && isEmpty this.RuntimeAssemblyFolders &&
            this.CompileLibFolders
            |> Seq.map (fun c -> c.FolderContents.Libraries |> Set.toSeq, c.FolderContents.FrameworkReferences |> Set.toSeq)
            |> Seq.forall (fun (libs, refs) -> Seq.isEmpty libs && Seq.isEmpty refs)

        if foldersEmpty && List.isEmpty this.Analyzers then
            emptyModel this.PackageName this.PackageVersion
        else
            this

    let calcLibFoldersG empty (parsePackage : UnparsedPackageFile -> FrameworkDependentFile option) (libs:UnparsedPackageFile list) =
        libs
        |> List.choose parsePackage
        |> List.map (fun p -> p.Path)
        |> List.distinct //By (fun f -> f.Platforms)
        |> List.sort
        |> PlatformMatching.getSupportedTargetProfiles
        |> Seq.map (fun entry -> { Path = entry.Key; Targets = entry.Value; FolderContents = empty })
        |> Seq.toList

    let calcLegacyReferenceLibFolders = calcLibFoldersG ReferenceOrLibraryFolder.empty getCompileLibAssembly
    let calcReferenceFolders = calcLibFoldersG Set.empty getCompileRefAssembly
    let calcRuntimeAssemblyFolders = calcLibFoldersG Set.empty getRuntimeAssembly
    let calcRuntimeLibraryFolders l = calcLibFoldersG Set.empty getRuntimeLibrary l
    //let calcRefFolders = calcLibFoldersG extractRefFolder

    let addFileToFolder<'T, 'Item> (path:FrameworkFolder<'T>) (file:'Item) (folders:FrameworkFolder<'T> list) (addfn: 'Item -> 'T -> 'T) =
        folders
        |> List.map (fun p ->
            if p.Path <> path.Path then p else
            { p with FolderContents = addfn file p.FolderContents })

    let private addPackageLegacyLibFile references (path:FrameworkFolder<ReferenceOrLibraryFolder>) (file:FrameworkDependentFile) (this:InstallModel) : InstallModel =
        let install =
            match references with
            | NuspecReferences.All -> true
            | NuspecReferences.Explicit list -> List.exists file.File.FullPath.EndsWith list

        if not install then 
            this 
        else
            let folders = addFileToFolder path (Library.ofFile file) this.CompileLibFolders ReferenceOrLibraryFolder.addLibrary
            { this with
                CompileLibFolders = folders }

    let private addPackageRefFile references (path:FrameworkFolder<Library Set>) (file:FrameworkDependentFile) (this:InstallModel) : InstallModel =
        let install =
            match references with
            | NuspecReferences.All -> true
            | NuspecReferences.Explicit list -> List.exists file.File.FullPath.EndsWith list

        if not install then this else
        { this with
            CompileRefFolders = addFileToFolder path (Library.ofFile file) this.CompileRefFolders Set.add }

    let private addPackageRuntimeAssemblyFile references (path:FrameworkFolder<RuntimeLibrary Set>) (file:FrameworkDependentFile) (this:InstallModel) : InstallModel =
        let install =
            match references with
            | NuspecReferences.All -> true
            | NuspecReferences.Explicit list -> List.exists file.File.FullPath.EndsWith list

        if not install then this else
        { this with
            RuntimeAssemblyFolders = addFileToFolder path (RuntimeLibrary.ofFile file) this.RuntimeAssemblyFolders Set.add }

    let private addPackageRuntimeLibraryFile references (path:FrameworkFolder<RuntimeLibrary Set>) (file:FrameworkDependentFile) (this:InstallModel) : InstallModel =
        let install =
            match references with
            | NuspecReferences.All -> true
            | NuspecReferences.Explicit list -> List.exists file.File.FullPath.EndsWith list

        if not install then this else
        { this with
            RuntimeLibFolders = addFileToFolder path (RuntimeLibrary.ofFile file) this.RuntimeLibFolders Set.add }

    let private addItem libs extract addFunc getFolder initialState =
        List.fold (fun (model:InstallModel) file ->
            match extract file with
            | Some (parsedFile:FrameworkDependentFile) ->
                match List.tryFind (fun (folder:FrameworkFolder<_>) -> folder.Path = parsedFile.Path) (getFolder model) with
                | Some path -> addFunc path parsedFile model
                | _ -> model
            | None -> model) initialState libs


    let addAnalyzerFiles (analyzerFiles:NuGet.UnparsedPackageFile seq) (installModel:InstallModel)  : InstallModel =
        let analyzerLibs =
            analyzerFiles
            |> Seq.map (fun file -> FileInfo file.FullPath |> AnalyzerLib.FromFile)
            |> List.ofSeq

        { installModel with Analyzers = installModel.Analyzers @ analyzerLibs}


    let rec addTargetsFile (path:FrameworkFolder<_>)  (file:FrameworkDependentFile) (installModel:InstallModel) :InstallModel =
        { installModel with
            TargetsFileFolders = addFileToFolder path (MsBuildFile.ofFile file) installModel.TargetsFileFolders Set.add
        }

    let getAllRuntimeAssemblies (installModel:InstallModel) =
        getAllFiles installModel.RuntimeAssemblyFolders (fun f -> f |> Set.toSeq)
        |> Seq.cache

    let getRuntimeAssemblies (graph:RuntimeGraph) (rid:Rid) (target : TargetProfile) (installModel:InstallModel) =
        // We need to recalculate the framework association after filtering with the RID
        let allAssemblies = installModel.RuntimeAssemblyFolders |> Seq.collect (fun f -> f.FolderContents |> Seq.map (fun c -> f.Path, c))
        let allRids = allAssemblies |> Seq.choose (fun (_,s) -> s.Rid) |> Set.ofSeq
        let bestMatchingRid =
            RuntimeGraph.getInheritanceList rid graph
            |> Seq.tryFind (fun rid -> allRids.Contains rid)
        let filtered = allAssemblies |> Seq.filter (fun (_, a) -> a.Rid = None || a.Rid = bestMatchingRid) |> Seq.cache
        let unParsedList = filtered |> Seq.map (fun (p, f) ->
            { UnparsedPackageFile.FullPath = f.Library.Path; UnparsedPackageFile.PathWithinPackage = f.Library.PathWithinPackage } ) |> Seq.toList
        let recalculated = calcRuntimeAssemblyFolders unParsedList
        let filledFolder =
            unParsedList
            |> Seq.fold (fun folder (file) ->
                let fdf = (getRuntimeAssembly file).Value
                match List.tryFind (fun (folder:FrameworkFolder<_>) -> folder.Path = fdf.Path) (folder) with
                | Some path -> addFileToFolder path (RuntimeLibrary.ofFile fdf) folder Set.add
                | _ -> folder) recalculated

        let tfmData =
            getFileFolders target filledFolder (Set.toSeq)
            |> Seq.filter (fun lib -> lib.Rid = None || lib.Rid = bestMatchingRid)
            |> Seq.cache
        if verbose && allRids.Count > 0 && bestMatchingRid = None then
            // No idea what this means, if this fails for you make this a warning or remove it completely.
            // I added this to see if it appears in the real world...
            printfn "We found RID dependency libraries in the package '%A'-'%A' but nothing matched against '%A'" installModel.PackageName installModel.PackageVersion rid
        tfmData

    let getAllRuntimeLibraries (installModel:InstallModel) =
        getAllFiles installModel.RuntimeLibFolders (fun f -> f |> Set.toSeq)
        |> Seq.cache

    let getRuntimeLibraries (graph:RuntimeGraph) (rid:Rid) (target : TargetProfile) (installModel:InstallModel) =
        let allLibraries = getAllRuntimeLibraries installModel
        if allLibraries |> Seq.exists (fun r -> r.Rid.IsNone) then
            failwithf "Expected that all runtime libraries are associated with an RID."
        let allRids = allLibraries |> Seq.choose (fun s -> s.Rid) |> Set.ofSeq
        let bestMatchingRid =
            RuntimeGraph.getInheritanceList rid graph
            |> Seq.tryFind (fun rid -> allRids.Contains rid)
        let tfmData =
            getFileFolders target (installModel.RuntimeLibFolders) (Set.toSeq)
            |> Seq.filter (fun lib -> lib.Rid = bestMatchingRid)
            |> Seq.cache
        let ridData =
            getFileFoldersByPath Tfm.Empty installModel.RuntimeLibFolders Set.toSeq
            |> Seq.filter (fun lib -> lib.Rid = bestMatchingRid)
            |> Seq.cache
        if verbose && allRids.Count > 0 && bestMatchingRid = None then
            // No idea what this means, if this fails for you make this a warning or remove it completely.
            // I added this to see if it appears in the real world...
            printfn "We found RID dependency assemblies in the package '%A'-'%A' but nothing matched against '%A'" installModel.PackageName installModel.PackageVersion rid
        Seq.append tfmData ridData


    let mapFolderContents<'T> (mapfn: 'T -> 'T) folders =
        folders
        |> List.map (fun p ->
            { p with FolderContents = mapfn p.FolderContents })

    let mapCompileLibFolders mapfn (installModel:InstallModel) =
        { installModel with
            CompileLibFolders = List.map mapfn installModel.CompileLibFolders }
    let mapCompileRefFolders mapfn (installModel:InstallModel) =
        { installModel with
            CompileRefFolders = List.map mapfn installModel.CompileRefFolders }
    let mapRuntimeAssemblyFolders mapfn (installModel:InstallModel) =
        { installModel with
            RuntimeAssemblyFolders = List.map mapfn installModel.RuntimeAssemblyFolders }
    let mapTargetsFileFolders mapfn (installModel:InstallModel) =
        { installModel with
            TargetsFileFolders = List.map mapfn installModel.TargetsFileFolders }

    let mapCompileLibFrameworkReferences mapfn (installModel:InstallModel) =
        mapCompileLibFolders (FrameworkFolder.map (fun l -> { l with FrameworkReferences = mapfn l.FrameworkReferences })) installModel
    let mapCompileLibReferences mapfn (installModel:InstallModel) =
        mapCompileLibFolders (FrameworkFolder.map (fun l -> { l with Libraries = mapfn l.Libraries })) installModel
    let mapCompileRefFiles mapfn (installModel:InstallModel) =
        mapCompileRefFolders (FrameworkFolder.map (mapfn)) installModel
    let mapRuntimeAssemblyFiles mapfn (installModel:InstallModel) =
        mapRuntimeAssemblyFolders (FrameworkFolder.map (mapfn)) installModel
    let mapTargetsFiles mapfn (installModel:InstallModel) =
        mapTargetsFileFolders (FrameworkFolder.map (mapfn)) installModel

    let addFrameworkAssemblyReference (installModel:InstallModel) (reference:FrameworkAssemblyReference) : InstallModel =
        let referenceApplies (folder : FrameworkFolder<_>) =
            applyRestrictionsToTargets (reference.FrameworkRestrictions |> getExplicitRestriction) (folder.Targets)
            |> Seq.isEmpty
            |> not

        let model =
            if List.isEmpty installModel.CompileLibFolders then
                // TODO: Ask forki about this wtf
                let folders = calcLegacyReferenceLibFolders [{ FullPath ="lib/Default.dll"; PathWithinPackage = "lib/Default.dll" }]
                { installModel with CompileLibFolders = folders }
            else
                installModel

        model |> mapCompileLibFolders(fun folder ->
            if referenceApplies folder then
                FrameworkFolder.map (fun c -> { c with FrameworkReferences = Set.add (FrameworkReference.ofName reference.AssemblyName) c.FrameworkReferences }) folder
            else
                folder)

    let addFrameworkAssemblyReferences references (installModel:InstallModel) : InstallModel =
        references |> Seq.fold addFrameworkAssemblyReference (installModel:InstallModel)



    let filterExcludes (excludes:string list) (installModel:InstallModel) =
        let excluded e (pathOrName:string) =
            pathOrName.Contains e

        excludes
        |> List.fold (fun (model:InstallModel) fileName ->
            model
            |> mapCompileLibReferences (Set.filter (fun n -> n.PathWithinPackage |> excluded fileName |> not))
            |> mapCompileLibFrameworkReferences (Set.filter (fun r -> r.Name |> excluded fileName |> not))
          ) installModel

    let filterUnknownFiles (installModel:InstallModel) =
        let ofRuntimeLibrary (l:RuntimeLibrary) = l.Library
        let isAssemblyDll (l:Library) =
            let lib = l.Path
            String.endsWithIgnoreCase ".dll" lib || String.endsWithIgnoreCase ".exe" lib
        let isNativeDll (l:Library) =
            let lib = l.Path
            isAssemblyDll l || String.endsWithIgnoreCase ".so" lib || String.endsWithIgnoreCase ".dylib" lib
        installModel
        |> mapCompileLibReferences (Set.filter isAssemblyDll)
        |> mapCompileRefFiles (Set.filter isAssemblyDll)
        |> mapRuntimeAssemblyFiles (Set.filter (ofRuntimeLibrary >> isNativeDll))
        |> mapCompileLibReferences (Set.filter (fun lib -> not (lib.Path.EndsWith ".resources.dll")))
        |> mapTargetsFiles (Set.filter (fun t ->
            let targetsFile = t.Path
            (String.endsWithIgnoreCase (sprintf "%s.props" installModel.PackageName.Name) targetsFile||
             String.endsWithIgnoreCase (sprintf "%s.targets" installModel.PackageName.Name) targetsFile)))
    let filterBlackList = filterUnknownFiles

    let applyFrameworkRestrictions (restriction:FrameworkRestriction) (installModel:InstallModel) =
        match restriction with
        | FrameworkRestriction.HasNoRestriction -> installModel
        | restriction ->
            let applyRestriction folder =
                { folder with Targets = applyRestrictionsToTargets restriction folder.Targets}

            { installModel with
                CompileLibFolders =
                    installModel.CompileLibFolders
                    |> List.map applyRestriction
                    |> List.filter (fun folder -> not folder.Targets.IsEmpty)

                CompileRefFolders =
                    installModel.CompileRefFolders
                    |> List.map applyRestriction
                    |> List.filter (fun folder -> not folder.Targets.IsEmpty)

                RuntimeAssemblyFolders =
                    installModel.RuntimeAssemblyFolders
                    |> List.map applyRestriction
                    |> List.filter (fun folder -> not folder.Targets.IsEmpty)

                TargetsFileFolders =
                    installModel.TargetsFileFolders
                    |> List.map applyRestriction
                    |> List.filter (fun folder -> not folder.Targets.IsEmpty)
            }

    let rec addTargetsFiles (targetsFiles:UnparsedPackageFile list) (this:InstallModel) : InstallModel =
        let targetsFileFolders =
            calcLibFoldersG Set.empty getMsbuildFile targetsFiles
        { this with TargetsFileFolders = targetsFileFolders }
            |> addItem targetsFiles getMsbuildFile (addTargetsFile) (fun i -> i.TargetsFileFolders)


    let filterReferences (references:string Set) (this:InstallModel) =
        this
        |> mapCompileLibReferences (Set.filter (fun reference -> Set.contains reference.Name references |> not))
        |> mapCompileLibFrameworkReferences (Set.filter (fun reference -> Set.contains reference.Name references |> not))

    let addLicense url (model: InstallModel) =
        if String.IsNullOrWhiteSpace url then model
        else  { model with LicenseUrl = Some url }

    let addLibReferences (libs:UnparsedPackageFile seq) references (installModel:InstallModel) : InstallModel =
        let libs = libs |> Seq.toList
        let legacyLibFolders = calcLegacyReferenceLibFolders libs
        let refFolders = calcReferenceFolders libs
        let runtimeAssemblyFolders = calcRuntimeAssemblyFolders libs
        let runtimeLibraryFolders = calcRuntimeLibraryFolders libs

        { installModel with
            CompileLibFolders = legacyLibFolders
            CompileRefFolders = refFolders
            RuntimeAssemblyFolders = runtimeAssemblyFolders
            RuntimeLibFolders = runtimeLibraryFolders
        }
        |> addItem libs getCompileLibAssembly (addPackageLegacyLibFile references) (fun i -> i.CompileLibFolders)
        |> addItem libs getCompileRefAssembly (addPackageRefFile references) (fun i -> i.CompileRefFolders)
        |> addItem libs getRuntimeAssembly (addPackageRuntimeAssemblyFile references) (fun i -> i.RuntimeAssemblyFolders)
        |> addItem libs getRuntimeLibrary (addPackageRuntimeLibraryFile references) (fun i -> i.RuntimeLibFolders)

    let addNuGetFiles (content:NuGetPackageContent) (model:InstallModel) : InstallModel =
        let asList o = defaultArg o []
        let analyzers = NuGet.tryFindFolder "analyzers" content |> asList
        let lib = NuGet.tryFindFolder "lib" content |> asList
        let ref = NuGet.tryFindFolder "ref" content |> asList
        let runtimes = NuGet.tryFindFolder "runtimes" content |> asList
        let build = NuGet.tryFindFolder "build" content |> asList

        model
        |> addLibReferences (lib @ ref @ runtimes) content.Spec.References
        |> addTargetsFiles build
        |> addAnalyzerFiles analyzers
        |> addFrameworkAssemblyReferences content.Spec.FrameworkAssemblyReferences
        |> addLicense content.Spec.LicenseUrl
        |> filterUnknownFiles

    let createFromContent packageName packageVersion frameworkRestrictions content =
        emptyModel packageName packageVersion
        |> addNuGetFiles content
        |> filterBlackList
        |> applyFrameworkRestrictions frameworkRestrictions
        |> removeIfCompletelyEmpty

    [<Obsolete "use createFromContent instead">]
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


    member this.GetLegacyReferences target = InstallModel.getLegacyReferences target this
    member this.GetCompileReferences target = InstallModel.getCompileReferences target this
    member this.GetRuntimeAssemblies graph rid target = InstallModel.getRuntimeAssemblies graph rid target this
    member this.GetRuntimeLibraries graph rid target = InstallModel.getRuntimeLibraries graph rid target this

    [<Obsolete("usually this should not be used, use GetLegacyReferences for the full .net and GetCompileReferences for dotnetcore")>]
    member this.GetLibReferences frameworkIdentifier = InstallModel.getLegacyPlatformReferences frameworkIdentifier this

    member this.GetLibReferenceFiles frameworkIdentifier =
        InstallModel.getLegacyPlatformReferences frameworkIdentifier this
        |> Seq.map (fun lib -> FileInfo lib.Path)

    member this.GetLegacyAndCompileReferences target =
        Seq.append
            (this.GetLegacyReferences target)
            (this.GetCompileReferences target)


    member this.GetLegacyReferenceFiles  target =
        InstallModel.getLegacyReferences target this
        |> Seq.map (fun lib -> FileInfo lib.Path)


    member this.GetCompileReferenceFiles target =
        InstallModel.getCompileReferences target this
        |> Seq.map (fun lib -> FileInfo lib.Path)


    member this.GetLegacyAndCompileReferenceFiles target =
        Seq.append
            (this.GetLegacyReferenceFiles target)
            (this.GetCompileReferenceFiles target)


    member this.GetTargetsFiles target =
        InstallModel.getTargetsFiles target this

    member this.GetAllLegacyFrameworkReferences () = InstallModel.getAllLegacyFrameworkReferences this
    member this.GetAllLegacyReferences () = InstallModel.getAllLegacyReferences this
    member this.GetAllLegacyReferenceAndFrameworkReferenceNames () =
        this.GetAllLegacyFrameworkReferences() |> Seq.map (fun r -> r.Name)
        |> Seq.append (this.GetAllLegacyReferences() |> Seq.map (fun r -> r.Name))
        |> Set.ofSeq

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

    static member CreateFromContent(packageName, packageVersion, frameworkRestriction:FrameworkRestriction, content : NuGetPackageContent) =
        InstallModel.createFromContent packageName packageVersion frameworkRestriction content

    [<Obsolete "use CreateFromContent instead">]
    static member CreateFromLibs(packageName, packageVersion, frameworkRestriction:FrameworkRestriction, libs : UnparsedPackageFile seq, targetsFiles, analyzerFiles, nuspec : Nuspec) =
        InstallModel.createFromLibs packageName packageVersion frameworkRestriction libs targetsFiles analyzerFiles nuspec