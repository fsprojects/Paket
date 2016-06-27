[<AutoOpen>]
module Paket.PowerShell.ArgumentParserExt

open Argu

// https://github.com/nessos/UnionArgParser/issues/35

type ArgumentParser<'Args when 'Args :> IArgParserTemplate> with
    member parser.CreateParseResultsOfList(inputs : 'Args list) : ParseResult<'Args> =
        let cliParams = parser.PrintCommandLine inputs // unparse inputs to cli form
        parser.ParseCommandLine cliParams