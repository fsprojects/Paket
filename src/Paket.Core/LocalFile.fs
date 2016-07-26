namespace Paket

open Paket.Domain
open Paket.ModuleResolver
open Paket.PackageSources

type LocalOverride =
    | LocalSourceOverride of packageName: PackageName * devSource: PackageSource
    | LocalGitOverride    of packageName: PackageName * gitSource: string

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

    let private parseLine = function
        | Regex "nuget[ ]+(.*)[ ]+->[ ]+(source[ ]+.*)" [package; source] ->
            source
            |> Trial.Catch PackageSource.Parse
            |> Trial.mapFailure (fun _ -> [sprintf "Cannot parse source '%s'" source])
            |> Trial.lift (fun s -> LocalSourceOverride (PackageName package, s))
        | Regex "nuget[ ]+(.*)[ ]+->[ ]+git[ ]+(.*)"    [package; gitSource] ->
            LocalGitOverride (PackageName package, gitSource)
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

    let private overrideResolution (packageName, source) resolution = 
        resolution
        |> Map.map (fun name original -> 
            if name = packageName then
                { original with PackageResolver.ResolvedPackage.Source = source }
            else
                original)

    let private warning x =
        match x with
        | LocalSourceOverride (p,s) ->
            sprintf "nuget %s -> %s" (p.ToString()) (s.ToString())
        | LocalGitOverride   (p,s) ->
            sprintf "nuget %s -> %s" (p.ToString()) s
        |> (+) "paket.local override: "
        |> Logging.traceWarn
        x

    let overrideDependency (lockFile: LockFile) = warning >> function
        | LocalSourceOverride (p,s) -> 
            let groups =
                lockFile.Groups
                |> Map.map (fun name g -> 
                    if name.GetCompareString() = "main" then 
                        { g with Resolution = overrideResolution (p,s) g.Resolution }
                    else
                        g )
            LockFile(lockFile.FileName, groups)
        | LocalGitOverride   (p,s) ->
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
                    if name.GetCompareString() = "main" then
                        { g with Resolution  = overrideResolution (p,source g.Name) g.Resolution
                                 RemoteFiles = remoteFile :: g.RemoteFiles } 
                    else
                        g)
            LockFile(lockFile.FileName, groups)
            
    let overrideLockFile (LocalFile overrides) lockFile =
        List.fold overrideDependency lockFile overrides

    let overrides (LocalFile xs) package =
        xs 
        |> List.exists (function | LocalSourceOverride (p, _)
                                 | LocalGitOverride   (p, _) -> p = package)