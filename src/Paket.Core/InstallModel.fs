namespace Paket

open System.IO
open Paket.Domain
open Paket.Requirements

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

type InstallModel = 
    { PackageName : PackageName
      PackageVersion : SemVerInfo
      ReferenceFileFolders : LibFolder list
      TargetsFileFolders : LibFolder list
      Analyzers: AnalyzerLib list}


[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module InstallModel =

    let emptyModel packageName packageVersion = 
        { PackageName = packageName
          PackageVersion = packageVersion
          ReferenceFileFolders = []
          TargetsFileFolders = [] 
          Analyzers = [] }

    let extractLibFolder path = Utils.extractPath "lib" path

    let extractBuildFolder path = Utils.extractPath "build" path

    let mapFolders mapfn (installModel:InstallModel) = 
        { installModel with 
            ReferenceFileFolders = List.map mapfn installModel.ReferenceFileFolders
            TargetsFileFolders   = List.map mapfn installModel.TargetsFileFolders  }
    
    let mapFiles mapfn (installModel:InstallModel) = 
        installModel
        |> mapFolders (fun folder -> { folder with Files = mapfn folder.Files })

    let private getFileFolders (target:TargetProfile)  folderType choosefn = 
        match Seq.tryFind (fun lib -> Seq.exists ((=) target) lib.Targets) folderType with
        | Some folder -> folder.Files.References |> Seq.choose choosefn
        | None -> Seq.empty

    let getLibReferences (target : TargetProfile) (installModel:InstallModel) = 
        getFileFolders target installModel.ReferenceFileFolders (function Reference.Library lib -> Some lib | _ -> None)

    let getTargetsFiles (target : TargetProfile) (installModel:InstallModel) = 
        getFileFolders target installModel.TargetsFileFolders 
            (function Reference.TargetsFile targetsFile -> Some targetsFile | _ -> None)

    let hasReferences (installModel:InstallModel) = List.isEmpty installModel.ReferenceFileFolders |> not

    let getPlatformReferences frameworkIdentifier installModel = 
        getLibReferences (SinglePlatform frameworkIdentifier) installModel

    let getFrameworkAssembliesLazy (installModel:InstallModel) = 
        lazy ([ for lib in installModel.ReferenceFileFolders do
                    yield! lib.Files.GetFrameworkAssemblies()]
              |> Set.ofList)

    let getLibReferencesLazy (installModel:InstallModel) = 
        lazy ([ for lib in installModel.ReferenceFileFolders do
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


    let calcLibFolders libs =
        libs 
        |> Seq.choose extractLibFolder 
        |> Seq.distinct 
        |> List.ofSeq
        |> PlatformMatching.getSupportedTargetProfiles 
        |> Seq.map (fun entry -> { Name = entry.Key; Targets = List.ofSeq entry.Value; Files = InstallFiles.empty })
        |> Seq.toList

    let addFileToFolder (path:LibFolder) (file:string) (folders:LibFolder list) (addfn: string -> InstallFiles -> InstallFiles) =
            folders |> List.map (fun p -> 
                if p.Name <> path.Name then p else
                { p with Files = addfn file p.Files }) 
                
    let addPackageFile (path:LibFolder) (file:string) references (this:InstallModel) : InstallModel =
        let install = 
            match references with
            | NuspecReferences.All -> true
            | NuspecReferences.Explicit list -> List.exists file.EndsWith list

        if not install then this else
        { this with 
            ReferenceFileFolders = addFileToFolder path file this.ReferenceFileFolders InstallFiles.addReference }


    let addLibReferences (libs: string seq) references (installModel:InstallModel) : InstallModel =
        let libFolders = calcLibFolders libs

        Seq.fold (fun (model:InstallModel) file ->
            match extractLibFolder file with
            | Some folderName ->
                match Seq.tryFind (fun folder -> folder.Name = folderName) model.ReferenceFileFolders with
                | Some path -> addPackageFile path file references model
                | _ -> model
            | None -> model) { installModel with ReferenceFileFolders = libFolders } libs


    let addAnalyzerFiles (analyzerFiles:string seq) (installModel:InstallModel)  : InstallModel =
        let analyzerLibs =
            analyzerFiles
            |> Seq.map (fun file -> FileInfo file |> AnalyzerLib.FromFile)
            |> List.ofSeq
        { installModel with Analyzers = installModel.Analyzers @ analyzerLibs}


    let rec addTargetsFile (path:LibFolder)  (file:string) (installModel:InstallModel) :InstallModel =
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
            if List.isEmpty installModel.ReferenceFileFolders then
                let folders = calcLibFolders ["lib/Default.dll"]
                { installModel with ReferenceFileFolders = folders } 
            else
                installModel

        model |> mapFolders(fun folder ->
            if referenceApplies folder then
                { folder with Files = folder.Files.AddFrameworkAssemblyReference reference.AssemblyName }
            else
                folder)

    let addFrameworkAssemblyReferences references (installModel:InstallModel) : InstallModel = 
        references |> Seq.fold (addFrameworkAssemblyReference) (installModel:InstallModel) 

    let filterBlackList  (installModel:InstallModel)  = 

        let includeReferences = function
            | Reference.Library lib -> not (String.endsWithIgnoreCase ".dll" lib || String.endsWithIgnoreCase ".exe" lib )
            | Reference.TargetsFile targetsFile -> 
                (not (String.endsWithIgnoreCase ".props" targetsFile|| String.endsWithIgnoreCase ".targets" targetsFile))
            | _ -> false

        let excludeSatelliteAssemblies = function
            | Reference.Library lib -> lib.EndsWith ".resources.dll"
            | _ -> false

        let blacklisted (blacklist:string list) (file:string) = blacklist |> List.exists (String.endsWithIgnoreCase file )

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
                ReferenceFileFolders = 
                    installModel.ReferenceFileFolders
                    |> List.map applRestriction
                    |> List.filter (fun folder -> folder.Targets <> []) 

                TargetsFileFolders = 
                    installModel.TargetsFileFolders
                    |> List.map applRestriction
                    |> List.filter (fun folder -> folder.Targets <> [])                     }

    let rec addTargetsFiles (targetsFiles:string seq) (this:InstallModel) : InstallModel =
        let targetsFileFolders = 
            targetsFiles 
            |> Seq.choose extractBuildFolder
            |> Seq.distinct 
            |> PlatformMatching.getSupportedTargetProfiles 
            |> Seq.map (fun entry -> { Name = entry.Key; Targets = List.ofSeq entry.Value; Files = InstallFiles.empty })
            |> Seq.toList

        Seq.fold (fun model file ->
            match extractBuildFolder file with
            | Some folderName ->
                match Seq.tryFind (fun folder -> folder.Name = folderName) model.TargetsFileFolders with
                | Some path -> addTargetsFile path file model
                | _ -> model
            | None -> model) { this with TargetsFileFolders = targetsFileFolders } targetsFiles
    

    let filterReferences references (this:InstallModel) =
        let inline mapfn (files:InstallFiles) = 
            { files with 
                References = files.References |> Set.filter (fun reference -> Set.contains reference.ReferenceName references |> not) 
            }
        mapFiles mapfn this

    let createFromLibs packageName packageVersion frameworkRestrictions libs targetsFiles analyzerFiles (nuspec:Nuspec) = 
        emptyModel packageName packageVersion
        |> addLibReferences libs nuspec.References
        |> addTargetsFiles targetsFiles
        |> addAnalyzerFiles analyzerFiles 
        |> addFrameworkAssemblyReferences nuspec.FrameworkAssemblyReferences
        |> filterBlackList
        |> applyFrameworkRestrictions frameworkRestrictions
        |> removeIfCompletelyEmpty


type InstallModel with

    static member EmptyModel (packageName, packageVersion) = InstallModel.emptyModel packageName packageVersion

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

    member this.HasLibReferences () = InstallModel.hasReferences this

    member this.AddReferences libs = InstallModel.addLibReferences libs NuspecReferences.All this

    member this.AddAnalyzerFiles analyzerFiles = InstallModel.addAnalyzerFiles analyzerFiles this

    member this.AddTargetsFile(path, file) = InstallModel.addTargetsFile path file this

    member this.AddTargetsFiles targetsFiles = InstallModel.addTargetsFiles targetsFiles this

    member this.AddPackageFile (path, file, references) = InstallModel.addPackageFile path file references this
    
    member this.AddFrameworkAssemblyReference reference = InstallModel.addFrameworkAssemblyReference this reference 

    member this.AddFrameworkAssemblyReferences references = InstallModel.addFrameworkAssemblyReferences references this
     
    member this.FilterBlackList () = InstallModel.filterBlackList this

    member this.FilterReferences(references) = InstallModel.filterReferences references this

    member this.ApplyFrameworkRestrictions restrictions = InstallModel.applyFrameworkRestrictions restrictions this

    member this.RemoveIfCompletelyEmpty() = InstallModel.removeIfCompletelyEmpty this
    
    static member CreateFromLibs(packageName, packageVersion, frameworkRestrictions:FrameworkRestriction list, libs, targetsFiles, analyzerFiles, nuspec : Nuspec) = 
        InstallModel.createFromLibs packageName packageVersion frameworkRestrictions libs targetsFiles analyzerFiles nuspec 