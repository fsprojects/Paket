module Paket.IntegrationTests.RemoveSpecs

open Fake
open System
open NUnit.Framework
open FsUnit
open System
open System.IO
open System.Diagnostics
open Paket
open Paket.Domain

[<Test>]
let ``#1600 paket remove nuget should remove empty groups``() = 
    paket "remove add Castle.Core testgroup" "i001600-remove-empty-group" |> ignore
