module Paket.IntegrationTests.AddSpecs

open Fake
open System
open NUnit.Framework
open FsUnit
open System
open System.IO
open Pri.LongPath
open System.Diagnostics
open Paket
open Paket.Domain

[<Test>]
let ``#310 paket add nuget should not resolve inconsistent dependency graph``() = 
    try
        paket "add nuget Castle.Windsor version 3.3.0" "i000310-add-should-not-create-invalid-resolution" |> ignore
        failwith "resolver error expected"
    with
    | exn when exn.Message.Contains("There was a version conflict during package resolution") -> ()