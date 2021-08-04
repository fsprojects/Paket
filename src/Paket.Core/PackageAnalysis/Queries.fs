module Paket.Queries // mostly logic which was in PublicAPI.fs but is useful to other places (like script generation)

open Domain
open System.IO


let getLockFileFromDependenciesFile dependenciesFileName =
    let lockFileName = DependenciesFile.FindLockfile dependenciesFileName
    LockFile.LoadFrom lockFileName.FullName

let listPackages (packages: System.Collections.Generic.KeyValuePair<GroupName*PackageName, PackageResolver.PackageInfo> seq) =
    packages
    |> Seq.map (fun kv ->
        let groupName,packageName = kv.Key
        groupName.ToString(),packageName.ToString(),kv.Value.Version.ToString())
    |> Seq.toList

let getAllInstalledPackagesFromLockFile (lockFile: LockFile) =
    lockFile.GetGroupedResolution() |> listPackages

let getInstalledPackageModel (lockFile: LockFile) (QualifiedPackageName(groupName, packageName)) =
    match lockFile.Groups |> Map.tryFind groupName with
    | None -> failwithf "Group %O can't be found in paket.lock." groupName
    | Some group ->
        match group.TryFind(packageName) with
        | None -> failwithf "Package %O is not installed in group %O." packageName groupName
        | Some resolvedPackage ->
            let packageName = resolvedPackage.Name
            let storage = defaultArg resolvedPackage.Settings.StorageConfig PackagesFolderGroupConfig.Default
            let includeVersion = defaultArg resolvedPackage.Settings.IncludeVersionInPath false
            let resolvedConfig = storage.Resolve lockFile.RootPath groupName packageName resolvedPackage.Version includeVersion
            let folder =
                match resolvedConfig.Path with
                | Some f -> f
                | None -> NuGetCache.GetTargetUserFolder packageName resolvedPackage.Version
            let kind =
                match resolvedPackage.Kind with
                | PackageResolver.ResolvedPackageKind.Package -> InstallModelKind.Package
                | PackageResolver.ResolvedPackageKind.DotnetCliTool -> InstallModelKind.DotnetCliTool

            InstallModel.CreateFromContent(
                packageName,
                resolvedPackage.Version,
                kind,
                Paket.Requirements.FrameworkRestriction.NoRestriction,
                NuGet.GetContent(folder).Force())

let getRuntimeGraph (lockFile: LockFile) (groupName:GroupName) =
    lockFile.Groups
    |> Map.tryFind groupName
    |> Option.toList
    |> Seq.collect (fun group -> group.Resolution |> Map.toSeq)
    |> Seq.map snd
    |> Seq.choose (fun p ->
        let groupFolder = if groupName = Constants.MainDependencyGroup then "" else "/" + groupName.ToString()
        let runtimeJson = sprintf "%s/packages%s/%O/runtime.json" lockFile.RootPath groupFolder p.Name
        if File.Exists runtimeJson then
            let data = File.ReadAllText runtimeJson
            Some (RuntimeGraphParser.readRuntimeGraph data)
        else
            None)
    |> RuntimeGraph.mergeSeq

let getRuntimePackages rid (lockFile:LockFile) (groupName:GroupName) =
    let g = getRuntimeGraph lockFile groupName

    lockFile.Groups
    |> Map.tryFind groupName
    |> Option.toList
    |> Seq.collect (fun group -> group.Resolution |> Map.toSeq)
    |> Seq.map snd
    |> Seq.collect (fun p -> RuntimeGraph.findRuntimeDependencies rid p.Name g)
    |> Seq.toList

let resolveFrameworkForScriptGeneration (dependencies: DependenciesFile) = lazy (
    dependencies.Groups
        |> Seq.map (fun f -> f.Value.Options.Settings.FrameworkRestrictions)
        |> Seq.map(fun restrictions ->
            match restrictions with
            | Paket.Requirements.AutoDetectFramework -> failwithf "couldn't detect framework"
            | Paket.Requirements.ExplicitRestriction list -> list.RepresentedFrameworks
          )
        |> Seq.concat
    )

let resolveEnvironmentFrameworkForScriptGeneration = lazy (
    // HACK: resolve .net version based on environment
    // list of match is incomplete / inaccurate
#if DOTNETCORE
    // Environment.Version is not supported
    //dunno what is used for, using a default
    DotNetFramework FrameworkVersion.V4_5
#else
    let version = System.Environment.Version
    match version.Major, version.Minor, version.Build, version.Revision with
    | 4, 0, 30319, 42000 -> DotNetFramework FrameworkVersion.V4_6
    | 4, 0, 30319, _ -> DotNetFramework FrameworkVersion.V4_5
    | _ -> DotNetFramework FrameworkVersion.V4_5 // paket.exe is compiled for framework 4.5
#endif
    )