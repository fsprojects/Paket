module Paket.ConfigFile

open System
open System.Xml
open System.Security.Cryptography
open System.Text
open System.IO
open Pri.LongPath

open Chessie.ErrorHandling
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


let private fillRandomBytes =
    let provider = RandomNumberGenerator.Create()
    (fun (b:byte[]) -> provider.GetBytes(b))

let private getRandomSalt() =
    let saltSize = 8
    let saltBytes = Array.create saltSize ( new Byte() )
    fillRandomBytes(saltBytes)
    saltBytes

/// Encrypts a string with a user specific keys
let Encrypt (password : string) = 
    let salt = getRandomSalt()
    let encryptedPassword = 
        try 
            ProtectedData.Protect(Encoding.UTF8.GetBytes password, salt, DataProtectionScope.CurrentUser)
        with | :? CryptographicException as e ->
            if verbose then
                verbosefn "could not protect password: %s\n for current user" e.Message
            ProtectedData.Protect(Encoding.UTF8.GetBytes password, salt, DataProtectionScope.LocalMachine)
    salt |> Convert.ToBase64String ,
    encryptedPassword |> Convert.ToBase64String

/// Decrypt a encrypted string with a user specific keys
let Decrypt (salt : string) (encrypted : string) =
    ProtectedData.Unprotect(Convert.FromBase64String encrypted, Convert.FromBase64String salt, DataProtectionScope.CurrentUser)
    |> Encoding.UTF8.GetString

let DecryptNuget (encrypted : string) = 
    ProtectedData.Unprotect(Convert.FromBase64String encrypted, Encoding.UTF8.GetBytes "NuGet", DataProtectionScope.CurrentUser)
    |> Encoding.UTF8.GetString

let private readPassword (message : string) : string = 
    Console.Write message
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
        else Console.Write "\r"
    password

let getAuthFromNode (node : XmlNode) = 
    match node.Name.ToLowerInvariant() with
    | "credential" ->
        let username = node.Attributes.["username"].Value
        let password = node.Attributes.["password"].Value
        let salt = node.Attributes.["salt"].Value
        Credentials (username, Decrypt salt password)
    | "token" -> Token node.Attributes.["value"].Value
    | _ -> failwith "unknown node"

let private createSourceNode (credentialsNode : XmlNode) source nodeName =
    let node = credentialsNode.OwnerDocument.CreateElement nodeName
    node.SetAttribute ("source", source)
    credentialsNode.AppendChild node |> ignore
    node

let private setCredentials (username : string) (password : string) (node : XmlElement) =
    let salt, encrypedPassword = Encrypt password
    node.SetAttribute ("username", username)
    node.SetAttribute ("password", encrypedPassword)
    node.SetAttribute ("salt", salt)
    node

let private setToken (token : string) (node : XmlElement) =
    node.SetAttribute ("value", token)
    node

/// Check if the provided credentials for a specific source are correct
let checkCredentials(url, cred) = 
    let client = Utils.createHttpClient(url,cred)
    try 
        client.DownloadData (Uri url) |> ignore
        true
    with _ ->
        try
            let folderUrl = sprintf "%s/" url
            client.DownloadData (Uri folderUrl) |> ignore
            true
        with _ -> false

let getSourceNodes (credentialsNode : XmlNode) source nodeType = 
    sprintf "//%s" nodeType |> credentialsNode.SelectNodes
    |> Seq.cast<XmlElement>
    |> Seq.filter (fun n -> n.Attributes.["source"].Value = source)
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
    | sourceNode :: _ ->
        let auth = getAuthFromNode sourceNode
        if checkCredentials (url, Some auth) then 
            Some auth
        else 
            failwithf "Credentials from authentication store for %s are invalid" source
            None
    | _ -> None)

/// Get the authentication from the authentication store for a specific source
let GetAuthentication (source : string) =
    GetAuthenticationForUrl(source,source)

let AddCredentials (source, username, password) = 
    trial { 
        let! credentialsNode = getConfigNode "credentials"
        let newCredentials = 
            match getSourceNodes credentialsNode source "credential" |> List.tryHead with
            | None -> createSourceNode credentialsNode source "credential" |> Some
            | Some existingNode -> 
                match getAuthFromNode existingNode with
                | Credentials (_, existingPassword) ->
                    if existingPassword <> password then existingNode |> Some
                    else None
                | _ -> None
            |> Option.map (setCredentials username password)
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

let askAndAddAuth (source : string) (username : string) (password : string) = 
    let username =
        if username = "" then
            Console.Write "Username: "
            Console.ReadLine()
        else 
            username

    let password = 
        if password = "" then
            readPassword "Password: "
        else
            password
    AddCredentials (source.TrimEnd [|'/'|], username, password)
