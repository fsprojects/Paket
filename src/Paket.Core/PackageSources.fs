module Paket.PackageSources

open System
open System.IO
open System.Text.RegularExpressions

open Paket.Logging
open Chessie.ErrorHandling

type EnvironmentVariable = 
    { Variable : string
      Value    : string }

    static member Create(variable) = 
        let envVarRegex = Regex("^%(\w*)%$")
        if envVarRegex.IsMatch(variable) then
            let trimmed = envVarRegex.Match(variable).Groups.[1].Value
            let expanded = Environment.GetEnvironmentVariable(trimmed)
            if expanded = null then 
                traceWarnfn "environment variable '%s' not found" variable
                Some { Variable = variable; Value = ""}
            else 
                Some { Variable = variable; Value = expanded }
        else
            None

type NugetSourceAuthentication = 
    | PlainTextAuthentication of username : string * password : string
    | EnvVarAuthentication of usernameVar : EnvironmentVariable * passwordVar : EnvironmentVariable
    | ConfigAuthentication of username : string * password : string

let toBasicAuth = function
    | PlainTextAuthentication(username,password) ->
        {Username = username; Password = password}
    | EnvVarAuthentication(usernameVar, passwordVar) -> 
        {Username = usernameVar.Value; Password = passwordVar.Value}
    | ConfigAuthentication(username, password) -> 
        {Username = username; Password = password}

type NugetSource = 
    { Url : string
      Authentication : NugetSourceAuthentication option }

let private parseAuth(text, source) =
    let userNameRegex = Regex("username[:][ ]*[\"]([^\"]*)[\"]", RegexOptions.IgnoreCase)
    let passwordRegex = Regex("password[:][ ]*[\"]([^\"]*)[\"]", RegexOptions.IgnoreCase)

    if userNameRegex.IsMatch(text) && passwordRegex.IsMatch(text) then 
        let username = userNameRegex.Match(text).Groups.[1].Value
        let password = passwordRegex.Match(text).Groups.[1].Value

        let auth = 
            match EnvironmentVariable.Create(username),
                  EnvironmentVariable.Create(password) with
            | Some userNameVar, Some passwordVar ->
                EnvVarAuthentication(userNameVar, passwordVar) 
            | _, _ -> 
                PlainTextAuthentication(username, password)

        let basicAuth = toBasicAuth auth
        if(basicAuth.Username = "" && basicAuth.Password = "") then
            ConfigFile.GetCredentials source
            |> Option.map (fun (username,password) -> 
                            ConfigAuthentication(username, password))
        else
            Some(auth)
    else 
        if text.Contains("username:") || text.Contains("password:") then 
            failwithf "Could not parse auth in \"%s\"" text
        ConfigFile.GetCredentials source
        |> Option.map (fun (username,password) -> 
                            ConfigAuthentication(username, password))

/// Represents the package source type.
type PackageSource =
| Nuget of NugetSource
| LocalNuget of string
    override this.ToString() =
        match this with
        | Nuget source -> source.Url
        | LocalNuget path -> path

    static member Parse(line : string) =
        let sourceRegex = Regex("source[ ]*[\"]([^\"]*)[\"]", RegexOptions.IgnoreCase)
        let parts = line.Split ' '
        let source = 
            if sourceRegex.IsMatch line then
                sourceRegex.Match(line).Groups.[1].Value.TrimEnd([| '/' |])
            else
                parts.[1].Replace("\"","").TrimEnd([| '/' |])
        PackageSource.Parse(source, parseAuth(line, source))

    static member Parse(source,auth) = 
        match System.Uri.TryCreate(source, System.UriKind.Absolute) with
        | true, uri -> if uri.Scheme = System.Uri.UriSchemeFile then LocalNuget(source) else Nuget({ Url = source; Authentication = auth })
        | _ ->  match System.Uri.TryCreate(source, System.UriKind.Relative) with
                | true, uri -> LocalNuget(source)
                | _ -> failwithf "unable to parse package source: %s" source

    static member NugetSource url = Nuget { Url = url; Authentication = None }

    static member warnIfNoConnection (source,_) = 
        match source with
        | Nuget {Url = url; Authentication = auth} -> 
            use client = Utils.createWebClient(url, auth |> Option.map toBasicAuth)
            try client.DownloadData url |> ignore 
            with _ ->
                traceWarnfn "Unable to ping remote Nuget feed: %s." url
        | LocalNuget path -> 
            if not <| File.Exists path then 
                traceWarnfn "Local Nuget feed doesn't exist: %s." path            

let DefaultNugetSource = PackageSource.NugetSource Constants.DefaultNugetStream