namespace Paket

open Paket.Domain
open Paket.ModuleResolver
open Paket.PackageSources

type OverriddenPackage =
    OverriddenPackage of packageName: PackageName * group: GroupName

type LocalOverride =
    | LocalSourceOverride of package: OverriddenPackage * devSource: PackageSource * version: string
    | LocalGitOverride    of package: OverriddenPackage * gitSource: string * version: string

type LocalFile = LocalFile of devSourceOverrides: LocalOverride list

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module LocalFile =
    open System

    open Chessie.ErrorHandling

    let private (|Regex|_|) pattern input =
        let m = System.Text.RegularExpressions.Regex(pattern).Match(input)
        if m.Success then 
            Some(List.tail [ for g in m.Groups -> g.Value ])
        else 
            None

    let private nameGroup (name, group) = 
        let group = 
            if group = "" then 
                Constants.MainDependencyGroup 
            else 
                GroupName group
        OverriddenPackage (PackageName name, group)

    let private parseLine = function
        | Regex 
            "^nuget[ ]+(.*?)([ ]+group[ ]+(.*))?[ ]+->[ ]+(source[ ]+.*?)([ ]+version[ ]+(.*))?$" 
            [package; _; group; source; _; version ] ->
            source
            |> Trial.Catch PackageSource.Parse
            |> Trial.mapFailure (fun _ -> [sprintf "Cannot parse source '%s'" source])
            |> Trial.lift (fun s -> LocalSourceOverride (nameGroup(package, group), s, version))
        | Regex 
            "^nuget[ ]+(.*?)([ ]+group[ ]+(.*))?[ ]+->[ ]+git[ ]+(.*?)([ ]+version[ ]+(.*))?$"
            [package; _; group; gitSource; _; version] ->
            LocalGitOverride (nameGroup(package, group), gitSource, version)
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

    let private overrideResolution (packageName, source, version) resolution = 
        resolution
        |> Map.map (fun name original -> 
            if name = packageName then
                { original with PackageResolver.ResolvedPackage.Source = source; PackageResolver.ResolvedPackage.Version = (if version <> null then SemVer.Parse(version) else original.Version)}
            else
                original)

    let private warning x =
        match x with
        | LocalSourceOverride (OverriddenPackage(p,g),s,version) ->
            sprintf "nuget %s group %s -> %s (%s)" (p.ToString()) (g.ToString()) (s.ToString())(version)
        | LocalGitOverride   (OverriddenPackage(p,g),s,version) ->
            sprintf "nuget %s group %s -> %s (%s)" (p.ToString()) (g.ToString()) s (version)
        |> (+) "paket.local override: "
        |> Logging.traceWarn
        x

    let overrideDependency (lockFile: LockFile) = warning >> function
        | LocalSourceOverride (OverriddenPackage(p,group),s,version) -> 
            let groups =
                lockFile.Groups
                |> Map.map (fun name g -> 
                    if name = group then 
                        { g with Resolution = overrideResolution (p,s,version) g.Resolution }
                    else
                        g )
            LockFile(lockFile.FileName, groups)
        | LocalGitOverride   (OverriddenPackage(p,group),s, version) ->
            let owner,branch,project,cloneUrl,buildCommand,operatingSystemRestriction,packagePath = 
                Git.Handling.extractUrlParts s
            let restriction = VersionRestriction.Concrete (defaultArg branch "master")
            let sha = 
                RemoteDownload.getSHA1OfBranch (GitLink(cloneUrl)) owner project restriction None 
                |> Async.RunSynchronously

            let remoteFile =
                { ResolvedSourceFile.Commit = sha
                  Owner = owner
                  Origin = GitLink(cloneUrl)
                  Project = project
                  Dependencies = Set.empty
                  Command = buildCommand
                  OperatingSystemRestriction = operatingSystemRestriction
                  PackagePath = packagePath
                  Name = "" 
                  AuthKey = None }

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
                        { g with Resolution  = overrideResolution (p,source g.Name, version) g.Resolution
                                 RemoteFiles = remoteFile :: g.RemoteFiles } 
                    else
                        g)
            LockFile(lockFile.FileName, groups)
            
    let overrideLockFile (LocalFile overrides) lockFile =
        List.fold overrideDependency lockFile overrides

    let overrides (LocalFile xs) (package, group) =
        xs 
        |> List.exists (function | LocalSourceOverride (OverriddenPackage(p,g), _, _)
                                 | LocalGitOverride    (OverriddenPackage(p,g), _, _) 
                                   -> p = package && g = group)