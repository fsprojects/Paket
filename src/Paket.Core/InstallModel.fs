namespace Paket

open System
open System.IO
open System.Collections.Generic

open Paket.Domain
open Paket.Requirements

[<RequireQualifiedAccess>]
type Reference = 
    | Library of string
    | FrameworkAssemblyReference of string

    member this.LibName =
        match this with
        | Reference.Library lib -> 
            let fi = new FileInfo(normalizePath lib)
            Some(fi.Name.Replace(fi.Extension, ""))
        | _ -> None

    member this.FrameworkReferenceName =
        match this with
        | FrameworkAssemblyReference name -> Some name
        | _ -> None

    member this.ReferenceName =
        match this with
        | FrameworkAssemblyReference name -> name
        | Reference.Library lib -> 
            let fi = new FileInfo(normalizePath lib)
            fi.Name.Replace(fi.Extension, "")


type InstallFiles = 
    { References : Reference Set
      ContentFiles : string Set }
    
    static member empty = 
        { References = Set.empty
          ContentFiles = Set.empty }
    
    static member singleton lib = InstallFiles.empty.AddReference lib

    member this.AddReference lib = 
        { this with References = Set.add (Reference.Library lib) this.References }

    member this.AddFrameworkAssemblyReference assemblyName = 
        { this with References = Set.add (Reference.FrameworkAssemblyReference assemblyName) this.References }

    member this.GetFrameworkAssemblies() =
        this.References
        |> Set.map (fun r -> r.FrameworkReferenceName)
        |> Seq.choose id

    member this.MergeWith(that:InstallFiles)= 
        { this with 
            References = Set.union that.References this.References
            ContentFiles = Set.union that.ContentFiles this.ContentFiles }

type LibFolder =
    { Name : string
      Targets : seq<TargetProfile>
      Files : InstallFiles}

type InstallModel = 
    { PackageName : PackageName
      PackageVersion : SemVerInfo
      LibFolders : seq<LibFolder> }

    static member EmptyModel(packageName, packageVersion) : InstallModel = 
        { PackageName = packageName
          PackageVersion = packageVersion
          LibFolders = Seq.empty }
   
    member this.GetTargets() = 
        this.LibFolders
        |> Seq.map (fun folder -> folder.Targets)
        |> Seq.concat
    
    member this.GetFiles(target : TargetProfile) = 
        match Seq.tryFind (fun lib -> Seq.exists (fun t -> t = target) lib.Targets) this.LibFolders with
        | Some folder -> folder.Files.References
                         |> Seq.map (fun x -> 
                                match x with
                                | Reference.Library lib -> Some lib
                                | _ -> None)
                         |> Seq.choose id
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

    member this.MapFolders(mapF) = { this with LibFolders = Seq.map mapF this.LibFolders }
    
    member this.MapFiles(mapF) = 
        this.MapFolders(fun folder -> { folder with Files = mapF folder.Files })

    member this.AddPackageFiles(path : LibFolder, file : string, references) : InstallModel =
        let install = 
            match references with
            | NuspecReferences.All -> true
            | NuspecReferences.Explicit list -> List.exists file.EndsWith list

        if not install then this else
        
        let folders = Seq.map (fun p -> 
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
    
    member this.AddFrameworkAssemblyReference(reference) : InstallModel =
        this.MapFiles(fun files -> files.AddFrameworkAssemblyReference(reference.AssemblyName))
    
    member this.AddFrameworkAssemblyReferences(references) : InstallModel = 
        references 
        |> Seq.fold (fun model reference -> model.AddFrameworkAssemblyReference reference) this
    
    member this.FilterBlackList() = 
        let blackList = 
            [ fun (reference : Reference) -> 
                match reference with
                | Reference.Library lib -> not (lib.EndsWith ".dll" || lib.EndsWith ".exe")
                | _ -> false ]

        blackList
        |> List.map (fun f -> f >> not) // inverse
        |> List.fold (fun (model:InstallModel) f ->
                model.MapFiles(fun files -> { files with References = Set.filter f files.References }) )
                this
    
    member this.FilterReferences(references) =
        let inline mapF (files:InstallFiles) = {files with References = files.References |> Set.filter (fun reference -> Set.contains reference.ReferenceName references |> not) }
        this.MapFiles(fun files -> mapF files)
    
    member this.GetReferenceNames = 
        lazy ([ for lib in this.LibFolders do
                    yield! lib.Files.References]
              |> Set.ofList
              |> Set.map (fun lib -> lib.ReferenceName))

    member this.GetFrameworkAssemblies = 
        lazy ([ for lib in this.LibFolders do
                    yield! lib.Files.GetFrameworkAssemblies()]
              |> Set.ofList)
    
    static member CreateFromLibs(packageName, packageVersion, frameworkRestriction:FrameworkRestriction, libs, nuspec : Nuspec) = 
        InstallModel
            .EmptyModel(packageName, packageVersion)
            .AddReferences(libs, nuspec.References)
            .AddFrameworkAssemblyReferences(nuspec.FrameworkAssemblyReferences)
            .FilterBlackList()