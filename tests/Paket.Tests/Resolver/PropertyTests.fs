module Resovler.PropertyTests

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers
open Paket.Domain
open Paket.PackageResolver
open FsCheck


[<Test>]
let ``sound check`` () =
    let p xs = List.rev (List.rev xs) = xs
    Check.QuickThrowOnFailure p