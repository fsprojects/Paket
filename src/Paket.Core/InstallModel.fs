namespace Paket

open System
open System.IO
open System.Collections.Generic

open Paket.Domain
open Paket.Requirements

[<RequireQualifiedAccess>]
type Reference = 
    { Path : string }

    member this.LibName =
        let fi = new FileInfo(normalizePath this.Path)
        Some(fi.Name.Replace(fi.Extension, ""))

    member this.ReferenceName =
        let fi = new FileInfo(normalizePath this.Path)
        fi.Name.Replace(fi.Extension, "")

type InstallFiles = 
    { References : Reference Set
      ContentFiles : string Set }
    
    static member empty = 
        { References = Set.empty
          ContentFiles = Set.empty }
    
    static member singleton lib = InstallFiles.empty.AddReference lib

    member this.AddReference lib = 
        { this with References = Set.add { Path = lib } this.References }

    member this.MergeWith(that:InstallFiles)= 
        { this with 
            References = Set.union that.References this.References
            ContentFiles = Set.union that.ContentFiles this.ContentFiles }

type LibFolder =
    { Name : string
      Targets : TargetProfile list
      Files : InstallFiles}

type InstallModel = 
    { PackageName : PackageName
      PackageVersion : SemVerInfo
      LibFolders : LibFolder list
      FrameworkAssemblies : FrameworkAssemblyReference list }

    static member EmptyModel(packageName, packageVersion) : InstallModel = 
        { PackageName = packageName
          PackageVersion = packageVersion
          LibFolders = []
          FrameworkAssemblies = [] }
   
    member this.GetTargets() = 
        this.LibFolders
        |> List.map (fun folder -> folder.Targets)
        |> List.concat
    
    member this.GetFiles(target : TargetProfile) = 
        match Seq.tryFind (fun lib -> Seq.exists (fun t -> t = target) lib.Targets) this.LibFolders with
        | Some folder -> folder.Files.References |> Seq.map (fun r -> r.Path)
        | None -> Seq.empty
    
    member this.AddLibFolders(libs : seq<string>) : InstallModel =
        let libFolders = 
            libs 
            |> Seq.map this.ExtractLibFolder
            |> Seq.choose id
            |> Seq.distinct 
            |> List.ofSeq

        if libFolders.Length = 0 then this
        else
            let libFolders =
                PlatformMatching.getSupportedTargetProfiles libFolders
                |> Seq.map (fun entry -> { Name = entry.Key; Targets = entry.Value; Files = InstallFiles.empty })
                |> Seq.toList

            { this with LibFolders = libFolders}
    
    member this.ExtractLibFolder(path : string) : string option=
        let path = path.Replace("\\", "/").ToLower()
        let fi = new FileInfo(path)

        let startPos = path.LastIndexOf("lib/")
        let endPos = path.IndexOf('/', startPos + 4)
        if startPos < 0 then None 
        elif endPos < 0 then Some("")
        else 
            Some(path.Substring(startPos + 4, endPos - startPos - 4))

    member this.MapFolders(mapF) = { this with LibFolders = List.map mapF this.LibFolders }
    
    member this.MapFiles(mapF) = 
        this.MapFolders(fun folder -> { folder with Files = mapF folder.Files })

    member this.AddPackageFiles(path : LibFolder, file : string, references) : InstallModel =
        let install = 
            match references with
            | NuspecReferences.All -> true
            | NuspecReferences.Explicit list -> List.exists file.EndsWith list

        if not install then this else
        
        let folders = List.map (fun p -> 
                               if p.Name = path.Name then { p with Files = p.Files.AddReference file }
                               else p) this.LibFolders
        { this with LibFolders = folders }

    member this.AddFiles(files : seq<string>, references) : InstallModel =
        Seq.fold (fun model file ->
                    match model.ExtractLibFolder file with
                    | Some folderName -> 
                        match Seq.tryFind (fun folder -> folder.Name = folderName) model.LibFolders with
                        | Some path -> model.AddPackageFiles(path, file, references)
                        | _ -> model
                    | None -> model) this files

    member this.AddReferences(libs, references) : InstallModel = 
        this.AddLibFolders(libs)
            .AddFiles(libs, references)
    
    member this.AddReferences(libs) = this.AddReferences(libs, NuspecReferences.All)
    
    member this.AddFrameworkAssemblyReferences(references) : InstallModel = 
        { this with FrameworkAssemblies = references }
    
    member this.FilterBlackList() = 
        let blackList = 
            [ fun (reference : Reference) -> 
                not (reference.Path.EndsWith ".dll" || reference.Path.EndsWith ".exe") ]

        blackList
        |> List.map (fun f -> f >> not) // inverse
        |> List.fold (fun (model:InstallModel) f ->
                model.MapFiles(fun files -> { files with References = Set.filter f files.References }) )
                this
    
    member this.FilterReferences(references) =
        let inline mapF (files:InstallFiles) = {files with References = files.References |> Set.filter (fun reference -> Set.contains reference.ReferenceName references |> not) }
        this.MapFiles(fun files -> mapF files)

    member this.GetReferences = 
        lazy ([ for lib in this.LibFolders do
                    yield! lib.Files.References]                    
              |> Set.ofList)
    
    member this.GetReferenceNames() = 
        this.GetReferences.Force()
        |> Set.map (fun lib -> lib.ReferenceName)

    static member CreateFromLibs(packageName, packageVersion, frameworkRestriction:FrameworkRestriction, libs, nuspec : Nuspec) = 
        InstallModel
            .EmptyModel(packageName, packageVersion)
            .AddReferences(libs, nuspec.References)
            .AddFrameworkAssemblyReferences(nuspec.FrameworkAssemblyReferences)
            .FilterBlackList()
