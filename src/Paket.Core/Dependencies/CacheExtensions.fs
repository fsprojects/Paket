namespace Paket
open System.IO
open Paket.Domain

[<AutoOpen>]
module CacheExtensions =
    type Nuspec with
        static member LoadFromCache(name:PackageName, version) =
            let folder = DirectoryInfo(NuGetCache.GetTargetUserFolder name version).FullName
            let nuspec = Path.Combine(folder,sprintf "%O.nuspec" name)
            let fi = FileInfo(nuspec)
            // It happens in some cases that the package is not available in the cache, and in that case
            // it would be sensible to exhaustively search for the nuspec in the default package folder before returning
            // a Nuspec.All, see issue #3723
            // Maybe this method should be refactored/renamed now, as it not only tries to load from the cache
            // but also the packages folder.
            match fi.Exists with
            | true -> Nuspec.Load nuspec
            | _ -> let candiateNupkgs = Directory.GetFiles(Constants.DefaultPackagesFolderName, sprintf "%O.nuspec" name, SearchOption.AllDirectories)
                   let expectedNumberOfMatches = 1
                   if (candiateNupkgs.Length = expectedNumberOfMatches) then Nuspec.Load candiateNupkgs.[0] else Nuspec.Load nuspec

    type PackageResolver.PackageInfo with
        member x.Folder root groupName =
            let settings = x.Settings
            let includeVersion = defaultArg settings.IncludeVersionInPath false
            let storageConf = defaultArg settings.StorageConfig PackagesFolderGroupConfig.Default
            match (storageConf.Resolve root groupName x.Name x.Version includeVersion).Path with
            | Some f -> f
            | None ->
                NuGetCache.GetTargetUserFolder x.Name x.Version
