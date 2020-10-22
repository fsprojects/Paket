namespace Paket

open Paket.Domain
open Paket.ModuleResolver
open Paket.PackageSources

type OverriddenPackage = {
    Name : PackageName
    Group : GroupName
}

type LocalOverride =
    | LocalSourceOverride of package: OverriddenPackage * devSource: PackageSource * version: Option<SemVerInfo>
    | LocalGitOverride    of package: OverriddenPackage * gitSource: string

type LocalFile = LocalFile of devSourceOverrides: LocalOverride list

module LocalFile =
    open System

    open Chessie.ErrorHandling
    open Logging

    let private (|Regex|_|) pattern input =
        let m = System.Text.RegularExpressions.Regex(pattern).Match(input)
        if m.Success then
            Some(List.tail [ for g in m.Groups -> g.Value ])
        else
            None

    let nameGroup (name, group) =
        let group =
            if group = "" then
                Constants.MainDependencyGroup
            else
                GroupName group
        { Name = PackageName name
          Group = group }

    let private parseLine = function
        | Regex
            "^nuget[ ]+(.*?)([ ]+group[ ]+(.*))?[ ]+->[ ]+(source[ ]+.*?)([ ]+version[ ]+(.*))?$"
            [package; _; group; source; _; version] ->
            let v =
                if String.IsNullOrWhiteSpace version then None
                else
                    try SemVer.Parse version |> Some
                    with exn ->
                        traceWarnfn "Could not parse version '%s': %s" version exn.Message
                        if verbose then
                            traceWarnfn "Exception: %O" exn
                        None
            source
            |> Trial.Catch PackageSource.Parse
            |> Trial.mapFailure (fun _ -> [sprintf "Cannot parse source '%s'" source])
            |> Trial.lift (fun s -> LocalSourceOverride (nameGroup(package, group), s, v))
        | Regex
            "nuget[ ]+(.*?)([ ]+group[ ]+(.*))?[ ]+->[ ]+git[ ]+(.*)"
            [package; _; group; gitSource] ->
            LocalGitOverride (nameGroup(package, group), gitSource)
            |> Trial.ok
        | line ->
            Trial.fail (sprintf "Cannot parse line '%s'" line)

    let parse =
        List.map String.trim
        >> List.filter (not << String.IsNullOrWhiteSpace)
        >> List.filter (not << String.startsWithIgnoreCase @"//")
        >> List.filter (not << String.startsWithIgnoreCase @"#")
        >> List.map parseLine
        >> Trial.collect
        >> Trial.lift LocalFile

    let readFile =
        IO.File.ReadAllLines
        >> Array.toList
        >> parse

    let empty = LocalFile []

    let private overrideResolution (p, v, source) resolution =
        resolution
        |> Map.map (fun name original ->
            if name = p then
                { original with PackageResolver.ResolvedPackage.Source = source
                                PackageResolver.ResolvedPackage.Version =
                                    defaultArg v original.Version }
            else
                original)

    let private warning x =
        match x with
        | LocalSourceOverride ({ Name = p; Group = g},s, Some v) ->
            sprintf "nuget %s group %s -> %s version %s" (p.ToString()) (g.ToString()) (s.ToString()) (v.ToString())
        | LocalSourceOverride ({ Name = p; Group = g},s, None) ->
            sprintf "nuget %s group %s -> %s" (p.ToString()) (g.ToString()) (s.ToString())
        | LocalGitOverride   ({ Name = p; Group = g},s) ->
            sprintf "nuget %s group %s -> %s" (p.ToString()) (g.ToString()) s
        |> (+) "paket.local override: "
        |> Logging.traceWarn
        x

    let overrideDependency (lockFile: LockFile) = warning >> function
        | LocalSourceOverride ({ Name = p; Group = group },s,v) ->
            let groups =
                lockFile.Groups
                |> Map.map (fun name g ->
                    if name = group then
                        { g with Resolution = overrideResolution (p,v,s) g.Resolution }
                    else
                        g )
            LockFile(lockFile.FileName, groups)
        | LocalGitOverride   ({ Name = p; Group = group},s) ->
            let owner,branch,project,cloneUrl,buildCommand,operatingSystemRestriction,packagePath =
                Git.Handling.extractUrlParts s
            let restriction = VersionRestriction.Concrete (defaultArg branch "master")
            let sha =
                RemoteDownload.getSHA1OfBranch (GitLink cloneUrl) owner project restriction None
                |> Async.RunSynchronously

            let remoteFile = {
                ResolvedSourceFile.Commit = sha
                Owner = owner
                Origin = GitLink cloneUrl
                Project = project
                Dependencies = Set.empty
                Command = buildCommand
                OperatingSystemRestriction = operatingSystemRestriction
                PackagePath = packagePath
                Name = ""
                AuthKey = None
            }

            let packagesPath =
                match packagePath with
                | Some p -> p
                | None   ->
                    failwith "paket.local: only git repositories as NuGet source (with 'Packages: ...') are currently supported"

            let source groupName =
                let root = ""
                let fullPath = remoteFile.ComputeFilePath(root,groupName, packagesPath)
                let relative = (createRelativePath root fullPath).Replace("\\","/")
                LocalNuGet(relative, None)

            let groups =
                lockFile.Groups
                |> Map.map (fun name g ->
                    if name = group then
                        { g with Resolution  = overrideResolution (p,None,source g.Name) g.Resolution
                                 RemoteFiles = remoteFile :: g.RemoteFiles }
                    else
                        g)
            LockFile(lockFile.FileName, groups)

    let overrideLockFile (LocalFile overrides) lockFile =
        List.fold overrideDependency lockFile overrides

    let overrides (LocalFile xs) (package, group) =
        xs
        |> List.exists (function | LocalSourceOverride ({ Name = p; Group = g}, _, _)
                                 | LocalGitOverride    ({ Name = p; Group = g}, _)
                                   -> p = package && g = group)