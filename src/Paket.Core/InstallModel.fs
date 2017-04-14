namespace Paket

open System
open System.IO
open Paket.Domain
open Paket.Requirements
open Logging

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

/// Represents a subfolder of a nuget package that provides files (content, references, etc) for one or more Target Profiles.  This is a logical representation of the 'net45' folder in a NuGet package, for example.
type LibFolder =
    { Name : string
      Targets : TargetProfile list
      Files : InstallFiles}

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
      LegacyReferenceFileFolders : LibFolder list
      NewReferenceFileFolders : LibFolder list
      TargetsFileFolders : LibFolder list
      Analyzers: AnalyzerLib list
      LicenseUrl: string option }

module FolderScanner =
    // Stolen and modifed to our needs from http://www.fssnip.net/4I/title/sscanf-parsing-with-format-strings
    open System
    open System.Text
    open System.Text.RegularExpressions
    open Microsoft.FSharp.Reflection

    let check f x = if f x then x
                    else failwithf "format failure \"%s\"" x

    let parseDecimal x = Decimal.Parse(x, System.Globalization.CultureInfo.InvariantCulture)

    let parsers = dict [
                     'b', Boolean.Parse >> box
                     'd', int >> box
                     'i', int >> box
                     's', box
                     'u', uint32 >> int >> box
                     'x', check (String.forall Char.IsLower) >> ((+) "0x") >> int >> box
                     'X', check (String.forall Char.IsUpper) >> ((+) "0x") >> int >> box
                     'o', ((+) "0o") >> int >> box
                     'e', float >> box // no check for correct format for floats
                     'E', float >> box
                     'f', float >> box
                     'F', float >> box
                     'g', float >> box
                     'G', float >> box
                     'M', parseDecimal >> box
                     'c', char >> box
                    ]

    //let advancedParsers = dict [
    //    "",
    //]

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
       //| '%'::'A'::xr -> getFormatters xr
       | '%'::x::xr -> if parsers.ContainsKey x then x::getFormatters xr
                       else failwithf "Unknown formatter %%%c" x
       | x::xr -> getFormatters xr
       | [] -> []


    let sscanf (pf:PrintfFormat<_,_,_,_,'t>) s : 't =
      let formatStr = pf.Value.Replace("%%", "%")
      let constants = formatStr.Split(separators, StringSplitOptions.None)
      let regex = Regex("^" + String.Join("(.*?)", constants |> Array.map Regex.Escape) + "$")
      let formatters = pf.Value.ToCharArray() // need original string here (possibly with "%%"s)
                       |> Array.toList |> getFormatters
      let groups =
        regex.Match(s).Groups
        |> Seq.cast<Group>
        |> Seq.skip 1
      let matches =
        (groups, formatters)
        ||> Seq.map2 (fun g f -> g.Value |> parsers.[f])
        |> Seq.toArray

      if matches.Length = 1 then matches.[0] :?> 't
      else FSharpValue.MakeTuple(matches, typeof<'t>) :?> 't

    // some basic testing
    //let (a,b) = sscanf "(%%%s,%M)" "(%hello, 4.53)"
    //let (x,y,z) = sscanf "%s-%s-%s" "test-this-string"
    //let (c,d,e,f,g,h,i) = sscanf "%b-%d-%i,%u,%x,%X,%o" "false-42--31,13,ff,FF,42"
    //let (j,k,l,m,n,o,p) = sscanf "%f %F %g %G %e %E %c" "1 2.1 3.4 .3 43.2e32 0 f"
    //let (t:string, asdfy: obj) = sscanf "%A %A" "asdas"

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module InstallModel =
    // A lot of insights can be gained from https://github.com/NuGet/NuGet.Client/blob/85731166154d0818d79a19a6d2417de6aa851f39/src/NuGet.Core/NuGet.Packaging/ContentModel/ManagedCodeConventions.cs#L385-L505
    // if you read this update the hash ;)
    open Logging

    let emptyModel packageName packageVersion =
        { PackageName = packageName
          PackageVersion = packageVersion
          LegacyReferenceFileFolders = []
          NewReferenceFileFolders = []
          TargetsFileFolders = []
          Analyzers = []
          LicenseUrl = None }

    let getReferenceFolders (installModel: InstallModel) =
        installModel.LegacyReferenceFileFolders @
        (installModel.NewReferenceFileFolders
         |> List.choose (fun r ->
            match installModel.LegacyReferenceFileFolders |> List.tryFind (fun r2 -> r2.Targets = r.Targets) with
            | None -> Some r
            | _ -> None))

    //let getLibraryFolders (installModel: InstallModel) =
    //    if installModel.NewReferenceFileFolders.IsEmpty then
    //      installModel.LegacyReferenceFileFolders
    //    else installModel.NewReferenceFileFolders

    let extractRefFolder packageName (path:UnparsedPackageFile) =
        let path = path.FullPath.Replace("\\", "/").ToLower()
        Utils.extractPath ("ref", packageName, path)

    let extractLibFolder packageName (path:UnparsedPackageFile) =
        let path = path.FullPath.Replace("\\", "/").ToLower()
        if path.Contains "runtimes" then
            Utils.extractPath ("runtimes", packageName, path)
        else
            Utils.extractPath ("lib", packageName, path)

    let extractBuildFolder packageName (path:UnparsedPackageFile) = Utils.extractPath ("build", packageName, path.FullPath)

    let mapFolders mapfn (installModel:InstallModel) =
        { installModel with
            LegacyReferenceFileFolders = List.map mapfn installModel.LegacyReferenceFileFolders
            NewReferenceFileFolders = List.map mapfn installModel.NewReferenceFileFolders
            TargetsFileFolders   = List.map mapfn installModel.TargetsFileFolders  }

    let mapFiles mapfn (installModel:InstallModel) =
        installModel
        |> mapFolders (fun folder -> { folder with Files = mapfn folder.Files })

    let private getFileFolders (target:TargetProfile)  folderType choosefn =
        match Seq.tryFind (fun lib -> Seq.exists ((=) target) lib.Targets) folderType with
        | Some folder -> folder.Files.References |> Seq.choose choosefn
        | None -> Seq.empty

    let getLibReferences (target : TargetProfile) installModel =
        let results =
          getFileFolders target (getReferenceFolders installModel) (function Reference.Library lib -> Some lib | _ -> None)
          |> Seq.cache
        if results |> Seq.isEmpty then
          getFileFolders target installModel.LegacyReferenceFileFolders (function Reference.Library lib -> Some lib | _ -> None)
        else results

    let getTargetsFiles (target : TargetProfile) (installModel:InstallModel) =
        getFileFolders target installModel.TargetsFileFolders
            (function Reference.TargetsFile targetsFile -> Some targetsFile | _ -> None)

    let getPlatformReferences frameworkIdentifier installModel =
        getLibReferences (SinglePlatform frameworkIdentifier) installModel

    let getFrameworkAssembliesLazy installModel =
        lazy ([ for lib in getReferenceFolders installModel do
                    yield! lib.Files.GetFrameworkAssemblies()]
              |> Set.ofList)

    let getLibReferencesLazy installModel =
        lazy ([ for lib in getReferenceFolders installModel do
                    yield! lib.Files.References]
              |> Set.ofList)

    let getTargetsFilesLazy (installModel:InstallModel) =
        lazy ([ for lib in installModel.TargetsFileFolders do
                    yield! lib.Files.References]
              |> Set.ofList)

    let removeIfCompletelyEmpty (this:InstallModel) =
        if Set.isEmpty (getFrameworkAssembliesLazy this |> force)
         && Set.isEmpty (getLibReferencesLazy this |> force)
         && Set.isEmpty (getTargetsFilesLazy this |> force)
         && List.isEmpty this.Analyzers then
            emptyModel this.PackageName this.PackageVersion
        else
            this

    let calcLibFoldersG extract packageName (libs:UnparsedPackageFile list) =
       libs
        |> List.choose (extract packageName)
        |> List.distinct
        |> List.sort
        |> PlatformMatching.getSupportedTargetProfiles
        |> Seq.map (fun entry -> { Name = entry.Key; Targets = entry.Value; Files = InstallFiles.empty })
        |> Seq.toList

    let calcLibFolders = calcLibFoldersG extractLibFolder
    let calcRefFolders = calcLibFoldersG extractRefFolder

    let addFileToFolder (path:LibFolder) (file:UnparsedPackageFile) (folders:LibFolder list) (addfn: string -> InstallFiles -> InstallFiles) =
        folders
        |> List.map (fun p ->
            if p.Name <> path.Name then p else
            { p with Files = addfn file.FullPath p.Files })

    let private addPackageFile (path:LibFolder) (file:UnparsedPackageFile) references (this:InstallModel) : InstallModel =
        let install =
            match references with
            | NuspecReferences.All -> true
            | NuspecReferences.Explicit list -> List.exists file.FullPath.EndsWith list

        if not install then this else
        { this with
            LegacyReferenceFileFolders = addFileToFolder path file this.LegacyReferenceFileFolders InstallFiles.addReference }

    let private addPackageRefFile (path:LibFolder) (file:UnparsedPackageFile) references (this:InstallModel) : InstallModel =
        let install =
            match references with
            | NuspecReferences.All -> true
            | NuspecReferences.Explicit list -> List.exists file.FullPath.EndsWith list

        if not install then this else
        { this with
            NewReferenceFileFolders = addFileToFolder path file this.NewReferenceFileFolders InstallFiles.addReference }

    let addLibReferences (libs:UnparsedPackageFile seq) references (installModel:InstallModel) : InstallModel =
        let libs = libs |> Seq.toList
        let libFolders = calcLibFolders installModel.PackageName libs
        let refFolders = calcRefFolders installModel.PackageName libs

        let addItem extract addFunc getFolder initialState =
          List.fold (fun (model:InstallModel) file ->
              match extract installModel.PackageName file with
              | Some folderName ->
                  match List.tryFind (fun folder -> folder.Name = folderName) (getFolder model) with
                  | Some path -> addFunc path file references model
                  | _ -> model
              | None -> model) initialState libs

        let newState = addItem extractLibFolder addPackageFile (fun i -> i.LegacyReferenceFileFolders) { installModel with LegacyReferenceFileFolders = libFolders }
        addItem extractRefFolder addPackageRefFile (fun i -> i.NewReferenceFileFolders) { newState with NewReferenceFileFolders = refFolders }

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
            if List.isEmpty installModel.LegacyReferenceFileFolders then
                let folders = calcLibFolders installModel.PackageName [{ FullPath ="lib/Default.dll"; PathWithinPackage = "lib/Default.dll" }]
                { installModel with LegacyReferenceFileFolders = folders }
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
                LegacyReferenceFileFolders =
                    installModel.LegacyReferenceFileFolders
                    |> List.map applRestriction
                    |> List.filter (fun folder -> folder.Targets <> [])

                NewReferenceFileFolders =
                    installModel.NewReferenceFileFolders
                    |> List.map applRestriction
                    |> List.filter (fun folder -> folder.Targets <> [])

                TargetsFileFolders =
                    installModel.TargetsFileFolders
                    |> List.map applRestriction
                    |> List.filter (fun folder -> folder.Targets <> [])  }

    let rec addTargetsFiles (targetsFiles:UnparsedPackageFile list) (this:InstallModel) : InstallModel =
        let targetsFileFolders =
            targetsFiles
            |> List.choose (extractBuildFolder this.PackageName)
            |> List.distinct
            |> PlatformMatching.getSupportedTargetProfiles
            |> Seq.map (fun entry -> { Name = entry.Key; Targets = List.ofSeq entry.Value; Files = InstallFiles.empty })
            |> Seq.toList

        List.fold (fun model file ->
            match extractBuildFolder this.PackageName file with
            | Some folderName ->
                match List.tryFind (fun folder -> folder.Name = folderName) model.TargetsFileFolders with
                | Some path -> addTargetsFile path file model
                | _ -> model
            | None -> model) { this with TargetsFileFolders = targetsFileFolders } targetsFiles


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

    member this.GetReferenceFolders() = InstallModel.getReferenceFolders this

    member this.MapFolders mapfn = InstallModel.mapFolders mapfn this

    member this.MapFiles mapfn = InstallModel.mapFiles mapfn this

    member this.GetLibReferences target = InstallModel.getLibReferences target this

    member this.GetLibReferences frameworkIdentifier = InstallModel.getPlatformReferences frameworkIdentifier this

    member this.GetTargetsFiles target = InstallModel.getTargetsFiles target this

    member this.GetFrameworkAssembliesLazy =  InstallModel.getFrameworkAssembliesLazy this

    member this.GetLibReferencesLazy = InstallModel.getLibReferencesLazy this

    member this.GetTargetsFilesLazy =  InstallModel.getTargetsFilesLazy this

    member this.CalcLibFolders libs = InstallModel.calcLibFolders libs

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
