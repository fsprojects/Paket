namespace Paket

open System.IO
open Paket.Domain
open System.Collections.Concurrent
open PackageResolver
open Mono.Cecil
open System.Collections.Generic
open Logging
open ProviderImplementation.AssemblyReader.Utils.SHA1

// Needs an update so that all work is not done at once
// computation should be done on a per group/per framework basis
// and cached for later retrieval


type ReferenceType =
    | Assembly  of FileInfo
    | Framework of string
    /// Relative path based on base-directory
    | LoadScript of string
    override self.ToString () = self |> function
        | Assembly info -> sprintf "Assembly: '%s'" info.FullName
        | Framework info -> sprintf "Framework: '%s'" info
        | LoadScript info -> sprintf "LoadScript: '%s'" info

type DependencyCache (lockFile:LockFile) =
    let loadedGroups = HashSet<GroupName>()
    let mutable nuspecCache = ConcurrentDictionary<PackageName*SemVerInfo, Nuspec>()
    let mutable installModelCache = ConcurrentDictionary<GroupName * PackageName,InstallModel>()
    let mutable orderedGroupCache = ConcurrentDictionary<GroupName,PackageInfo list>()
    let mutable orderedGroupReferences = ConcurrentDictionary<(GroupName * FrameworkIdentifier),ReferenceType list>()


    let getLeafPackagesGeneric getPackageName getDependencies (knownPackages:Set<_>) openList =
        
        let leafPackages =
            openList |> List.filter (fun p ->
                not (knownPackages.Contains(getPackageName p)) &&
                getDependencies p |> Seq.forall (knownPackages.Contains)
            )

        let newKnownPackages =
            (knownPackages,leafPackages)
            ||> Seq.fold (fun state package -> state |> Set.add (getPackageName package)) 

        let newState =
            openList |> List.filter (fun p -> 
                leafPackages |> Seq.forall (fun l -> getPackageName l <> getPackageName p)
            )
        leafPackages, newKnownPackages, newState


    let getPackageOrderGeneric getPackageName getDependencies packages =
        
        let rec step finalList knownPackages currentPackages =
            match currentPackages |> getLeafPackagesGeneric getPackageName getDependencies knownPackages with
            | ([], _, _) -> finalList
            | (leafPackages, newKnownPackages, newState) ->
                step (leafPackages @ finalList) newKnownPackages newState
        
        step [] Set.empty packages
        |> List.rev  


    let getPackageOrderResolvedPackage =
        getPackageOrderGeneric 
            (fun (p:PackageResolver.PackageInfo) -> p.Name) 
            (fun p -> p.Dependencies |> Seq.map (fun (n,_,_) -> n))


    let loadPackages () =
        let packs = 
            lockFile.GetResolvedPackages() |> Seq.map (fun kvp -> async {
                let groupName, packages = kvp.Key,kvp.Value
                let orderedPackages = getPackageOrderResolvedPackage kvp.Value
                orderedGroupCache.TryAdd (groupName,orderedPackages) |> ignore
            }) |> Array.ofSeq
        Async.Parallel packs 
        |> Async.RunSynchronously 
        |> ignore
    
    do loadPackages ()

    let getDllOrder (dllFiles : AssemblyDefinition list) =
        // this check saves looking at assembly metadata when we know this is not needed
        if List.length dllFiles = 1 then dllFiles else
        // we ignore all unknown references as they are most likely resolved on package level
        let known = dllFiles |> Seq.map (fun a -> a.FullName) |> Set.ofSeq
        getPackageOrderGeneric
            (fun (p:AssemblyDefinition) -> p.FullName)
            (fun p -> p.MainModule.AssemblyReferences |> Seq.map (fun r -> r.FullName) |> Seq.filter (known.Contains))
            dllFiles


    let getDllsWithinPackage (framework: FrameworkIdentifier) (installModel :InstallModel) =
        let dllFiles =
            installModel
            |> InstallModel.getLegacyReferences (TargetProfile.SinglePlatform framework)
            |> Seq.map (fun l -> l.Path)
            |> Seq.map (fun path -> AssemblyDefinition.ReadAssembly path, FileInfo(path))
            |> dict

        getDllOrder (dllFiles.Keys |> Seq.toList)
        |> List.map (fun a -> dllFiles.[a])
    
    let referencesForGroup group (framework:FrameworkIdentifier) = 
        let libs = HashSet<FileInfo>()
        let sysLibs = HashSet<_>()
        match tryGet group orderedGroupCache with
        | None -> []
        | Some packs -> 
            for pack in packs do
                match tryGet (group,pack.Name) installModelCache with
                | None -> ()
                | Some model ->
                    for lib in model.GetLibReferenceFiles (TargetProfile.SinglePlatform framework) do
                        libs.Add lib |> ignore
                    for sysLib in model.GetAllLegacyFrameworkReferences() do
                        sysLibs.Add sysLib |> ignore

            let assemblyFilePerAssemblyDef = 
                libs |> Seq.map (fun (f:FileInfo) -> 
                    AssemblyDefinition.ReadAssembly (f.FullName:string), f)
                |> dict

            let assemblies = 
                assemblyFilePerAssemblyDef.Keys
                |> Seq.toList |> getDllOrder
                |> Seq.map (assemblyFilePerAssemblyDef.TryGetValue >> snd)

            let assemblyRefs = 
                assemblies |> Seq.map ReferenceType.Assembly 

            let frameworkRefs = 
                sysLibs
                |> Seq.map (fun x ->  ReferenceType.Framework x.Name)

            Seq.append assemblyRefs  frameworkRefs |> List.ofSeq


    member self.GetOrderedReferences groupName framework =
        match tryGet (groupName,framework) orderedGroupReferences with
        | Some refs -> refs 
        | None -> 
            
            self.SetupGroup groupName |> ignore
            let refs = referencesForGroup groupName framework 
            orderedGroupReferences.TryAdd((groupName,framework),refs)|> ignore
            refs 
            

    member __.GetOrderedPackageReferences groupName packageName framework =
        match tryGet (groupName,packageName) installModelCache with
        | None -> []
        | Some model -> 
            getDllsWithinPackage framework model 
    
    
    member self.GetOrderedFrameworkReferences  groupName packageName (framework: FrameworkIdentifier) =
        match tryGet (groupName,packageName) installModelCache with
        | None -> []
        | Some installModel -> 
            let shouldExcludeFrameworkAssemblies =
                // NOTE: apparently for .netcore / .netstandard we should skip framework dependencies
                // https://github.com/fsprojects/Paket/issues/2156
                function
                | FrameworkIdentifier.DotNetCoreApp _
                | FrameworkIdentifier.DotNetStandard _ -> true
                | _ -> false

            if shouldExcludeFrameworkAssemblies framework then List.empty else
            installModel
            |> InstallModel.getAllLegacyFrameworkReferences
            |> Seq.toList


    member __.LockFile = lockFile
    
    member __.InstallModels () = 
        installModelCache |> Seq.map (fun x -> x.Value) |> List.ofSeq
    
    member __.Nuspecs () = 
        nuspecCache |> Seq.map (fun x -> x.Value) |> List.ofSeq

    member __.SetupGroup (groupName:GroupName) : bool =
        if loadedGroups.Contains groupName then 
            true 
        else
            match tryGet groupName orderedGroupCache with
            | None -> false
            | Some resolvedPackageList ->
                let exprs =
                    if resolvedPackageList <> [] then
                        if verbose then
                            verbosefn "[Loading packages from group - %O]" groupName

                    resolvedPackageList 
                    |> List.map (fun package -> async {
                        let folder = package.Folder lockFile.RootPath groupName

                        if Directory.Exists folder |> not then
                            return failwithf "Folder %s doesn't exist. Did you restore groups %O?" folder groupName

                        let nuspecShort = Path.Combine(folder, sprintf "%O.nuspec" package.Name)
                        if verbose then
                            verbosefn " -- %s" nuspecShort
                        let nuspec = FileInfo(Path.Combine (lockFile.RootPath,nuspecShort))
                        let nuspec = Nuspec.Load nuspec.FullName
                        nuspecCache.TryAdd((package.Name,package.Version),nuspec) |>ignore
                        let kind =
                            match package.Kind with
                            | ResolvedPackageKind.Package -> InstallModelKind.Package
                            | ResolvedPackageKind.DotnetCliTool -> InstallModelKind.DotnetCliTool
                        let model = 
                            InstallModel.CreateFromContent(
                                package.Name, 
                                package.Version, 
                                kind,
                                Paket.Requirements.FrameworkRestriction.NoRestriction, 
                                NuGet.GetContent(folder).Force())
                        installModelCache.TryAdd((groupName,package.Name) , model) |> ignore }) 
                    |> Array.ofSeq

                Async.Parallel exprs
                |> Async.RunSynchronously |> ignore
                loadedGroups.Add groupName |> ignore
                true

    member self.OrderedGroups () =
        orderedGroupCache |> Seq.map (fun x -> 
            self.SetupGroup x.Key |> ignore
            x.Key, x.Value
        )|> Map.ofSeq
    
    member self.OrderedGroups (groupName:GroupName) = 
        self.SetupGroup groupName |> ignore
        tryGet groupName orderedGroupCache |> Option.defaultValue []
        
    member self.ClearLoaded () = loadedGroups.Clear ()


    new (dependencyFilePath:string) = 
        let lockFile = DependenciesFile.FindLockfile dependencyFilePath |> fun path -> path.FullName |> LockFile.LoadFrom
        DependencyCache (lockFile)

 
    member __.InstallModel groupName packageName = tryGet (groupName, packageName)  installModelCache
    
