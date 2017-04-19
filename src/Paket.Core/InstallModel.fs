namespace Paket

open System
open System.IO
open Paket.Domain
open Paket.Requirements
open Logging
open PlatformMatching
open ProviderImplementation.AssemblyReader.Utils.SHA1

type UnparsedPackageFile = Paket.NuGet.UnparsedPackageFile

type Library =
    { Name : string
      Path : string }
module Library =
    let ofFile (f:UnparsedPackageFile) =
        let fi = FileInfo(normalizePath f.FullPath)
        let name = fi.Name.Replace(fi.Extension, "")
        { Name = name; Path = f.FullPath }

type MsBuildFile =
    { Name : string
      Path : string }
module MsBuildFile =
    let ofFile (f:UnparsedPackageFile) =
        let fi = FileInfo(normalizePath f.FullPath)
        let name = fi.Name.Replace(fi.Extension, "")
        { Name = name; Path = f.FullPath }

type FrameworkReference =
    { Name : string }
module FrameworkReference =
    let ofName n = { FrameworkReference.Name = n }

type ReferenceOrLibraryFolder =
   { FrameworkReferences : FrameworkReference Set
     Libraries : Library Set }

module ReferenceOrLibraryFolder =
   let empty = { FrameworkReferences = Set.empty; Libraries = Set.empty }
   let addLibrary item old =
      { old with ReferenceOrLibraryFolder.Libraries = Set.add item old.Libraries }
   let addFrameworkReference item old =
      { old with ReferenceOrLibraryFolder.FrameworkReferences = Set.add item old.FrameworkReferences }

/// Represents a subfolder of a nuget package that provides files (content, references, etc) for one or more Target Profiles.  This is a logical representation of the 'net45' folder in a NuGet package, for example.
type LibFolder<'T> =
    { Path : ParsedPlatformPath
      Targets : TargetProfile list
      FolderContents : 'T  }

    member this.GetSinglePlatforms() =
        this.Targets
        |> List.choose (function SinglePlatform t -> Some t | _ -> None)

module LibFolder =
    let map f (l:LibFolder<_>) =
        { Path = l.Path
          Targets = l.Targets
          FolderContents = f l.FolderContents }

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
      CompileLibFolders : LibFolder<ReferenceOrLibraryFolder> list
      CompileRefFolders : LibFolder<Library Set> list
      //RuntimeLibFolders : LibFolder<Library Set> list
      RuntimeAssemblyFolders : LibFolder<Library Set> list
      TargetsFileFolders : LibFolder<MsBuildFile Set> list
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
          //RuntimeLibFolders = []
          RuntimeAssemblyFolders = []
          TargetsFileFolders = []
          Analyzers = []
          LicenseUrl = None }

    type Tfm = PlatformMatching.ParsedPlatformPath
    type Rid = Paket.Rid
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
        Runtime : Rid }

    let getCompileRefAssembly (p:UnparsedPackageFile) =
        (trySscanf "ref/%A{tfm}/%A{noSeperator}" p.PathWithinPackage : (Tfm * string) option)
        |> Option.map (fun (l,_) -> { Path = l; File = p; Runtime = Rid.Any })

    let getRuntimeAssembly (p:UnparsedPackageFile) =
        (trySscanf "lib/%A{tfm}/%A{noSeperator}" p.PathWithinPackage : (Tfm * string) option)
        |> Option.map (fun (l,_) -> { Path = l; File = p; Runtime = Rid.Any })
        |> Option.orElseWith (fun _ ->
            (trySscanf "runtimes/%A{rid}/lib/%A{tfm}/%A{noSeperator}" p.PathWithinPackage : (Rid * Tfm * string) option)
            |> Option.map (fun (rid, l, _) -> { Path = l; File = p; Runtime = rid }))
        |> Option.orElseWith (fun _ ->
            (trySscanf "lib/%A{noSeperator}" p.PathWithinPackage : string option)
            |> Option.map (fun (_) -> { Path = Tfm.Empty; File = p; Runtime = Rid.Any }))

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
                { Path = { l with Platforms = [ FrameworkIdentifier.Native(newBuildMode,newPlatform) ]}; File = p; Runtime = Rid.Any }
            else
            { Path = l; File = p; Runtime = Rid.Any })
        |> Option.orElseWith (fun _ ->
            (trySscanf "lib/%A{noSeperator}" p.PathWithinPackage : string option)
            |> Option.map (fun (_) -> { Path = Tfm.Empty; File = p; Runtime = Rid.Any }))

    let getRuntimeLibrary (p:UnparsedPackageFile) =
        (trySscanf "runtimes/%A{rid}/nativeassets/%A{tfm}/%A{noSeperator}" p.PathWithinPackage : (Rid * Tfm * string) option)
        |> Option.map (fun (rid, l,_) -> { Path = l; File = p; Runtime = rid })
        |> Option.orElseWith (fun _ ->
            (trySscanf "runtimes/%A{rid}/native/%A{noSeperator}" p.PathWithinPackage : (Rid * string) option)
            |> Option.map (fun (rid, _) -> { Path = Tfm.Empty; File = p; Runtime = rid }))

    let getMsbuildFile (p:UnparsedPackageFile) =
        (trySscanf "build/%A{tfm}/%A{noSeperator}" p.PathWithinPackage : (Tfm * string) option)
        |> Option.map (fun (l,_) -> { Path = l; File = p; Runtime = Rid.Any })
        |> Option.orElseWith (fun _ ->
            (trySscanf "build/%A{noSeperator}" p.PathWithinPackage : string option)
            |> Option.map (fun (_) -> { Path = Tfm.Empty; File = p; Runtime = Rid.Any }))

    //let mapFolders mapfn (installModel:InstallModel) =
    //    { installModel with
    //        CompileLibFolders = List.map mapfn installModel.CompileLibFolders
    //        CompileRefFolders = List.map mapfn installModel.CompileRefFolders
    //        RuntimeLibFolders = List.map mapfn installModel.RuntimeLibFolders
    //        TargetsFileFolders   = List.map mapfn installModel.TargetsFileFolders  }
    //
    //let mapFiles mapfn (installModel:InstallModel) =
    //    installModel
    //    |> mapFolders (fun folder -> { folder with Files = mapfn folder.Files })

    let private getFileFolders (target:TargetProfile)  folderType choosefn =
        match Seq.tryFind (fun lib -> Seq.exists ((=) target) lib.Targets) folderType with
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

    let getRuntimeAssemblies graph rid (target : TargetProfile) (installModel:InstallModel) =
        getFileFolders target (installModel.RuntimeAssemblyFolders) (fun f -> f |> Set.toSeq)
        |> Seq.cache

    let getTargetsFiles (target : TargetProfile) (installModel:InstallModel) =
        getFileFolders target installModel.TargetsFileFolders (fun f -> f |> Set.toSeq)

    /// This is for library references, which at the same time can be used for references (old world - pre dotnetcore)
    let getLegacyPlatformReferences frameworkIdentifier installModel =
        getLegacyReferences (SinglePlatform frameworkIdentifier) installModel

    let isEmpty (lib: LibFolder<Set<'T>> list) =
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
    //let calcRefFolders = calcLibFoldersG extractRefFolder

    let addFileToFolder<'T, 'Item> (path:LibFolder<'T>) (file:'Item) (folders:LibFolder<'T> list) (addfn: 'Item -> 'T -> 'T) =
        folders
        |> List.map (fun p ->
            if p.Path <> path.Path then p else
            { p with FolderContents = addfn file p.FolderContents })

    let private addPackageLegacyLibFile references (path:LibFolder<ReferenceOrLibraryFolder>) (file:UnparsedPackageFile) (this:InstallModel) : InstallModel =
        let install =
            match references with
            | NuspecReferences.All -> true
            | NuspecReferences.Explicit list -> List.exists file.FullPath.EndsWith list
        if not install then this else
        { this with
            CompileLibFolders = addFileToFolder path (Library.ofFile file) this.CompileLibFolders ReferenceOrLibraryFolder.addLibrary }

    let private addPackageRefFile references (path:LibFolder<Library Set>) (file:UnparsedPackageFile) (this:InstallModel) : InstallModel =
        let install =
            match references with
            | NuspecReferences.All -> true
            | NuspecReferences.Explicit list -> List.exists file.FullPath.EndsWith list

        if not install then this else
        { this with
            CompileRefFolders = addFileToFolder path (Library.ofFile file) this.CompileRefFolders Set.add }

    let private addPackageRuntimeAssemblyFile references (path:LibFolder<Library Set>) (file:UnparsedPackageFile) (this:InstallModel) : InstallModel =
        let install =
            match references with
            | NuspecReferences.All -> true
            | NuspecReferences.Explicit list -> List.exists file.FullPath.EndsWith list

        if not install then this else
        { this with
            RuntimeAssemblyFolders = addFileToFolder path (Library.ofFile file) this.RuntimeAssemblyFolders Set.add }

    let private addItem libs extract addFunc getFolder initialState =
        List.fold (fun (model:InstallModel) file ->
            match extract file with
            | Some (folderName:FrameworkDependentFile) ->
                match List.tryFind (fun (folder:LibFolder<_>) -> folder.Path = folderName.Path) (getFolder model) with
                | Some path -> addFunc path file model
                | _ -> model
            | None -> model) initialState libs

    let addLibReferences (libs:UnparsedPackageFile seq) references (installModel:InstallModel) : InstallModel =
        let libs = libs |> Seq.toList
        let legacyLibFolders = calcLegacyReferenceLibFolders libs
        let refFolders = calcReferenceFolders libs
        let runtimeAssemblyFolders = calcRuntimeAssemblyFolders libs

        { installModel with
            CompileLibFolders = legacyLibFolders
            CompileRefFolders = refFolders
            RuntimeAssemblyFolders = runtimeAssemblyFolders }
        |> addItem libs getCompileLibAssembly (addPackageLegacyLibFile references) (fun i -> i.CompileLibFolders)
        |> addItem libs getCompileRefAssembly (addPackageRefFile references) (fun i -> i.CompileRefFolders)
        |> addItem libs getRuntimeAssembly (addPackageRuntimeAssemblyFile references) (fun i -> i.RuntimeAssemblyFolders)

    let addAnalyzerFiles (analyzerFiles:UnparsedPackageFile seq) (installModel:InstallModel)  : InstallModel =
        let analyzerLibs =
            analyzerFiles
            |> Seq.map (fun file -> FileInfo file.FullPath |> AnalyzerLib.FromFile)
            |> List.ofSeq

        { installModel with Analyzers = installModel.Analyzers @ analyzerLibs}


    let rec addTargetsFile (path:LibFolder<_>)  (file:UnparsedPackageFile) (installModel:InstallModel) :InstallModel =
        { installModel with
            TargetsFileFolders = addFileToFolder path (MsBuildFile.ofFile file) installModel.TargetsFileFolders Set.add
        }

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
        mapCompileLibFolders (LibFolder.map (fun l -> { l with FrameworkReferences = mapfn l.FrameworkReferences })) installModel
    let mapCompileLibReferences mapfn (installModel:InstallModel) =
        mapCompileLibFolders (LibFolder.map (fun l -> { l with Libraries = mapfn l.Libraries })) installModel
    let mapCompileRefFiles mapfn (installModel:InstallModel) =
        mapCompileRefFolders (LibFolder.map (mapfn)) installModel
    let mapRuntimeAssemblyFiles mapfn (installModel:InstallModel) =
        mapRuntimeAssemblyFolders (LibFolder.map (mapfn)) installModel
    let mapTargetsFiles mapfn (installModel:InstallModel) =
        mapTargetsFileFolders (LibFolder.map (mapfn)) installModel

    let addFrameworkAssemblyReference (installModel:InstallModel) (reference:FrameworkAssemblyReference) : InstallModel =
        let referenceApplies (folder : LibFolder<_>) =
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

        model |> mapCompileLibFolders(fun folder ->
            if referenceApplies folder then
                LibFolder.map (fun c -> { c with FrameworkReferences = Set.add (FrameworkReference.ofName reference.AssemblyName) c.FrameworkReferences }) folder
                //{ folder with FolderContents = { folder.FolderContents with FrameworkReferences = Set.add reference.AssemblyName  folder.FolderContents.FrameworkReferences } }
            else
                folder)

    let addFrameworkAssemblyReferences references (installModel:InstallModel) : InstallModel =
        references |> Seq.fold addFrameworkAssemblyReference (installModel:InstallModel)



    let filterExcludes (excludes:string list) (installModel:InstallModel) =
        let excluded e (reference:string) =
            reference.Contains e

        excludes
        |> List.fold (fun (model:InstallModel) fileName ->
            model
            |> mapCompileLibReferences (Set.filter (fun n -> n.Name |> excluded fileName |> not))
            |> mapCompileLibFrameworkReferences (Set.filter (fun r -> r.Name |> excluded fileName |> not))
          ) installModel

    let filterBlackList (installModel:InstallModel) =
        installModel
        |> mapCompileLibReferences (Set.filter (fun l ->
            let lib = l.Path
            (String.endsWithIgnoreCase ".dll" lib || String.endsWithIgnoreCase ".exe" lib || String.endsWithIgnoreCase ".so" lib || String.endsWithIgnoreCase ".dylib" lib )))
        |> mapCompileLibReferences (Set.filter (fun lib -> not (lib.Path.EndsWith ".resources.dll")))
        |> mapTargetsFiles (Set.filter (fun t ->
            let targetsFile = t.Path
            (String.endsWithIgnoreCase ".props" targetsFile|| String.endsWithIgnoreCase ".targets" targetsFile)))

    let applyFrameworkRestrictions (restrictions:FrameworkRestriction list) (installModel:InstallModel) =
        match restrictions with
        | [] -> installModel
        | restrictions ->
            let applyRestriction folder =
                { folder with Targets = applyRestrictionsToTargets restrictions folder.Targets}

            { installModel with
                CompileLibFolders =
                    installModel.CompileLibFolders
                    |> List.map applyRestriction
                    |> List.filter (fun folder -> folder.Targets <> [])

                CompileRefFolders =
                    installModel.CompileRefFolders
                    |> List.map applyRestriction
                    |> List.filter (fun folder -> folder.Targets <> [])

                RuntimeAssemblyFolders =
                    installModel.RuntimeAssemblyFolders
                    |> List.map applyRestriction
                    |> List.filter (fun folder -> folder.Targets <> [])

                TargetsFileFolders =
                    installModel.TargetsFileFolders
                    |> List.map applyRestriction
                    |> List.filter (fun folder -> folder.Targets <> [])  }

    let rec addTargetsFiles (targetsFiles:UnparsedPackageFile list) (this:InstallModel) : InstallModel =
        let targetsFileFolders =
            calcLibFoldersG Set.empty getMsbuildFile targetsFiles
        { this with TargetsFileFolders = targetsFileFolders }
            |> addItem targetsFiles getMsbuildFile (addTargetsFile) (fun i -> i.TargetsFileFolders)


    let filterReferences (references:string Set) (this:InstallModel) =
        this
        |> mapCompileLibReferences (Set.filter (fun reference -> Set.contains reference.Name references |> not))
        |> mapCompileLibFrameworkReferences (Set.filter (fun reference -> Set.contains reference.Name references |> not))
        //let inline mapfn (files:InstallFiles) =
        //    { files with
        //        References = files.References |> Set.filter (fun reference -> Set.contains reference.ReferenceName references |> not)
        //    }
        //mapFiles mapfn this

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

    //member this.MapFolders mapfn = InstallModel.mapFolders mapfn this

    //member this.MapFiles mapfn = InstallModel.mapFiles mapfn this

    [<Obsolete("usually this should not be used, use GetLegacyReferences for the full .net and GetCompileReferences/GetRuntimeLibraries for dotnetcore")>]
    member this.GetLibReferences target = InstallModel.getLegacyReferences target this
    member this.GetLegacyReferences target = InstallModel.getLegacyReferences target this
    member this.GetCompileReferences target = InstallModel.getCompileReferences target this
    member this.GetRuntimeAssemblies graph rid target = InstallModel.getRuntimeAssemblies graph rid target this
    // TODO: 
    member this.GetRuntimeLibraries graph rid target = InstallModel.getRuntimeLibraries graph rid target this

    [<Obsolete("usually this should not be used, use GetLegacyReferences for the full .net and GetCompileReferences for dotnetcore")>]
    member this.GetLibReferences frameworkIdentifier = InstallModel.getLegacyPlatformReferences frameworkIdentifier this

    member this.GetTargetsFiles target = InstallModel.getTargetsFiles target this

    member this.GetAllLegacyFrameworkReferences () = InstallModel.getAllLegacyFrameworkReferences this
    member this.GetAllLegacyReferences () = InstallModel.getAllLegacyReferences this
    member this.GetAllLegacyReferenceAndFrameworkReferenceNames () =
        this.GetAllLegacyFrameworkReferences() |> Seq.map (fun r -> r.Name)
        |> Seq.append (this.GetAllLegacyReferences() |> Seq.map (fun r -> r.Name))
        |> Set.ofSeq
    //[<Obsolete("usually this should not be used, use GetLegacyFrameworkAssembliesLazy for the full .net and remove this call for dotnetcore (dnc has no reference assemblies)")>]
    //member this.GetFrameworkAssembliesLazy =  InstallModel.getLegacyFrameworkAssembliesLazy this
    //member this.GetLegacyFrameworkAssembliesLazy =  InstallModel.getLegacyFrameworkAssembliesLazy this

    //[<Obsolete("usually this should not be used, use GetLegacyReferencesLazy for the full .net and GetCompileReferencesLazy for dotnetcore")>]
    //member this.GetLibReferencesLazy = InstallModel.getLegacyReferencesLazy this
    //member this.GetLegacyReferencesLazy = InstallModel.getLegacyReferencesLazy this
    //member this.GetCompileReferencesLazy = InstallModel.getReferencesLazy this
    //
    //member this.GetTargetsFilesLazy =  InstallModel.getTargetsFilesLazy this

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
