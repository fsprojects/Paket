namespace Paket

open Chessie.ErrorHandling

open Paket.Domain
open Paket.PackageSources

type DevSourceOverride =
    | DevNugetSourceOverride of packageName: PackageName * devSource: PackageSource

type LocalFile = DevSourceOverride list

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module LocalFile =
    let parse lines =
        Trial.pass ()