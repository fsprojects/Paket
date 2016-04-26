namespace Paket

open Chessie.ErrorHandling

open Paket.Domain
open Paket.PackageSources

type DevSourceOverride =
    | DevNugetSourceOverride of packageName: PackageName * devSource: PackageSource

type LocalFile = LocalFile of devSourceOverrides: DevSourceOverride list

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module LocalFile =
    let parse lines =
        LocalFile []
        |> Trial.pass