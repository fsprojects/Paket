module Paket.PackageSources 

open System
open System.IO
open System.Text.RegularExpressions

open Paket.Logging
open Chessie.ErrorHandling

open Newtonsoft.Json

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

[<StructuredFormatDisplay("{AsString}")>]
type NugetSourceAuthentication = 
    | PlainTextAuthentication of username : string * password : string
    | EnvVarAuthentication of usernameVar : EnvironmentVariable * passwordVar : EnvironmentVariable
    | ConfigAuthentication of username : string * password : string 
        with
            override x.ToString() =
                match x with
                    | PlainTextAuthentication(u,_) -> sprintf "PlainTextAuthentication (username = %s, password = ***)" u
                    | EnvVarAuthentication(u,_) ->  sprintf "EnvVarAuthentication (usernameVar = %s, passwordVar = ***)" u.Variable
                    | ConfigAuthentication(u,_) -> sprintf "ConfigAuthentication (username = %s, password = ***)" u
            member x.AsString = x.ToString()

let toBasicAuth = function
    | PlainTextAuthentication(username,password) | ConfigAuthentication(username, password) ->
        Credentials(username, password)
    | EnvVarAuthentication(usernameVar, passwordVar) -> 
        Credentials(usernameVar.Value, passwordVar.Value)

let tryParseWindowsStyleNetworkPath (path : string) =
    let trimmed = path.TrimStart()
    if (isUnix || isMacOS) && trimmed.StartsWith @"\\" then
        trimmed.Replace('\\', '/') |> sprintf "smb:%s" |> Some
    else None

let RemoveOutsideQuotes(path : string) =
    let trimChars = [|'\"'|]
    path.Trim(trimChars)

type NugetSource = 
    { Url : string
      Authentication : NugetSourceAuthentication option }

type NugetV3SourceResourceJSON = 
    { [<JsonProperty("@type")>]
      Type : string
      [<JsonProperty("@id")>]
      ID : string }

type NugetV3SourceRootJSON = 
    { [<JsonProperty("resources")>]
      Resources : NugetV3SourceResourceJSON [] }

type NugetV3Source = 
    { Url : string
      Authentication : NugetSourceAuthentication option }

type NugetV3ResourceType = 
    | AutoComplete
    | AllVersionsAPI
    | Registration

    member this.AsString = 
        match this with
        | AutoComplete -> "SearchAutoCompleteService"
        | Registration -> "RegistrationsBaseUrl"
        | AllVersionsAPI -> "PackageBaseAddress/3.0.0"

let private nugetV3Resources = ref Map.empty 
        
let getNuGetV3Resource (source : NugetV3Source) (resourceType : NugetV3ResourceType) =
    async {
        let key = (source, resourceType)
        match !nugetV3Resources |> Map.tryFind key with
        | Some x -> return x
        | None -> 
            let basicAuth = source.Authentication |> Option.map toBasicAuth
            let! rawData = safeGetFromUrl(basicAuth, source.Url, acceptJson)
            let rawData =
                match rawData with
                | FSharp.Core.Result.Error e ->
                    raise <| new Exception(sprintf "Could not load resources from '%s'" source.Url, e.SourceException)
                | FSharp.Core.Result.Ok x -> x

            let json = JsonConvert.DeserializeObject<NugetV3SourceRootJSON>(rawData)
            let resources = 
                json.Resources 
                |> Seq.distinctBy(fun x -> x.Type.ToLower())
                |> Seq.map(fun x -> x.Type.ToLower(), x.ID) 

            let newMap = 
                lock !nugetV3Resources (fun() ->
                    let newMap =
                        resources
                        |> Seq.fold(fun m (res, value) ->
                            let resType =
                                match res.ToLower() with
                                | "searchautocompleteservice" -> Some AutoComplete
                                | "registrationsbaseurl" -> Some Registration
                                | "packagebaseaddress/3.0.0" -> Some AllVersionsAPI
                                | _ -> None
                            match resType with
                            | None -> m
                            | Some resType ->
                                m |> Map.add (source, resType) value
                        ) !nugetV3Resources
                            
                    nugetV3Resources := newMap
                        
                    newMap)

            return
                match newMap |> Map.tryFind key with
                | Some x -> x
                | None ->
                    failwithf "could not find an %s endpoint for %s" (resourceType.ToString()) source.Url
    }
let userNameRegex = Regex("username[:][ ]*[\"]([^\"]*)[\"]", RegexOptions.IgnoreCase ||| RegexOptions.Compiled)
let passwordRegex = Regex("password[:][ ]*[\"]([^\"]*)[\"]", RegexOptions.IgnoreCase ||| RegexOptions.Compiled)

let internal parseAuth(text:string, source) =
    let getAuth() = ConfigFile.GetAuthentication source |> Option.map (function Credentials(username, password) -> ConfigAuthentication(username, password) | _ -> ConfigAuthentication("",""))
    if text.Contains("username:") || text.Contains("password:") then
        if not (userNameRegex.IsMatch(text) && passwordRegex.IsMatch(text)) then 
            failwithf "Could not parse auth in \"%s\"" text

        let username = userNameRegex.Match(text).Groups.[1].Value
        let password = passwordRegex.Match(text).Groups.[1].Value

        let auth = 
            match EnvironmentVariable.Create(username),
                  EnvironmentVariable.Create(password) with
            | Some userNameVar, Some passwordVar ->
                EnvVarAuthentication(userNameVar, passwordVar) 
            | _, _ -> 
                PlainTextAuthentication(username, password)

        match toBasicAuth auth with
        | Credentials(username, password) when username = "" && password = "" -> getAuth()
        | _ -> Some auth
    else
        getAuth()

/// Represents the package source type.
type PackageSource =
| NuGetV2 of NugetSource
| NuGetV3 of NugetV3Source
| LocalNuGet of string * Cache option
    override this.ToString() =
        match this with
        | NuGetV2 source -> source.Url
        | NuGetV3 source -> source.Url
        | LocalNuGet(path,_) -> path

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
                    if String.endsWithIgnoreCase "v3/index.json" source then
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
        | LocalNuGet(n,_) -> None

    static member NuGetV2Source url = NuGetV2 { Url = url; Authentication = None }
    static member NuGetV3Source url = NuGetV3 { Url = url; Authentication = None }

    static member FromCache (cache:Cache) = LocalNuGet(cache.Location,Some cache)

    static member WarnIfNoConnection (source,_) = 
        let n url auth =
            use client = Utils.createWebClient(url, auth |> Option.map toBasicAuth)
            try client.DownloadData url |> ignore 
            with _ ->
                traceWarnfn "Unable to ping remote NuGet feed: %s." url
        match source with
        | NuGetV2 x -> n x.Url x.Authentication
        | NuGetV3 x -> n x.Url x.Authentication
        | LocalNuGet(path,_) -> 
            if not <| Directory.Exists (RemoveOutsideQuotes path) then 
                traceWarnfn "Local NuGet feed doesn't exist: %s." path

let DefaultNuGetSource = PackageSource.NuGetV2Source Constants.DefaultNuGetStream


type NugetPackage = {
    Id : string
    VersionRange : VersionRange
    TargetFramework : string option
}
