module Paket.PackageSources

open System
open System.IO
open System.Text.RegularExpressions
open Paket.Logging

let private envVarRegex = Regex("^%(\w*)%$", RegexOptions.Compiled)

type EnvironmentVariable =
    { Variable : string
      Value    : string }

    static member Create(variable) =
        if envVarRegex.IsMatch(variable) then
            let trimmed = envVarRegex.Match(variable).Groups.[1].Value
            match Environment.GetEnvironmentVariable(trimmed) with
            | null ->
                traceWarnfn "environment variable '%s' not found" variable
                Some { Variable = variable; Value = ""}
            | expanded ->
                Some { Variable = variable; Value = expanded }
        else
            None

let tryParseWindowsStyleNetworkPath (path : string) =
    let trimmed = path.TrimStart()
    if (isUnix || isMacOS) && trimmed.StartsWith @"\\" then
        trimmed.Replace('\\', '/') |> sprintf "smb:%s" |> Some
    else None

let RemoveOutsideQuotes(path : string) =
    let trimChars = [|'\"'|]
    path.Trim(trimChars)

let urlSimilarToTfsOrVsts url =
    String.containsIgnoreCase "visualstudio.com" url || (String.containsIgnoreCase "/_packaging/" url && String.containsIgnoreCase "/nuget/v" url)

let urlIsNugetGallery url =
    String.containsIgnoreCase "nuget.org" url

let urlIsMyGet url =
    String.containsIgnoreCase "myget.org" url

type KnownNuGetSources =
    | OfficialNuGetGallery
    | TfsOrVsts
    | MyGet
    | UnknownNuGetServer

[<CustomComparison;CustomEquality>]
type NuGetSource =
    { Url : string
      Authentication : AuthProvider }
    member x.BasicAuth isRetry =
        x.Authentication.Retrieve isRetry
    override x.Equals(yobj) =
        match yobj with
        | :? NuGetSource as y -> (x.Url = y.Url)

        | _ -> false
    override x.GetHashCode() = hash x.Url
    interface System.IComparable with
      member x.CompareTo yobj =
          match yobj with
          | :? NuGetSource as y -> compare x.Url y.Url
          | _ -> invalidArg "yobj" "cannot compare values of different types"

type NuGetV3Source = NuGetSource

let userNameRegex = Regex("username[:][ ]*[\"]([^\"]*)[\"]", RegexOptions.IgnoreCase ||| RegexOptions.Compiled)
let passwordRegex = Regex("password[:][ ]*[\"]([^\"]*)[\"]", RegexOptions.IgnoreCase ||| RegexOptions.Compiled)
let authTypeRegex = Regex("authtype[:][ ]*[\"]([^\"]*)[\"]", RegexOptions.IgnoreCase ||| RegexOptions.Compiled)

let internal parseAuth(text:string, source) =
    let getAuth() =
        AuthService.GetGlobalAuthenticationProvider source
        //|> Option.map (function Credentials userPass -> ConfigAuthentication userPass | _ -> ConfigAuthentication{ Username = ""; Password = ""; Type = AuthType.Basic})

    if text.Contains("username:") || text.Contains("password:") then
        if not (userNameRegex.IsMatch(text) && passwordRegex.IsMatch(text)) then
            failwithf "Could not parse auth in \"%s\"" text

        let username = userNameRegex.Match(text).Groups.[1].Value
        let password = passwordRegex.Match(text).Groups.[1].Value

        let authType =
            if (authTypeRegex.IsMatch(text))
            then authTypeRegex.Match(text).Groups.[1].Value |> NetUtils.parseAuthTypeString
            else NetUtils.AuthType.Basic

        let auth =
            { Username =
                  EnvironmentVariable.Create(username)
                  |> Option.map (fun var -> var.Value)
                  |> Option.defaultValue username
              Password =
                  EnvironmentVariable.Create(password)
                  |> Option.map (fun var -> var.Value)
                  |> Option.defaultValue password
              Type = authType }

        match auth with
        | {Username = username; Password = password} when username = "" && password = "" -> getAuth()
        | _ -> //Some auth
            AuthProvider.ofFunction (fun _ -> Some (Auth.Credentials auth))
    else
        getAuth()

/// Represents the package source type.
type PackageSource =
| NuGetV2 of NuGetSource
| NuGetV3 of NuGetV3Source
| LocalNuGet of string * Cache option
    override this.ToString() =
        match this with
        | NuGetV2 source -> source.Url
        | NuGetV3 source -> source.Url
        | LocalNuGet(path,_) -> path
    member x.NuGetType =
        match x.Url with
        | _ when urlIsNugetGallery x.Url -> KnownNuGetSources.OfficialNuGetGallery
        | _ when urlIsMyGet x.Url -> KnownNuGetSources.MyGet
        | _ when urlSimilarToTfsOrVsts x.Url -> KnownNuGetSources.TfsOrVsts
        | _ -> KnownNuGetSources.UnknownNuGetServer
    static member Parse(line : string) =
        let sourceRegex = Regex("source[ ]*[\"]([^\"]*)[\"]", RegexOptions.IgnoreCase)
        let parts = line.Split ' '
        let source =
            if sourceRegex.IsMatch line then
                sourceRegex.Match(line).Groups.[1].Value.TrimEnd([| '/' |])
            else
                parts.[1].Replace("\"","").TrimEnd([| '/' |])

        let feed = normalizeFeedUrl source
        PackageSource.Parse(feed, parseAuth(line, feed))

    static member Parse(source,auth) =
        match tryParseWindowsStyleNetworkPath source with
        | Some path -> PackageSource.Parse(path)
        | _ ->
            match System.Uri.TryCreate(source, System.UriKind.Absolute) with
            | true, uri ->
#if DOTNETCORE
                if uri.Scheme = "file" then
#else
                if uri.Scheme = System.Uri.UriSchemeFile then
#endif
                    LocalNuGet(source,None)
                else
                    if source.Contains("/v3/") || source.EndsWith("index.json") then
                        NuGetV3 { Url = source; Authentication = auth }
                    else
                        NuGetV2 { Url = source; Authentication = auth }

            | _ ->  match System.Uri.TryCreate(source, System.UriKind.Relative) with
                    | true, uri -> LocalNuGet(source,None)
                    | _ -> failwithf "unable to parse package source: %s" source

    member this.Url =
        match this with
        | NuGetV2 n -> n.Url
        | NuGetV3 n -> n.Url
        | LocalNuGet(n,_) -> n

    member this.IsLocalFeed =
        match this with
        | LocalNuGet(n,_) -> true
        | _ -> false

    member this.Auth =
        match this with
        | NuGetV2 n -> n.Authentication
        | NuGetV3 n -> n.Authentication
        | LocalNuGet(n,_) -> CredentialProviders.GetAuthenticationProvider n

    static member NuGetV2Source url = NuGetV2 { Url = url; Authentication = CredentialProviders.GetAuthenticationProvider url }
    static member NuGetV3Source url = NuGetV3 { Url = url; Authentication = CredentialProviders.GetAuthenticationProvider url }

    static member FromCache (cache:Cache) = LocalNuGet(cache.Location,Some cache)

    static member WarnIfNoConnection (source,_) =
        let n url (auth:AuthProvider) =
            let client = NetUtils.createHttpClient(url, auth.Retrieve true)
            try
                client.DownloadData url |> ignore
            with _ ->
                traceWarnfn "Unable to ping remote NuGet feed: %s." url
        match source with
        | NuGetV2 x -> n x.Url x.Authentication
        | NuGetV3 x -> n x.Url x.Authentication
        | LocalNuGet(path,_) ->
            if not (Directory.Exists (RemoveOutsideQuotes path)) then
                traceWarnfn "Local NuGet feed doesn't exist: %s." path

let DefaultNuGetSource = PackageSource.NuGetV2Source Constants.DefaultNuGetStream
let DefaultNuGetV3Source = PackageSource.NuGetV3Source Constants.DefaultNuGetV3Stream

type NugetPackage = {
    Id : string
    VersionRequirement : VersionRequirement
    Kind : NugetPackageKind
    TargetFramework : string option
}
and [<RequireQualifiedAccess>] NugetPackageKind =
    | Package
    | DotnetCliTool
