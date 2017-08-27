namespace Paket
open System.IO
open Paket.Domain

[<AutoOpen>]
module CacheExtensions =
    type Nuspec with
        static member LoadFromCache(name:PackageName, version) =
            let folder = DirectoryInfo(NuGetCache.GetTargetUserFolder name version).FullName
            let nuspec = Path.Combine(folder,sprintf "%O.nuspec" name)
            Nuspec.Load nuspec

    type PackageResolver.PackageInfo with
        member x.Folder root groupName =
            let settings = x.Settings
            let includeVersion = defaultArg settings.IncludeVersionInPath false
            let storageConf = defaultArg settings.StorageConfig PackagesFolderGroupConfig.Default
            match (storageConf.Resolve root groupName x.Name x.Version includeVersion).Path with
            | Some f -> f
            | None ->
                NuGetCache.GetTargetUserFolder x.Name x.Version
