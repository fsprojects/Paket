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

type InstallModel = 
    { PackageName : PackageName
      PackageVersion : SemVerInfo
      ReferenceFileFolders : LibFolder list
      TargetsFileFolders : LibFolder list }

    static member EmptyModel(packageName, packageVersion) : InstallModel = 
        { PackageName = packageName
          PackageVersion = packageVersion
          ReferenceFileFolders = []
          TargetsFileFolders = [] }
    
    member this.GetLibReferences(target : TargetProfile) = 
        match Seq.tryFind (fun lib -> Seq.exists (fun t -> t = target) lib.Targets) this.ReferenceFileFolders with
        | Some folder -> folder.Files.References
                         |> Set.map (fun x -> 
                                match x with
                                | Reference.Library lib -> Some lib
                                | _ -> None)
                         |> Seq.choose id
        | None -> Seq.empty

    member this.GetTargetsFiles(target : TargetProfile) = 
        match Seq.tryFind (fun lib -> Seq.exists (fun t -> t = target) lib.Targets) this.TargetsFileFolders with
        | Some folder -> folder.Files.References
                         |> Set.map (fun x -> 
                                match x with
                                | Reference.TargetsFile targetsFile -> Some targetsFile
                                | _ -> None)
                         |> Seq.choose id
        | None -> Seq.empty
    
    member this.AddLibReferences(libs : seq<string>, references) : InstallModel =
        let libFolders = 
            libs 
            |> Seq.map this.ExtractLibFolder
            |> Seq.choose id
            |> Seq.distinct 
            |> List.ofSeq
            |> PlatformMatching.getSupportedTargetProfiles 
            |> Seq.map (fun entry -> { Name = entry.Key; Targets = entry.Value; Files = InstallFiles.empty })
            |> Seq.toList  

        Seq.fold (fun (model:InstallModel) file ->
                    match model.ExtractLibFolder file with
                    | Some folderName -> 
                        match Seq.tryFind (fun folder -> folder.Name = folderName) model.ReferenceFileFolders with
                        | Some path -> model.AddPackageFile(path, file, references)
                        | _ -> model
                    | None -> model) { this with ReferenceFileFolders = libFolders } libs

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

    member this.AddPackageFile(path : LibFolder, file : string, references) : InstallModel =
        let install = 
            match references with
            | NuspecReferences.All -> true
            | NuspecReferences.Explicit list -> List.exists file.EndsWith list

        if not install then this else
        
        let folders = 
            this.ReferenceFileFolders
            |> List.map (fun p -> 
                               if p.Name = path.Name then { p with Files = p.Files.AddReference file }
                               else p)

        { this with ReferenceFileFolders = folders }

    member this.AddTargetsFile(path : LibFolder, file : string) : InstallModel =        
        let folders = 
            this.TargetsFileFolders
            |> List.map (fun p -> 
                               if p.Name = path.Name then { p with Files = p.Files.AddTargetsFile file }
                               else p) 
        { this with TargetsFileFolders = folders }
    
    member this.AddReferences(libs) = this.AddLibReferences(libs, NuspecReferences.All)
    
    member this.AddFrameworkAssemblyReference(reference:FrameworkAssemblyReference) : InstallModel =
        let referenceApplies (folder : LibFolder) =
            match reference.FrameworkRestrictions with
            | [] -> true
            | restrictions ->
                restrictions
                |> List.exists (fun restriction ->
                      match restriction with
                      | FrameworkRestriction.Portable p ->
                            folder.Targets 
                            |> List.exists (fun target ->
                                match target with
                                | SinglePlatform t -> false
                                | _ -> true)
                      | FrameworkRestriction.Exactly target ->
                            folder.GetSinglePlatforms() 
                            |> List.exists (fun t -> t = target)
                        | FrameworkRestriction.AtLeast target ->
                            folder.GetSinglePlatforms() 
                            |> List.exists (fun t -> t >= target)
                        | FrameworkRestriction.Between(min,max) ->
                            folder.GetSinglePlatforms() 
                            |> List.exists (fun t -> t >= min && t < max)                            )
            
        this.MapFolders(fun folder ->
            if referenceApplies folder then
                { folder with Files = folder.Files.AddFrameworkAssemblyReference reference.AssemblyName }
            else
                folder)
    
    member this.AddFrameworkAssemblyReferences(references) : InstallModel = 
        references 
        |> Seq.fold (fun model reference -> model.AddFrameworkAssemblyReference reference) this
    
    member this.FilterBlackList() = 
        let includeReferences = function
            | Reference.Library lib -> not (lib.ToLower().EndsWith ".dll" || lib.ToLower().EndsWith ".exe")
            | Reference.TargetsFile targetsFile -> 
                (not (targetsFile.ToLower().EndsWith ".props" || targetsFile.ToLower().EndsWith ".targets")) ||
                targetsFile.ToLower().EndsWith("microsoft.bcl.build.targets") // would install a targets file causing the build to fail in VS
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
        this.MapFiles(fun files -> mapF files)

    member this.ApplyFrameworkRestrictions(restrictions:FrameworkRestrictions) =
        match restrictions with
        | [] -> this
        | restrictions ->
            let applRestriction folder =
                { folder with 
                    Targets = 
                        folder.Targets
                        |> List.filter 
                            (function 
                             | SinglePlatform pf -> 
                                restrictions
                                |> List.exists (fun restriction ->
                                        match restriction with
                                        | FrameworkRestriction.Exactly fw -> pf = fw
                                        | FrameworkRestriction.Portable r -> false
                                        | FrameworkRestriction.AtLeast fw -> pf >= fw                
                                        | FrameworkRestriction.Between(min,max) -> pf >= min && pf < max)
                             | _ -> 
                                restrictions
                                |> List.exists (fun restriction ->
                                        match restriction with
                                        | FrameworkRestriction.Portable r -> true
                                        | _ -> false))}

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
        if Set.isEmpty (this.GetFrameworkAssembliesLazy.Force()) && Set.isEmpty (this.GetLibReferencesLazy.Force()) && Set.isEmpty (this.GetTargetsFilesLazy.Force()) then
            InstallModel.EmptyModel(this.PackageName,this.PackageVersion)
        else
            this
    
    static member CreateFromLibs(packageName, packageVersion, frameworkRestrictions:FrameworkRestrictions, libs, targetsFiles, nuspec : Nuspec) = 
        InstallModel
            .EmptyModel(packageName, packageVersion)
            .AddLibReferences(libs, nuspec.References)
            .AddTargetsFiles(targetsFiles)
            .AddFrameworkAssemblyReferences(nuspec.FrameworkAssemblyReferences)
            .FilterBlackList()
            .ApplyFrameworkRestrictions(frameworkRestrictions)
            .RemoveIfCompletelyEmpty()       