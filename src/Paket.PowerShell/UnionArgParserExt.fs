[<AutoOpen>]
module Paket.PowerShell.UnionArgParserExt

open Nessos.UnionArgParser

// https://github.com/nessos/UnionArgParser/issues/35

type UnionArgParser<'Args when 'Args :> IArgParserTemplate> with
    member parser.CreateParseResultsOfList(inputs : 'Args list) : ParseResults<'Args> =
        let cliParams = parser.PrintCommandLine inputs // unparse inputs to cli form
        parser.ParseCommandLine cliParams

