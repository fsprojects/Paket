namespace Paket

open System.IO
open Paket.Domain
open System.Collections.Concurrent
open PackageResolver



type DependencyCache (dependencyFile:DependenciesFile, lockFile:LockFile) =
    let rootDir = Path.Combine (lockFile.RootPath,"packages")
    
    let mutable nuspecCache = ConcurrentDictionary<PackageName*SemVerInfo, Nuspec>()
    let mutable installModelCache = ConcurrentDictionary<GroupName * ResolvedPackage,InstallModel>()

    member private __.NuspecCache = nuspecCache
    member private __.InstallModelCache = installModelCache
    
    member __.LockFile = lockFile
    member __.DependenciesFile = dependencyFile
    member __.InstallModels () = installModelCache|> Seq.map(fun x->x.Value)|>List.ofSeq
    member __.Nuspecs () = nuspecCache|> Seq.map(fun x->x.Value)|>List.ofSeq

    member self.LoadPackages () =
        let packs = 
            lockFile.GetResolvedPackages () |> Seq.map (fun kvp -> async {
                let groupName, packages = kvp.Key,kvp.Value
                packages |> List.iter (fun package ->
                    let packageName = package.Name
                    let groupFolder = if groupName = Constants.MainDependencyGroup then "" else "/" + groupName.GetCompareString()
                    let folder = DirectoryInfo(sprintf "%s/packages%s/%O" lockFile.RootPath groupFolder packageName)
                    let nuspec = FileInfo(sprintf "%s/packages%s/%O/%O.nuspec" lockFile.RootPath groupFolder packageName packageName)
                    printfn "%s" nuspec.FullName
                    let nuspec = Nuspec.Load nuspec.FullName
                    self.NuspecCache.TryAdd((package.Name,package.Version),nuspec)|>ignore
                    let files = NuGetV2.GetLibFiles(folder.FullName)
                    let model = InstallModel.CreateFromLibs(packageName, package.Version, [], files, [], [], nuspec)
                    self.InstallModelCache.TryAdd((groupName,package) , model) |> ignore
                )
            }) |> Array.ofSeq
        Async.Parallel packs 
        |> Async.RunSynchronously 
        |> ignore

    new (dependencyFilePath:string) as self = 
        let depFile = DependenciesFile.ReadFromFile dependencyFilePath 
        let lockFile = depFile.FindLockfile() |> fun path -> path.FullName |> LockFile.LoadFrom
        DependencyCache (depFile,lockFile) then 
            self.LoadPackages()
 
    member __.Item 
        with get (groupName, packageName) = installModelCache.[groupName,packageName]
    
