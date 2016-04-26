namespace Paket

open Paket.Domain
open Paket.PackageSources

type DevSourceOverride =
    | DevNugetSourceOverride of packageName: PackageName * devSource: PackageSource

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
        | line ->
            Trial.fail (sprintf "Cannot parse line '%s'" line)

    let parse =
        List.filter (not << String.IsNullOrWhiteSpace)
        >> List.map parseLine
        >> Trial.collect
        >> Trial.lift LocalFile