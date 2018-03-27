module Paket.CommandsCheck

open Argu
open NUnit.Framework
open FsUnit

[<Test>]
let ``Argu commands can be checked``() =
    ArgumentParser<Paket.Commands.Command>.CheckStructure()