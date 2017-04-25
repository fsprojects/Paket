namespace Paket

open System.IO
open Paket.Domain
open System.Collections.Concurrent
open PackageResolver
open Mono.Cecil
open System.Collections.Generic

// Needs an update so that all work is not done at once
// computation should be done on a per group/per framework basis
// and cached for later retrieval


type ReferenceType =
    | Assembly  of FileInfo
    | Framework of string
    | LoadScript of FileInfo

type DependencyCache (dependencyFile:DependenciesFile, lockFile:LockFile) =
    let loadedGroups = HashSet<GroupName>()
    let mutable nuspecCache = ConcurrentDictionary<PackageName*SemVerInfo, Nuspec>()
    let mutable installModelCache = ConcurrentDictionary<GroupName * PackageName,InstallModel>()
    let mutable orderedGroupCache = ConcurrentDictionary<GroupName,ResolvedPackage list>()
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
            (fun (p:PackageResolver.ResolvedPackage) -> p.Name) 
            (fun p -> p.Dependencies |> Seq.map (fun (n,_,_) -> n))


    //let getPackageOrderFromLockFile (lockFile:LockFile) =
    //    lockFile.GetResolvedPackages ()
    //    |> Seq.map (fun kvp -> kvp.Key , getPackageOrderResolvedPackage kvp.Value)
    //    |> Map.ofSeq
        

    let getDllOrder (dllFiles : AssemblyDefinition list) =
        // this check saves looking at assembly metadata when we know this is not needed
        if List.length dllFiles = 1 then dllFiles  else
        // we ignore all unknown references as they are most likely resolved on package level
        let known = dllFiles |> Seq.map (fun a -> a.FullName) |> Set.ofSeq
        getPackageOrderGeneric
            (fun (p:AssemblyDefinition) -> p.FullName)
            (fun p -> p.MainModule.AssemblyReferences |> Seq.map (fun r -> r.FullName) |> Seq.filter (known.Contains))
            dllFiles


    let getDllsWithinPackage (framework: FrameworkIdentifier) (installModel :InstallModel) =
        let dllFiles =
            installModel
            |> InstallModel.getLegacyReferences (SinglePlatform framework)
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
            packs |> List.iter (fun pack -> 
                match tryGet (group,pack.Name) installModelCache with
                | None -> ()
                | Some model ->
                    model.GetLibReferenceFiles framework |> Seq.iter (libs.Add >> ignore)
                    model.GetAllLegacyFrameworkReferences ()|> Seq.iter (sysLibs.Add >> ignore)
            )

            let assemblyFilePerAssemblyDef = 
                libs |> Seq.map (fun (f:FileInfo) -> 
                    AssemblyDefinition.ReadAssembly(f.FullName:string), f)
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
            

    member self.GetOrderedPackageReferences groupName packageName framework =
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
                | FrameworkIdentifier.DotNetCore _ 
                | FrameworkIdentifier.DotNetStandard _ -> true
                | _ -> false

            if shouldExcludeFrameworkAssemblies framework then List.empty else
            installModel
            |> InstallModel.getAllLegacyFrameworkReferences
            |> Seq.toList


    member __.LockFile = lockFile
    member __.DependenciesFile = dependencyFile
    member __.InstallModels () = installModelCache|> Seq.map(fun x->x.Value)|>List.ofSeq
    member __.Nuspecs () = nuspecCache|> Seq.map(fun x->x.Value)|>List.ofSeq

    member __.OrderedGroups () =  orderedGroupCache|> Seq.map (fun x -> x.Key,x.Value)|> Map.ofSeq
    member __.OrderedGroups (groupName:GroupName) = 
         tryGet groupName orderedGroupCache |> Option.defaultValue []
        
    member self.ClearLoaded () = loadedGroups.Clear ()

    member self.LoadPackages () =
        let packs = 
            lockFile.GetResolvedPackages() |> Seq.map (fun kvp -> async {
                let groupName, packages = kvp.Key,kvp.Value
                let orderedPackages = getPackageOrderResolvedPackage kvp.Value
                orderedGroupCache.TryAdd (groupName,orderedPackages) |> ignore
            }) |> Array.ofSeq
        Async.Parallel packs 
        |> Async.RunSynchronously 
        |> ignore

    member self.SetupGroup (groupName:GroupName) : bool =
        if loadedGroups.Contains groupName then true else
        match tryGet groupName  orderedGroupCache with
        | None -> false
        | Some packages ->
            let exprs =
                self.OrderedGroups groupName |> List.map (fun package -> async {
                    let packageName = package.Name
                    let groupFolder = if groupName = Constants.MainDependencyGroup then "" else "/" + groupName.GetCompareString()
                    let folder = DirectoryInfo(sprintf "%s/packages%s/%O" lockFile.RootPath groupFolder packageName)
                    let nuspec = FileInfo(sprintf "%s/packages%s/%O/%O.nuspec" lockFile.RootPath groupFolder packageName packageName)
                    printfn "%s" nuspec.FullName
                    let nuspec = Nuspec.Load nuspec.FullName
                    nuspecCache.TryAdd((package.Name,package.Version),nuspec)|>ignore
                    let files = NuGetV2.GetLibFiles(folder.FullName)                    
                    let model = InstallModel.CreateFromLibs(packageName, package.Version, [], files, [], [], nuspec)
                    installModelCache.TryAdd((groupName,package.Name) , model) |> ignore                
                }) |> Array.ofSeq
            Async.Parallel exprs
            |> Async.RunSynchronously |> ignore
            loadedGroups.Add groupName |> ignore
            true

    new (dependencyFilePath:string) as self = 
        let depFile = DependenciesFile.ReadFromFile dependencyFilePath 
        let lockFile = depFile.FindLockfile() |> fun path -> path.FullName |> LockFile.LoadFrom
        DependencyCache (depFile,lockFile) then 
            self.LoadPackages()
 
    member __.InstallModel groupName packageName = tryGet (groupName, packageName)  installModelCache
    
