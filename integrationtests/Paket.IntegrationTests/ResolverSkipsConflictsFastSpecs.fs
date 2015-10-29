module Paket.IntegrationTests.ResolverSkipsConflictsFastSpecs

open Fake
open System
open NUnit.Framework
open FsUnit
open System
open System.IO

[<Test>]
let ``#1166 Should resolve Nancy without timeout``() = 
    update "i001166-resolve-nancy-fast"

[<Test>]
let ``#1157 should resolve from multiple feeds``() = 
    update "i001157-resolve-multiple-feeds"


[<Test>]
let ``#1174 Should find Ninject error``() = 
    updateShouldFindPackageConflict "Ninject" "i001174-resolve-fast-conflict"
