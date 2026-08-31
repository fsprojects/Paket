module Paket.ConfigFile

open System
open System.Xml
open System.Security.Cryptography
open System.Text
open System.IO

open Chessie.ErrorHandling
open Paket.Core.Common
open Paket.Domain
open Paket.Xml
open Paket.Logging
open Paket.Utils

let private rootElement = "configuration"

let private getConfigNode (nodeName : string) =
    let rootNode = 
        let doc = XmlDocument ()
        if File.Exists Constants.PaketConfigFile then 
            try 
                use f = File.OpenRead(Constants.PaketConfigFile)
                doc.Load f
                ok doc.DocumentElement
            with _ -> fail ConfigFileParseError
        else
            let element = doc.CreateElement rootElement
            doc.AppendChild element |> ignore
            ok element

    trial {
        let! root = rootNode
        let node = 
            match root |> getNode nodeName with
            | None -> root.OwnerDocument.CreateElement nodeName
                      |> root.AppendChild
            | Some node -> node
        return node
    }

let private saveConfigNode (node : XmlNode) =
    trial {
        do! createDir Constants.PaketConfigFolder
        do! saveNormalizedXml Constants.PaketConfigFile node.OwnerDocument
    }

let DecryptNuget (encrypted : string) = 
    ProtectedData.Unprotect(Convert.FromBase64String encrypted, Encoding.UTF8.GetBytes "NuGet", DataProtectionScope.CurrentUser)
    |> Encoding.UTF8.GetString

let private readPassword (message : string) : string = 
    Console.Write message
    // Console.ReadKey requires an interactive console. When input is redirected
    // (e.g. piped input, or some shells like Git Bash on Windows), it throws
    // InvalidOperationException. Fall back to a plain ReadLine in that case.
    if Console.IsInputRedirected then
        Console.ReadLine()
    else
    let mutable continueLooping = true
    let mutable password = ""
    while continueLooping do
        let key = Console.ReadKey true
        continueLooping <- key.Key <> ConsoleKey.Enter
        if continueLooping then 
            password <- 
                if key.Key <> ConsoleKey.Backspace then 
                    Console.Write "*"
                    password + key.KeyChar.ToString()
                else if password.Length > 0 then 
                    Console.Write "\b \b"
                    password.Substring(0, (password.Length - 1))
                else ""
        else Console.WriteLine()
    password

let getAuthFromNode (node : XmlNode) = 
    match node.Name.ToLowerInvariant() with
    | "credential" ->
        let username = node.Attributes.["username"].Value
        let password = node.Attributes.["password"].Value
        let authType =                         
            match node.Attributes.["authType"] with
            | null -> AuthType.Basic
            | n -> n.Value |> NetUtils.parseAuthTypeString

        let salt = node.Attributes.["salt"].Value
        let (PlainTextPassword password) = Crypto.decrypt password salt
        Credentials {Username = username; Password = password; Type = authType}
    | "token" -> Token node.Attributes.["value"].Value
    | _ -> failwith "unknown node"

let private createSourceNode (credentialsNode : XmlNode) source nodeName =
    let node = credentialsNode.OwnerDocument.CreateElement nodeName
    node.SetAttribute ("source", source)
    credentialsNode.AppendChild node |> ignore
    node

let private setCredentials (username : string) (password : string) (authType : string) (node : XmlElement) =
    let encryptedPassword, salt  = Crypto.encrypt (PlainTextPassword password)
    node.SetAttribute ("username", username)
    node.SetAttribute ("password", encryptedPassword.ToString())
    node.SetAttribute ("authType", authType)
    node.SetAttribute ("salt", salt.ToString())
    node

let private setToken (token : string) (node : XmlElement) =
    node.SetAttribute ("value", token)
    node

/// Check if the provided credentials for a specific source are correct
let checkCredentials(url, cred) = 
    let client = NetUtils.createHttpClient(url,cred)
    try 
        client.DownloadData (Uri url) |> ignore
        true
    with _ ->
        try
            let folderUrl = sprintf "%s/" url
            client.DownloadData (Uri folderUrl) |> ignore
            true
        with _ -> false

/// Normalizes a source URL for credential-lookup comparisons only (does not change what URL is
/// actually used for requests). Strips a trailing slash and a "www." host prefix so that e.g.
/// credentials stored for "https://www.nuget.org" are found when looking up "https://nuget.org"
/// and vice versa (see https://github.com/fsprojects/Paket/issues/3843).
let private normalizeSourceForComparison (source : string) =
    let source = source.TrimEnd([|'/'|])
    let wwwPrefix = "://www."
    let idx = source.IndexOf(wwwPrefix, StringComparison.OrdinalIgnoreCase)
    if idx >= 0 then
        source.Remove(idx + 3, 4) // remove "www." while keeping the leading "://"
    else
        source

let getSourceNodes (credentialsNode : XmlNode) (source : string) nodeType = 
    let source = normalizeSourceForComparison source
    sprintf "//%s" nodeType |> credentialsNode.SelectNodes
    |> Seq.cast<XmlElement>
    |> Seq.filter (fun n -> normalizeSourceForComparison n.Attributes.["source"].Value = source)
    |> Seq.toList

let private getCredentialsNode = lazy(getConfigNode "credentials" |> returnOrFail)

/// Get the authentication from the authentication store for a specific source and validates against the url
let GetAuthenticationForUrl =
    memoize (fun (source : string, url) ->
    let sourceNodes =
        if File.Exists Constants.PaketConfigFile |> not then [] else
        let credentialsNode = getCredentialsNode.Force()
        getSourceNodes credentialsNode source "credential" @ getSourceNodes credentialsNode source "token"

    match sourceNodes with
    | sourceNode :: _ -> Some (getAuthFromNode sourceNode)
    | _ -> None)

/// Get the authentication from the authentication store for a specific source
let GetAuthenticationProvider (source : string) =
    AuthProvider.ofFunction (fun _ -> GetAuthenticationForUrl(source,source))

let AddCredentials (source, username, password, authType) = 
    trial { 
        let! credentialsNode = getConfigNode "credentials"
        let newCredentials = 
            match getSourceNodes credentialsNode source "credential" |> List.tryHead with
            | None -> createSourceNode credentialsNode source "credential" |> Some
            | Some existingNode -> 
                match getAuthFromNode existingNode with
                | Credentials {Password = existingPassword} ->
                    if existingPassword <> password then existingNode |> Some
                    else None
                | _ -> None
            |> Option.map (setCredentials username password authType)
        match newCredentials with
        | Some credentials -> do! saveConfigNode credentials
        | None -> ()
    }

let AddToken (source, token) =
    trial {
        let! credentialsNode = getConfigNode "credentials"
        let newToken = 
            match getSourceNodes credentialsNode source "token" |> List.tryHead with
            | None -> createSourceNode credentialsNode source "token" |> Some
            | Some existingNode ->
                match getAuthFromNode existingNode with
                | Token existingToken ->
                    if existingToken <> token then existingNode |> Some
                    else None
                | _ -> None
            |> Option.map (setToken token)
        match newToken with
        | Some token -> do! saveConfigNode token
        | None -> () 
    }

let askAndAddAuth (source : string) (passedUserName : string) (passedPassword : string) (authType : string) (verify : bool) = 
    let username =
        if passedUserName = "" then
            Console.Write "Username: "
            Console.ReadLine()
        else 
            passedUserName

    let password = 
        if passedPassword = "" then
            readPassword "Password: "
        else
            passedPassword

    let authType =
        if authType = "" then
            if passedUserName <> "" && passedPassword <> "" then 
                "basic" 
            else
                Console.Write "Authentication type (basic|ntlm, default = basic): "
                let input = Console.ReadLine().Trim()
                if input = "" then "basic" else input
        else
            authType

    let authResult = 
        if verify then 
            tracef "Verifying the source URL and credentials...\n"
            let cred = Credentials({Username = username; Password = password; Type = parseAuthTypeString authType})
            checkCredentials(source, Some cred) 
        else 
            true
    if authResult = false then 
        raise (System.UnauthorizedAccessException("Credentials or the URL for source " + source + " are invalid"))

    AddCredentials (source.TrimEnd [|'/'|], username, password, authType)