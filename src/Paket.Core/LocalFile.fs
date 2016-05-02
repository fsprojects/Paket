namespace Paket

open Paket.Domain
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
        | Regex "nuget[ ]+(.*)[ ]+->[ ]+(git[ ]+.*)"    [package; gitSource] ->
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

    let overrideDependency (lockFile: LockFile) = function
        | DevNugetSourceOverride (p,s) -> 
            let groups =
                lockFile.Groups
                |> Map.map (fun _ g -> { g with Resolution = overrideResolution (p,s) g.Resolution } )
            LockFile(lockFile.FileName, groups)
        | DevGitSourceOverride   (p,s) ->
            let owner,vr,project,url,buildCommand,operatingSystemRestriction,packagePath = 
                Git.Handling.extractUrlParts s
            let source =
                packagePath
                |> Option.map (fun p -> LocalNuGet(p, None))

            let groups =
                lockFile.Groups
                |> Map.map (fun _ g -> { g with Resolution = 
                                                    match source with
                                                    | Some source ->
                                                        overrideResolution (p,source) g.Resolution
                                                    | None -> g.Resolution
                                                RemoteFiles = g.RemoteFiles } )
            LockFile(lockFile.FileName, groups)

    let overrideLockFile (LocalFile overrides) lockFile =
        List.fold overrideDependency lockFile overrides