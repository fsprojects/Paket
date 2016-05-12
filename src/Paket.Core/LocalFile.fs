namespace Paket

open Paket.Domain
open Paket.ModuleResolver
open Paket.PackageSources

type DevSourceOverride =
    | DevNugetSourceOverride of packageName: PackageName * devSource: PackageSource
    | DevGitSourceOverride   of packageName: PackageName * gitSource: string

type LocalFile = LocalFile of devSourceOverrides: DevSourceOverride list

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

    let private parseLine = function
        | Regex "nuget[ ]+(.*)[ ]+->[ ]+(source[ ]+.*)" [package; source] ->
            source
            |> Trial.Catch PackageSource.Parse
            |> Trial.mapFailure (fun _ -> [sprintf "Cannot parse source '%s'" source])
            |> Trial.lift (fun s -> DevNugetSourceOverride (PackageName package, s))
        | Regex "nuget[ ]+(.*)[ ]+->[ ]+git[ ]+(.*)"    [package; gitSource] ->
            DevGitSourceOverride (PackageName package, gitSource)
            |> Trial.ok
        | line ->
            Trial.fail (sprintf "Cannot parse line '%s'" line)

    let parse =
        List.filter (not << String.IsNullOrWhiteSpace)
        >> List.map parseLine
        >> Trial.collect
        >> Trial.lift LocalFile

    let readFile =
        IO.File.ReadAllLines
        >> Array.toList
        >> parse

    let empty = LocalFile []

    let private overrideResolution (packageName, source) resolution = 
        resolution
        |> Map.map (fun name original -> 
            if name = packageName then
                { original with PackageResolver.ResolvedPackage.Source = source }
            else
                original)

    let private warning x =
        match x with
        | DevNugetSourceOverride (p,s) ->
            sprintf "paket.local override: nuget %s -> %s" (p.ToString()) (s.ToString())
        | DevGitSourceOverride   (p,s) ->
            sprintf "paket.local override: nuget %s -> %s" (p.ToString()) s
        |> Logging.traceWarn
        x

    let overrideDependency (lockFile: LockFile) = warning >> function
        | DevNugetSourceOverride (p,s) -> 
            let groups =
                lockFile.Groups
                |> Map.map (fun _ g -> { g with Resolution = overrideResolution (p,s) g.Resolution } )
            LockFile(lockFile.FileName, groups)
        | DevGitSourceOverride   (p,s) ->
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
                |> Map.map (fun _ g -> 
                    { g with Resolution  = overrideResolution (p,source g.Name) g.Resolution
                             RemoteFiles = remoteFile :: g.RemoteFiles } )
            LockFile(lockFile.FileName, groups)

    let overrideLockFile (LocalFile overrides) lockFile =
        List.fold overrideDependency lockFile overrides

    let overrides (LocalFile xs) package =
        xs 
        |> List.exists (function | DevNugetSourceOverride (p, _)
                                 | DevGitSourceOverride   (p, _) -> p = package)