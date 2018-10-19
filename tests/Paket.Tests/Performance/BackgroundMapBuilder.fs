module Performance.BackgroundMapBuilder

open Paket.Profile

open FsCheck
open NUnit.Framework
open Paket
open System

[<Test>]
let ``check that building maps in the background works``() =
    // Test is that no exception is thrown here.
    PublicAPI.PreCalculateMaps().Wait()