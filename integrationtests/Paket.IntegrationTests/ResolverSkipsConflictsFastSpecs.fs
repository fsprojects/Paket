module Paket.IntegrationTests.ResolverSkipsConflictsFastSpecs

open Fake
open System
open NUnit.Framework
open FsUnit
open System
open System.IO

[<Test>]
let ``#1166 Should resolve Nancy without timeount``() = 
    update "i001166-resolve-nancy-fast"

[<Test>]
let ``#1174 Should find Ninject error``() = 
    updateShouldFindPackageConflict "Ninject" "i001174-resolve-fast-conflict"