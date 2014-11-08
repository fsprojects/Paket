module Paket.PackageSources

open System.Text.RegularExpressions

type NugetSource = 
    { Url : string
      Auth : Auth option }

let private parseAuth(text, source) =
    let userNameRegex = new Regex("username[:][ ]*[\"]([^\"]*)[\"]", RegexOptions.IgnoreCase)
    let passwordRegex = new Regex("password[:][ ]*[\"]([^\"]*)[\"]", RegexOptions.IgnoreCase)

    if userNameRegex.IsMatch(text) && passwordRegex.IsMatch(text) then 
        let username = AuthEntry.Create <| userNameRegex.Match(text).Groups.[1].Value
        let password = AuthEntry.Create <| passwordRegex.Match(text).Groups.[1].Value
        Some { Username = username; Password = password }
    else 
        if text.Contains("username:") || text.Contains("password:") then 
            failwithf "Could not parse auth in \"%s\"" text
        ConfigFile.GetCredentials source

/// Represents the package source type.
type PackageSource =
| Nuget of NugetSource
| LocalNuget of string
    override this.ToString() =
        match this with
        | Nuget source -> source.Url
        | LocalNuget path -> path

    static member Parse(line : string) =
        let parts = line.Split ' '
        let source = parts.[1].Replace("\"","").TrimEnd([| '/' |])
        PackageSource.Parse(source, parseAuth(line, source))

    static member Parse(source,auth) = 
        match System.Uri.TryCreate(source, System.UriKind.Absolute) with
        | true, uri -> if uri.Scheme = System.Uri.UriSchemeFile then LocalNuget(source) else Nuget({ Url = source; Auth = auth })
        | _ ->  match System.Uri.TryCreate(source, System.UriKind.Relative) with
                | true, uri -> LocalNuget(source)
                | _ -> failwithf "unable to parse package source: %s" source

    static member NugetSource url = Nuget { Url = url; Auth = None }

let DefaultNugetSource = PackageSource.NugetSource Constants.DefaultNugetStream