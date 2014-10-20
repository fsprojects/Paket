namespace Paket

open System.IO

[<RequireQualifiedAccess>]
type Reference =
| Library of string
| FrameworkAssemblyReference of string

type InstallFiles = 
    { References : Reference Set
      ContentFiles : string Set }
    
    static member empty = 
        { References = Set.empty
          ContentFiles = Set.empty }
    
    member this.AddReference lib = { this with References = Set.add (Reference.Library lib) this.References }
    member this.AddFrameworkAssemblyReference assemblyName = { this with References = Set.add (Reference.FrameworkAssemblyReference assemblyName) this.References }

type FrameworkGroup = 
    { Frameworks : Map<FrameworkIdentifier, InstallFiles>
      Fallbacks : InstallFiles }

    member this.GetFiles(framework:FrameworkIdentifier) = 
        match this.Frameworks.TryFind framework with
        | Some x -> 
            x.References
            |> Seq.map (fun x ->
                match x with
                | Reference.Library lib -> Some lib
                | _ -> None)
            |> Seq.choose id
        | None -> Seq.empty

    member this.GetReferences(framework:FrameworkIdentifier) = 
        match this.Frameworks.TryFind framework with
        | Some x -> x.References
        | None -> Set.empty
        
type InstallModel = 
    { PackageName : string
      PackageVersion : SemVerInfo
      Groups: Map<string,FrameworkGroup>
      DefaultFallback : InstallFiles }

    static member KnownSpecialTargets =
        [yield Silverlight "v3.0"
         yield Silverlight "v4.0"
         yield Silverlight "v5.0"
         yield Silverlight "v5.0"
         yield WindowsPhoneApp "7.1"
         yield Windows "v8.0"
         yield WindowsPhoneApp "v8.0"
         yield WindowsPhoneApp "v8.1"
         yield MonoAndroid
         yield MonoTouch ]

    static member EmptyModel(packageName, packageVersion) : InstallModel = 
        let frameworks = FrameworkVersion.KnownDotNetFrameworks |> List.map (fun x  -> DotNetFramework(x))
        let group : FrameworkGroup =
            { Frameworks = List.fold (fun map f -> Map.add f InstallFiles.empty map) Map.empty frameworks
              Fallbacks = InstallFiles.empty }

        { PackageName = packageName
          PackageVersion = packageVersion
          DefaultFallback = InstallFiles.empty
          Groups = Map.add FrameworkIdentifier.DefaultGroup group Map.empty}
    
    member this.GetFrameworks() = this.Groups |> Seq.map (fun kv -> kv.Value.Frameworks) |> Seq.concat
    
    member this.GetFiles(framework:FrameworkIdentifier) = 
        let g = framework.Group
        match this.Groups.TryFind g with
        | Some group -> group.GetFiles framework
        | None -> Seq.empty

    member this.GetReferences(framework:FrameworkIdentifier) = 
        let g = framework.Group
        match this.Groups.TryFind g with
        | Some group -> group.GetReferences framework
        | None -> Set.empty

    member this.AddReference(framework:FrameworkIdentifier,lib:string,references) : InstallModel = 
        let install =
            match references with
            | NuspecReferences.All -> true
            | NuspecReferences.Explicit list -> list |> List.exists lib.EndsWith

        if not install then this else
        let g = framework.Group
        { this
            with 
                Groups = 
                    match this.Groups.TryFind g with
                    | Some group ->
                        Map.add 
                          g
                          ({ group with Frameworks = 
                                            match Map.tryFind framework group.Frameworks with
                                            | Some files -> Map.add framework (files.AddReference lib) group.Frameworks
                                            | None -> Map.add framework (InstallFiles.empty.AddReference lib) group.Frameworks })
                          this.Groups                    
                    | None -> this.Groups }

        

    member this.AddReferences(libs,references) : InstallModel =         
        libs |> Seq.fold (fun model lib -> 
                    match FrameworkIdentifier.DetectFromPath lib with
                    | Some framework -> model.AddReference(framework,lib,references)
                    | _ -> model) this

    member this.AddReferences(libs) = this.AddReferences(libs, NuspecReferences.All)

    member this.AddFrameworkAssemblyReference(framework:FrameworkIdentifier,assemblyName) : InstallModel = 
        let g = framework.Group
        { this
            with 
                Groups = 
                    match this.Groups.TryFind g with
                    | Some group ->
                        Map.add 
                          g
                          ({ group with Frameworks = 
                                         match Map.tryFind framework group.Frameworks with
                                         | Some files -> Map.add framework (files.AddFrameworkAssemblyReference assemblyName) group.Frameworks
                                         | None -> Map.add framework (InstallFiles.empty.AddFrameworkAssemblyReference assemblyName) group.Frameworks })
                          this.Groups                    
                    | None -> this.Groups }

        

    member this.AddFrameworkAssemblyReferences(references) : InstallModel  = 
        references
        |> Seq.fold (fun model reference -> model.AddFrameworkAssemblyReference(reference.TargetFramework,reference.AssemblyName)) this

    member this.FilterBlackList() =
        let blackList =
            [fun (reference:Reference) -> 
                match reference with
                | Reference.Library lib -> not (lib.EndsWith ".dll" || lib.EndsWith ".exe")
                | _ -> false]

        { this
            with 
                Groups = 
                    this.Groups 
                    |> Map.map (fun name group ->
                                { group with Frameworks = 
                                             blackList 
                                             |> List.fold 
                                                    (fun frameworks f ->  Map.map (fun _ files -> {files with References = files.References |> Set.filter (f >> not)}) frameworks) 
                                                group.Frameworks } ) }


    member this.UseLowerVersionLibIfEmpty() = 
        let group =
            FrameworkVersion.KnownDotNetFrameworks
            |> List.rev
            |> List.fold (fun (group : FrameworkGroup) (lowerVersion) -> 
                   let newFiles = group.GetReferences(DotNetFramework(lowerVersion))
                   if Set.isEmpty newFiles then group else 
                   FrameworkVersion.KnownDotNetFrameworks
                    |> List.filter (fun (version) -> version > lowerVersion)
                    |> List.fold (fun (group : FrameworkGroup) upperVersion -> 
                            let framework = DotNetFramework(upperVersion)
                            match Map.tryFind framework group.Frameworks with
                            | Some files when Set.isEmpty files.References -> 
                                { group with Frameworks = Map.add framework { References = newFiles; ContentFiles = Set.empty } group.Frameworks }
                            | _ -> group) group) (Map.find FrameworkIdentifier.DefaultGroup this.Groups)
        { this with
            Groups = Map.add FrameworkIdentifier.DefaultGroup group this.Groups }

    member this.UseLowerVersionLibForSpecicalFrameworksIfEmpty() = 
        let newFiles = this.GetReferences(DotNetFramework(FrameworkVersion.V4_5))
        if Set.isEmpty newFiles then this else 
        
        InstallModel.KnownSpecialTargets
        |> List.fold (fun (model : InstallModel) framework ->
                            let g = framework.Group
                            { model 
                                with Groups =
                                        Map.add
                                          g                   
                                          (match Map.tryFind g model.Groups with
                                            | Some group ->
                                                match Map.tryFind framework group.Frameworks with
                                                | Some files when Set.isEmpty files.References -> 
                                                    { group with Frameworks = Map.add framework { References = newFiles; ContentFiles = Set.empty } group.Frameworks }
                                                | None -> { group with Frameworks = Map.add framework { References = newFiles; ContentFiles = Set.empty } group.Frameworks }
                                                | _ -> group
                                            | None -> { Frameworks = Map.add framework { References = newFiles; ContentFiles = Set.empty } Map.empty; Fallbacks = InstallFiles.empty } )
                                          model.Groups})
                    this

    member this.UseLastInGroupAsFallback() =
        { this with Groups =
                        this.Groups
                        |> Map.map (fun keey group -> { group with Fallbacks =(group.Frameworks |> Seq.last).Value })}

    member this.DeleteIfGroupFallback() =
        { this with Groups =
                        this.Groups
                        |> Map.map (fun key group -> 
                                        let fallbacks = group.Fallbacks
                                        {group with Frameworks =
                                                        group.Frameworks 
                                                        |> Seq.fold 
                                                            (fun frameworks kv ->
                                                                let files = kv.Value
                                                                if files.References <> fallbacks.References then frameworks else
                                                                Map.remove kv.Key frameworks) group.Frameworks})
                                               }

    member this.DeleteEmptyGroupIfDefaultFallback() =    
        let defaultFallbacks = this.DefaultFallback
        { this 
           with Groups =
                    this.Groups 
                    |> Map.map 
                        (fun g group -> 
                            let fallbacks = group.Fallbacks
                            group.Frameworks
                            |> Seq.fold (fun (group:FrameworkGroup) framework ->
                                          if framework.Value.References <> fallbacks.References then group else
                                           { group with Frameworks = Map.remove framework.Key group.Frameworks })
                                         group) }

    member this.UsePortableVersionLibIfEmpty() = 
        this.GetFrameworks()
        |> Seq.fold 
            (fun (model : InstallModel) kv -> 
                let newFiles = kv.Value.References
           
                if Set.isEmpty newFiles then model else

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
                    let g = framework.Group                    
                    match Map.tryFind g model.Groups with
                    | Some group ->
                        match Map.tryFind framework group.Frameworks with
                        | Some files when Set.isEmpty files.References |> not -> group
                        | _ -> { group with Frameworks = Map.add framework { References = newFiles; ContentFiles = Set.empty} group.Frameworks })
                         model) this

    member this.Process() =
        this
            .UseLowerVersionLibIfEmpty()
            .UsePortableVersionLibIfEmpty()
            .UseLowerVersionLibIfEmpty()  // because we now might need to use portable
            .UseLowerVersionLibForSpecicalFrameworksIfEmpty()
            .FilterBlackList()
            .UseLastInGroupAsFallback()

    member this.ProcessAndReduce() =
        this.Process()
            .DeleteIfGroupFallback()

    member this.GetLibraryNames =
        lazy([ for g in this.Groups do
                  for f in g.Value.Frameworks do
                    for lib in f.Value.References do         
                        match lib with
                        | Reference.Library lib ->       
                            let fi = new FileInfo(normalizePath lib)
                            yield fi.Name.Replace(fi.Extension,"") 
                        | _ -> ()
                  for lib in g.Value.Fallbacks.References do         
                        match lib with
                        | Reference.Library lib ->       
                            let fi = new FileInfo(normalizePath lib)
                            yield fi.Name.Replace(fi.Extension,"") 
                        | _ -> ()
               for lib in this.DefaultFallback.References do
                    match lib with
                    | Reference.Library lib ->       
                        let fi = new FileInfo(normalizePath lib)
                        yield fi.Name.Replace(fi.Extension,"") 
                    | _ -> ()]
            |> Set.ofList)

    static member CreateFromLibs(packageName,packageVersion,libs,nuspec:Nuspec) = 
        InstallModel.EmptyModel(packageName,packageVersion)
            .AddReferences(libs,nuspec.References)
            .AddFrameworkAssemblyReferences(nuspec.FrameworkAssemblyReferences)
            .ProcessAndReduce()