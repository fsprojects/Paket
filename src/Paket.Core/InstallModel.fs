namespace Paket

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

type FrameworkGroup = 
    { Frameworks : Map<FrameworkIdentifier, InstallFiles>
      Fallbacks : InstallFiles }
    
    member this.GetFiles(framework : FrameworkIdentifier) = 
        match this.Frameworks.TryFind framework with
        | Some x -> 
            x.References
            |> Seq.map (fun x -> 
                   match x with
                   | Reference.Library lib -> Some lib
                   | _ -> None)
            |> Seq.choose id
        | None -> Seq.empty
    
    member this.GetReferences(framework : FrameworkIdentifier) = 
        match this.Frameworks.TryFind framework with
        | Some x -> x.References
        | None -> Set.empty

    static member singleton(framework,libs) =
        { Frameworks = Map.add framework libs Map.empty; Fallbacks = InstallFiles.empty }

    member this.ReplaceFramework(framework,emptyReferencesF,mapReferencesF) =
        { this with Frameworks = 
                        match Map.tryFind framework this.Frameworks with
                        | Some files when Set.isEmpty files.References |> not -> Map.add framework (mapReferencesF files) this.Frameworks
                        | _ -> Map.add framework (emptyReferencesF()) this.Frameworks }

    member this.MergeWith(that:FrameworkGroup) =
        let mergedFrameworks =
            that.Frameworks
            |> Map.fold (fun group frameworkName framework ->            
                            match Map.tryFind frameworkName this.Frameworks with
                            | Some files -> { group with Frameworks = Map.add frameworkName (files.MergeWith framework) group.Frameworks }
                            | None -> { group with Frameworks = Map.add frameworkName framework group.Frameworks }) this
        { mergedFrameworks with
            Fallbacks = this.Fallbacks.MergeWith(that.Fallbacks) }

type InstallModel = 
    { PackageName : PackageName
      PackageVersion : SemVerInfo
      Groups : Map<string, FrameworkGroup>
      DefaultFallback : InstallFiles }

    static member EmptyModel(packageName, packageVersion) : InstallModel = 
        let frameworks = FrameworkVersion.KnownDotNetFrameworks |> List.map (fun x -> DotNetFramework(x))
        
        let group : FrameworkGroup = 
            { Frameworks = List.fold (fun map f -> Map.add f InstallFiles.empty map) Map.empty frameworks
              Fallbacks = InstallFiles.empty }
        { PackageName = packageName
          PackageVersion = packageVersion
          DefaultFallback = InstallFiles.empty
          Groups = Map.add FrameworkIdentifier.DefaultGroup group Map.empty }
   
    member this.GetFrameworks() = 
        this.Groups
        |> Seq.map (fun kv -> kv.Value.Frameworks)
        |> Seq.concat
    
    member this.GetFiles(framework : FrameworkIdentifier) = 
        match this.Groups.TryFind framework.Group with
        | Some group -> group.GetFiles framework
        | None -> Seq.empty
    
    member this.GetReferences(framework : FrameworkIdentifier) = 
        match this.Groups.TryFind framework.Group with
        | Some group -> group.GetReferences framework
        | None -> Set.empty
    
    member this.AddOrReplaceGroup(groupId,mapGroupF,newGroupF) =
        match this.Groups.TryFind groupId with
        | Some group -> { this with Groups = Map.add groupId (mapGroupF group) this.Groups } 
        | None -> 
            match newGroupF() with
            | Some newGroup -> { this with Groups = Map.add groupId newGroup this.Groups }
            | None -> this

    member this.MapGroups(mapF) = { this with Groups = Map.map mapF this.Groups }

    member this.MapGroupFrameworks(mapF) = 
        this.MapGroups(fun _ group -> { group with Frameworks = Map.map mapF group.Frameworks })

    member this.AddReference(framework : FrameworkIdentifier, lib : string, references) : InstallModel = 
        let install = 
            match references with
            | NuspecReferences.All -> true
            | NuspecReferences.Explicit list -> List.exists lib.EndsWith list

        if not install then this else

        this.AddOrReplaceGroup(
            framework.Group,
            (fun group -> 
                group.ReplaceFramework(
                    framework,
                    (fun _ -> InstallFiles.singleton lib),
                    (fun files -> files.AddReference lib))),
            (fun _ -> Some(FrameworkGroup.singleton(framework,InstallFiles.singleton lib))))

    member this.AddReferences(libs, references) : InstallModel = 
        Seq.fold (fun model lib -> 
                    match FrameworkIdentifier.DetectFromPath lib with
                    | Some framework -> model.AddReference(framework, lib, references)
                    | _ -> model) this libs
    
    member this.AddReferences(libs) = this.AddReferences(libs, NuspecReferences.All)
    
    member this.AddFrameworkAssemblyReference(reference) : InstallModel = 
        match reference.TargetFramework with
        | None -> 
            this.MapGroupFrameworks(fun fw files -> files.AddFrameworkAssemblyReference(reference.AssemblyName))
        | Some fw ->
            this.AddOrReplaceGroup(
                fw.Group,
                (fun group ->
                    group.ReplaceFramework(
                        fw,
                        (fun _ -> InstallFiles.empty.AddFrameworkAssemblyReference reference.AssemblyName),
                        (fun files -> files.AddFrameworkAssemblyReference reference.AssemblyName))),
                (fun _ -> None))
    
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
                model.MapGroupFrameworks(fun _ files -> { files with References = Set.filter f files.References }) )
                this
    
    member this.UseLowerVersionLibIfEmpty() =
        this.AddOrReplaceGroup(
            FrameworkIdentifier.DefaultGroup,
            (fun group -> 
                FrameworkVersion.KnownDotNetFrameworks
                |> List.rev
                |> List.fold (fun (group : FrameworkGroup) lowerVersion -> 
                    let newFiles = group.GetReferences(DotNetFramework(lowerVersion))
                    if Set.isEmpty newFiles then group  else 
                    FrameworkVersion.KnownDotNetFrameworks
                    |> List.filter (fun version -> version > lowerVersion)
                    |> List.fold (fun (group : FrameworkGroup) upperVersion -> 
                            let framework = DotNetFramework(upperVersion)
                            group.ReplaceFramework(
                                framework,
                                (fun _ -> { References = newFiles; ContentFiles = Set.empty }),
                                (fun files -> files))) 
                            group) group),
            (fun _ -> None))
    
    member this.UseLowerVersionLibForSpecicalFrameworksIfEmpty() = 
        let newFiles = this.GetReferences(DotNetFramework(FrameworkVersion.V4_5))
        if Set.isEmpty newFiles then this else 
        FrameworkIdentifier.KnownSpecialTargets 
        |> List.fold (fun (model : InstallModel) framework -> 
                model.AddOrReplaceGroup(
                    framework.Group,
                    (fun group ->
                        group.ReplaceFramework(
                            framework,
                            (fun _ -> { References = newFiles; ContentFiles = Set.empty }),
                            (fun files -> files))),
                    (fun _ -> Some(FrameworkGroup.singleton(framework,{ References = newFiles; ContentFiles = Set.empty }))))) this
    

    member this.MergeWith(that:InstallModel) =
        let mergedGroups =
            that.Groups
            |> Map.fold (fun (model : InstallModel) groupName group -> 
                            match this.Groups.TryFind groupName with
                            | Some g -> { model with Groups = Map.add groupName (group.MergeWith(g)) model.Groups }
                            | None -> { model with Groups = Map.add groupName group model.Groups }) this
        { mergedGroups with
            DefaultFallback = this.DefaultFallback.MergeWith(that.DefaultFallback) }

    member this.UseLastInGroupAsFallback() = 
        this.MapGroups(fun _ group -> { group with Fallbacks = (group.Frameworks |> Seq.last).Value })

    member this.UseLastGroupFallBackAsDefaultFallBack() =
        { this with DefaultFallback = this.Groups.[FrameworkIdentifier.DefaultGroup].Fallbacks }
    
    member this.FilterFallbacks() =
        this.MapGroups(fun _ group -> 
                   let fallbacks = group.Fallbacks
                   { group with Frameworks = 
                                    group.Frameworks |> Seq.fold (fun frameworks kv -> 
                                                            let files = kv.Value
                                                            if files.References <> fallbacks.References then frameworks
                                                            else Map.remove kv.Key frameworks) group.Frameworks })
    
    member this.DeleteEmptyGroupIfDefaultFallback() =
        this.MapGroups(fun _ group ->
                   let fallbacks = group.Fallbacks
                   group.Frameworks 
                   |> Seq.fold (fun (group : FrameworkGroup) framework -> 
                          if framework.Value.References <> fallbacks.References then group
                          else { group with Frameworks = Map.remove framework.Key group.Frameworks }) group)

    member this.ApplyFrameworkRestriction(restriction:FrameworkRestriction) =
        match restriction with
        | None -> this
        | Some fw ->
            {this with DefaultFallback = InstallFiles.empty }
              .MapGroups(fun _ group ->
                   group.Frameworks 
                   |> Seq.fold (fun (group : FrameworkGroup) framework -> 
                          if framework.Key = fw then group
                          else { group with Frameworks = Map.remove framework.Key group.Frameworks }) { group with Fallbacks = InstallFiles.empty })
    
    member this.UsePortableVersionLibIfEmpty() = 
        this.GetFrameworks() 
        |> Seq.fold 
               (fun (model : InstallModel) kv -> 
               let newFiles = kv.Value.References
               if Set.isEmpty newFiles then model
               else 
                   let otherProfiles = 
                       match kv.Key with
                       | PortableFramework(_, f) -> 
                           f.Split([| '+' |], System.StringSplitOptions.RemoveEmptyEntries)
                           |> Array.map FrameworkIdentifier.Extract
                           |> Array.choose id
                       | _ -> [||]

                   if Array.isEmpty otherProfiles then model else 
                   otherProfiles 
                   |> Array.fold (fun (model : InstallModel) framework -> 
                        model.AddOrReplaceGroup(
                            framework.Group,
                            (fun group ->
                                group.ReplaceFramework(
                                    framework,
                                    (fun _ -> { References = newFiles; ContentFiles = Set.empty }),
                                    (fun files -> files))),
                            (fun _ -> Some(FrameworkGroup.singleton(framework,{ References = newFiles; ContentFiles = Set.empty }))))) model) this
    
    member this.BuildUnfilteredModel(references) = 
        this
            .UseLowerVersionLibIfEmpty()
            .UsePortableVersionLibIfEmpty()
            .UseLowerVersionLibIfEmpty() // because we now might need to use portable
            .UseLowerVersionLibForSpecicalFrameworksIfEmpty()
            .FilterBlackList()
            .AddFrameworkAssemblyReferences(references)
            .UseLastInGroupAsFallback()
            .UseLastGroupFallBackAsDefaultFallBack()    

    member this.GetReferenceNames = 
        lazy ([ for g in this.Groups do
                    for f in g.Value.Frameworks do
                        yield! f.Value.References
                    yield! g.Value.Fallbacks.References 
                yield! this.DefaultFallback.References]
              |> Set.ofList
              |> Set.map (fun lib -> lib.ReferenceName))

    member this.GetFrameworkAssemblies = 
        lazy ([ for g in this.Groups do
                    for f in g.Value.Frameworks do
                        yield! f.Value.GetFrameworkAssemblies()
                    yield! g.Value.Fallbacks.GetFrameworkAssemblies() 
                yield! this.DefaultFallback.GetFrameworkAssemblies()]
              |> Set.ofList)
    
    static member CreateFromLibs(packageName, packageVersion, frameworkRestriction:FrameworkRestriction, libs, nuspec : Nuspec) = 
        InstallModel
            .EmptyModel(packageName, packageVersion)
            .AddReferences(libs, nuspec.References)            
            .BuildUnfilteredModel(nuspec.FrameworkAssemblyReferences)
            .ApplyFrameworkRestriction(frameworkRestriction)