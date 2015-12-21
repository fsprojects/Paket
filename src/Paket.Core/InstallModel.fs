namespace Paket

open System
open System.IO
open System.Collections.Generic

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
            let fi = new FileInfo(normalizePath lib)
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
            let fi = new FileInfo(normalizePath targetsFile)
            fi.Name.Replace(fi.Extension, "")
        | Reference.Library lib -> 
            let fi = new FileInfo(normalizePath lib)
            fi.Name.Replace(fi.Extension, "")

    member this.Path =
        match this with
        | Reference.Library path -> path
        | Reference.TargetsFile path -> path
        | Reference.FrameworkAssemblyReference path -> path

type InstallFiles = 
    { References : Reference Set
      ContentFiles : string Set }
    
    static member empty = 
        { References = Set.empty
          ContentFiles = Set.empty }
    
    static member singleton lib = InstallFiles.empty.AddReference lib

    member this.AddReference lib = 
        { this with References = Set.add (Reference.Library lib) this.References }

    member this.AddTargetsFile targetsFile = 
        { this with References = Set.add (Reference.TargetsFile targetsFile) this.References }

    member this.AddFrameworkAssemblyReference assemblyName = 
        { this with References = Set.add (Reference.FrameworkAssemblyReference assemblyName) this.References }

    member this.GetFrameworkAssemblies() =
        this.References
        |> Set.map (fun r -> r.FrameworkReferenceName)
        |> Seq.choose id

    member this.MergeWith(that:InstallFiles) = 
        { this with 
            References = Set.union that.References this.References
            ContentFiles = Set.union that.ContentFiles this.ContentFiles }

type LibFolder =
    { Name : string
      Targets : TargetProfile list
      Files : InstallFiles}

    member this.GetSinglePlatforms() =
        this.Targets 
        |> List.choose (fun target ->
            match target with
            | SinglePlatform t -> Some t
            | _ -> None)


type AnalyzerLanguage = Any | CSharp | FSharp | VisualBasic
    with
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

    static member EmptyModel(packageName, packageVersion) : InstallModel = 
        { PackageName = packageName
          PackageVersion = packageVersion
          ReferenceFileFolders = []
          TargetsFileFolders = [] 
          Analyzers = [] }
    
    member this.GetLibReferences(target : TargetProfile) = 
        match Seq.tryFind (fun lib -> Seq.exists (fun t -> t = target) lib.Targets) this.ReferenceFileFolders with
        | Some folder -> folder.Files.References
                         |> Set.map (fun x -> 
                                match x with
                                | Reference.Library lib -> Some lib
                                | _ -> None)
                         |> Seq.choose id
        | None -> Seq.empty

    member this.GetLibReferences(frameworkIdentifier) = this.GetLibReferences(SinglePlatform(frameworkIdentifier))

    member this.GetTargetsFiles(target : TargetProfile) = 
        match Seq.tryFind (fun lib -> Seq.exists (fun t -> t = target) lib.Targets) this.TargetsFileFolders with
        | Some folder -> folder.Files.References
                         |> Set.map (fun x -> 
                                match x with
                                | Reference.TargetsFile targetsFile -> Some targetsFile
                                | _ -> None)
                         |> Seq.choose id
        | None -> Seq.empty

    member this.CalcLibFolders libs =
        libs 
        |> Seq.map this.ExtractLibFolder
        |> Seq.choose id
        |> Seq.distinct 
        |> List.ofSeq
        |> PlatformMatching.getSupportedTargetProfiles 
        |> Seq.map (fun entry -> { Name = entry.Key; Targets = entry.Value; Files = InstallFiles.empty })
        |> Seq.toList

    member this.AddLibReferences(libs : seq<string>, references) : InstallModel =
        let libFolders = this.CalcLibFolders libs

        Seq.fold (fun (model:InstallModel) file ->
                    match model.ExtractLibFolder file with
                    | Some folderName -> 
                        match Seq.tryFind (fun folder -> folder.Name = folderName) model.ReferenceFileFolders with
                        | Some path -> model.AddPackageFile(path, file, references)
                        | _ -> model
                    | None -> model) { this with ReferenceFileFolders = libFolders } libs

    member this.AddAnalyzerFiles(analyzerFiles : seq<string>) : InstallModel =
        let analyzerLibs =
            analyzerFiles
            |> Seq.map (fun file -> FileInfo file)
            |> Seq.map AnalyzerLib.FromFile
            |> List.ofSeq

        { this with Analyzers = this.Analyzers @ analyzerLibs}

    member this.AddTargetsFiles(targetsFiles : seq<string>) : InstallModel =
        let targetsFileFolders = 
            targetsFiles 
            |> Seq.map this.ExtractBuildFolder
            |> Seq.choose id
            |> Seq.distinct 
            |> List.ofSeq
            |> PlatformMatching.getSupportedTargetProfiles 
            |> Seq.map (fun entry -> { Name = entry.Key; Targets = entry.Value; Files = InstallFiles.empty })
            |> Seq.toList


        Seq.fold (fun model file ->
                    match model.ExtractBuildFolder file with
                    | Some folderName -> 
                        match Seq.tryFind (fun folder -> folder.Name = folderName) model.TargetsFileFolders with
                        | Some path -> model.AddTargetsFile(path, file)
                        | _ -> model
                    | None -> model) { this with TargetsFileFolders = targetsFileFolders } targetsFiles
    
    member this.ExtractLibFolder path = Utils.extractPath "lib" path

    member this.ExtractBuildFolder path = Utils.extractPath "build" path

    member this.MapFolders(mapF) = { this with ReferenceFileFolders = List.map mapF this.ReferenceFileFolders; TargetsFileFolders = List.map mapF this.TargetsFileFolders  }
    
    member this.MapFiles(mapF) = 
        this.MapFolders(fun folder -> { folder with Files = mapF folder.Files })

    member this.AddFileToFolder(path : LibFolder, file : string, folders : LibFolder list, add: InstallFiles -> string -> InstallFiles) =
            folders
            |> List.map (fun p -> 
                                if p.Name = path.Name then { p with Files = add p.Files file }
                                else p) 

    member this.AddPackageFile(path : LibFolder, file : string, references) : InstallModel =
        let install = 
            match references with
            | NuspecReferences.All -> true
            | NuspecReferences.Explicit list -> List.exists file.EndsWith list

        if not install then this else
        
        { this with ReferenceFileFolders = this.AddFileToFolder(path, file, this.ReferenceFileFolders, (fun f -> f.AddReference)) }

    member this.AddTargetsFile(path : LibFolder, file : string) : InstallModel =
        { this with TargetsFileFolders = this.AddFileToFolder(path, file, this.TargetsFileFolders, (fun f -> f.AddTargetsFile)) }
    
    member this.AddReferences(libs) = this.AddLibReferences(libs, NuspecReferences.All)
    
    member this.AddFrameworkAssemblyReference(reference:FrameworkAssemblyReference) : InstallModel =
        let referenceApplies (folder : LibFolder) =
            match reference.FrameworkRestrictions with
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
                            |> List.exists (fun t -> t = target)
                        | FrameworkRestriction.AtLeast target ->
                            folder.GetSinglePlatforms() 
                            |> List.exists (fun t -> t >= target && t.IsSameCategoryAs(target))
                        | FrameworkRestriction.Between(min,max) ->
                            folder.GetSinglePlatforms() 
                            |> List.exists (fun t -> t >= min && t < max && t.IsSameCategoryAs(min)))
        
        let model = 
            if List.isEmpty this.ReferenceFileFolders then
                let folders = this.CalcLibFolders ["lib/Default.dll"]
                { this with ReferenceFileFolders = folders } 
            else
                this

        model.MapFolders(fun folder ->
            if referenceApplies folder then
                { folder with Files = folder.Files.AddFrameworkAssemblyReference reference.AssemblyName }
            else
                folder)
    
    member this.AddFrameworkAssemblyReferences(references) : InstallModel = 
        references 
        |> Seq.fold (fun model -> model.AddFrameworkAssemblyReference) this
    
    member this.FilterBlackList() = 
        let includeReferences = function
            | Reference.Library lib -> not (lib.ToLower().EndsWith ".dll" || lib.ToLower().EndsWith ".exe")
            | Reference.TargetsFile targetsFile -> 
                (not (targetsFile.ToLower().EndsWith ".props" || targetsFile.ToLower().EndsWith ".targets"))
            | _ -> false

        let excludeSatelliteAssemblies = function
            | Reference.Library lib -> lib.EndsWith ".resources.dll"
            | _ -> false

        let blacklisted (blacklist : string list) (file : string) = blacklist |> List.exists (fun blf -> file.ToLower().EndsWith (blf.ToLower()))

        let blackList = 
            [ includeReferences
              excludeSatelliteAssemblies]

        blackList
        |> List.map (fun f -> f >> not) // inverse
        |> List.fold (fun (model:InstallModel) f ->
                model.MapFiles(fun files -> { files with References = Set.filter f files.References }) )
                this
    
    member this.FilterReferences(references) =
        let inline mapF (files:InstallFiles) = {files with References = files.References |> Set.filter (fun reference -> Set.contains reference.ReferenceName references |> not) }
        this.MapFiles mapF

    member this.ApplyFrameworkRestrictions(restrictions:FrameworkRestrictions) =
        match restrictions with
        | [] -> this
        | restrictions ->
            let applRestriction folder =
                { folder with Targets = applyRestrictionsToTargets restrictions folder.Targets}

            {this with 
                ReferenceFileFolders = 
                    this.ReferenceFileFolders
                    |> List.map applRestriction
                    |> List.filter (fun folder -> folder.Targets <> []) 

                TargetsFileFolders = 
                    this.TargetsFileFolders
                    |> List.map applRestriction
                    |> List.filter (fun folder -> folder.Targets <> [])                     }

    member this.GetFrameworkAssembliesLazy = 
        lazy ([ for lib in this.ReferenceFileFolders do
                    yield! lib.Files.GetFrameworkAssemblies()]
              |> Set.ofList)

    member this.GetLibReferencesLazy = 
        lazy ([ for lib in this.ReferenceFileFolders do
                    yield! lib.Files.References] 
              |> Set.ofList)

    member this.GetTargetsFilesLazy = 
        lazy ([ for lib in this.TargetsFileFolders do
                    yield! lib.Files.References] 
              |> Set.ofList)

    member this.RemoveIfCompletelyEmpty() = 
        if Set.isEmpty (this.GetFrameworkAssembliesLazy.Force()) && Set.isEmpty (this.GetLibReferencesLazy.Force()) && Set.isEmpty (this.GetTargetsFilesLazy.Force()) && List.isEmpty this.Analyzers then
            InstallModel.EmptyModel(this.PackageName,this.PackageVersion)
        else
            this
    
    static member CreateFromLibs(packageName, packageVersion, frameworkRestrictions:FrameworkRestrictions, libs, targetsFiles, analyzerFiles, nuspec : Nuspec) = 
        InstallModel
            .EmptyModel(packageName, packageVersion)
            .AddLibReferences(libs, nuspec.References)
            .AddTargetsFiles(targetsFiles)
            .AddAnalyzerFiles(analyzerFiles)
            .AddFrameworkAssemblyReferences(nuspec.FrameworkAssemblyReferences)
            .FilterBlackList()
            .ApplyFrameworkRestrictions(frameworkRestrictions)
            .RemoveIfCompletelyEmpty()