namespace Paket
open System
open System.Collections.Generic
open System.Globalization

[<CompilationRepresentation (CompilationRepresentationFlags.ModuleSuffix)>]
module Cultures =
    let isLanguageName (text:string) =
        try
            new CultureInfo(text) |> ignore
            true
        with :? CultureNotFoundException -> false
