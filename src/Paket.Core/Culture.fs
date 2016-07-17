namespace Paket
open System
open System.Collections.Generic
open System.Globalization

[<CompilationRepresentation (CompilationRepresentationFlags.ModuleSuffix)>]
module Culture =
    let private allLanguageNames =
        let allLanguageNames = 
            CultureInfo.GetCultures CultureTypes.AllCultures
            |> Array.map (fun c -> c.Name)
            |> Array.filter (String.IsNullOrEmpty >> not)
        HashSet<_>(allLanguageNames, StringComparer.OrdinalIgnoreCase)

    let isLanguageName text = 
        if String.IsNullOrWhiteSpace text then
            false
        else
            allLanguageNames.Contains text
