module Paket.IntegrationTests.ResolverSkipsConflictsFastSpecs

open Fake
open Paket
open System
open NUnit.Framework
open FsUnit
open System
open System.IO
open Paket.Domain

[<Test>]
let ``#1166 Should resolve Nancy without timeout``() =
    let lockFile = update "i001166-resolve-nancy-fast"
    lockFile.Groups.[Constants.MainDependencyGroup].Resolution.[PackageName "Nancy"].Version
    |> shouldBeGreaterThan (SemVer.Parse "1.1")

[<Test>]
let ``#1174 Should find Ninject error``() =
    updateShouldFindPackageConflict "Ninject" "i001174-resolve-fast-conflict"