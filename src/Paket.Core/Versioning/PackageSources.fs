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

type NugetProtocolVersion =
    | ProtocolVersion2
    | ProtocolVersion3

[<CustomComparison;CustomEquality>]
type NuGetSource =
    { Url : string
      ProtocolVersion: NugetProtocolVersion
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

let userNameRegex = Regex("username[:][ ]*[\"]([^\"]*)[\"]", RegexOptions.IgnoreCase ||| RegexOptions.Compiled)
let passwordRegex = Regex("password[:][ ]*[\"]([^\"]*)[\"]", RegexOptions.IgnoreCase ||| RegexOptions.Compiled)
let authTypeRegex = Regex("authtype[:][ ]*[\"]([^\"]*)[\"]", RegexOptions.IgnoreCase ||| RegexOptions.Compiled)
let protocolVersionRegex = Regex("protocolVersion[:][ ]*(\d)", RegexOptions.IgnoreCase ||| RegexOptions.Compiled)

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
            match EnvironmentVariable.Create(username),
                    EnvironmentVariable.Create(password) with
            | Some userNameVar, Some passwordVar ->
               {Username = userNameVar.Value; Password = passwordVar.Value; Type = authType }
            | _, _ ->
               {Username = username; Password = password; Type = authType }

        match auth with
        | {Username = username; Password = password} when username = "" && password = "" -> getAuth()
        | _ -> //Some auth
            AuthProvider.ofFunction (fun _ -> Some (Auth.Credentials auth))
    else
        getAuth()

let (|NugetV3Url|_|) (url: Uri) =
  if url.ToString() = "https://api.nuget.org/v3/index.json" then Some () else None

type 't ``[]`` with
  member x.GetReverseIndex(i: int) = x.[x.Length - i]

let (|Https|_|) (uri: Uri) = if uri.Scheme = "https" then Some () else None
let (|Host|_|) (h: string) (uri: Uri) =
    if uri.Host.EndsWith h
    then Some ()
    else None

let (|LeadingPathSegments|_|) (segs: string[]) (uri: Uri) =
    let segCount = segs.Length
    let uriSegs = uri.Segments
    if uriSegs.Length < segCount+1 then None // initial segment is a /, so we need to skip it
    else
        let items: string[] = uriSegs.[1..segCount]
        let matched =
            (items, segs)
            ||> Array.zip
            |> Array.forall(fun (l, r) -> l.TrimEnd('/') = r) // have to trim end because the Uri.Segments api keeps the /-separators on the end of the segment
        if matched then Some () else None

let (|TrailingPathSegments|_|) (segs: string []) (uri: Uri) =
    let segCount = segs.Length
    let uriSegs = uri.Segments
    if uriSegs.Length < segCount then None
    else
        let items: string[] = uriSegs.[(uriSegs.Length-segCount)..]
        let matched =
            (items, segs)
            ||> Array.zip
            |> Array.forall(fun (l, r) -> l.TrimEnd('/') = r) // have to trim end because the Uri.Segments api keeps the /-separators on the end of the segment
        if matched then Some () else None

let (|MyGetV3Url|_|) (url: Uri) =

  // https://<your_myget_domain>/F/<your-feed-name>/<feed_endpoint>
  try
      match url with
      | Https & Host "myget.org" & TrailingPathSegments [|"api";"v3"; "index.json"|] -> Some ()
      | _ -> None
  with _ -> None

let (|ArtifactoryV3Url|_|) (url: Uri) =
    match url with
    | LeadingPathSegments [|"artifactory"; "api";"nuget";"v3"|] -> Some ()
    | _ -> None

let (|KnownNugetV3Endpoint|_|) url =
    match Uri url with
    | NugetV3Url | MyGetV3Url | ArtifactoryV3Url -> Some ()
    | _ -> None

let internal parseProtocolVersion(text:string, nugetSource) =
    if text.Contains("protocolVersion:") then
        if not (protocolVersionRegex.IsMatch(text)) then
            failwithf "Could not parse protocolVersion in \"%s\"" text

        let textProtocolVersion = protocolVersionRegex.Match(text).Groups.[1].Value
        let (parsed, specifiedProtocolVersion) = Int32.TryParse(textProtocolVersion)

        match (parsed,specifiedProtocolVersion) with
        | (true, 2) -> Some ProtocolVersion2
        | (true, 3) -> Some ProtocolVersion3
        | _ -> failwithf "Unsupported protocolVersion in \"%s\". Should be either 2 or 3" text
    else
        // derive protocolVersion from some well-known urls
        try
            match nugetSource with
            | KnownNugetV3Endpoint -> Some ProtocolVersion3
            | _ -> None
        with
        | _ -> None

/// Represents the package source type.
type PackageSource =
| NuGet of NuGetSource
| LocalNuGet of string * Cache option
    override this.ToString() =
        match this with
        | NuGet source -> source.Url
        | LocalNuGet(path,_) -> path
    member x.NuGetType =
        match x.Url with
        | _ when urlIsNugetGallery x.Url -> KnownNuGetSources.OfficialNuGetGallery
        | _ when urlIsMyGet x.Url -> KnownNuGetSources.MyGet
        | _ when urlSimilarToTfsOrVsts x.Url -> KnownNuGetSources.TfsOrVsts
        | _ -> KnownNuGetSources.UnknownNuGetServer
    static member Parse(line : string) =
        let sourceRegex = Regex("source[ ]*[\"]([^\"]*)[\"]([ ]+.+)?", RegexOptions.IgnoreCase)
        let parts = line.Split ' '
        let source =
            if sourceRegex.IsMatch line then
                sourceRegex.Match(line).Groups.[1].Value.TrimEnd([| '/' |])
            else
                parts.[1].Replace("\"","").TrimEnd([| '/' |])

        let feed = normalizeFeedUrl source

        PackageSource.Parse(feed, parseProtocolVersion(line, feed), parseAuth(line, feed))

    static member Parse(source, protocolVersion, auth) =
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
                    match protocolVersion with
                    | Some p -> NuGet { Url = source; ProtocolVersion = p; Authentication = auth }
                    | None -> NuGet { Url = source; ProtocolVersion = ProtocolVersion2; Authentication = auth }
            | _ ->  match System.Uri.TryCreate(source, System.UriKind.Relative) with
                    | true, uri -> LocalNuGet(source,None)
                    | _ -> failwithf "unable to parse package source: %s" source

    member this.Url =
        match this with
        | NuGet n -> n.Url
        | LocalNuGet(n,_) -> n

    member this.IsLocalFeed =
        match this with
        | LocalNuGet(n,_) -> true
        | _ -> false

    member this.Auth =
        match this with
        | NuGet n -> n.Authentication
        | LocalNuGet(n,_) -> CredentialProviders.GetAuthenticationProvider n

    static member NuGetV2Source url = NuGet { Url = url; ProtocolVersion = ProtocolVersion2; Authentication  = CredentialProviders.GetAuthenticationProvider url }
    static member NuGetV3Source url = NuGet { Url = url; ProtocolVersion = ProtocolVersion3; Authentication  = CredentialProviders.GetAuthenticationProvider url }

    static member FromCache (cache:Cache) = LocalNuGet(cache.Location,Some cache)

    static member WarnIfNoConnection (source,_) =
        let n url (auth:AuthProvider) =
            let client = NetUtils.createHttpClient(url, auth.Retrieve true)
            try
                client.DownloadData url |> ignore
            with _ ->
                traceWarnfn "Unable to ping remote NuGet feed: %s." url
        match source with
        | NuGet x -> n x.Url x.Authentication
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
