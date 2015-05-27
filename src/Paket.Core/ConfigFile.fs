module Paket.ConfigFile

open System
open System.Xml
open System.Security.Cryptography
open System.Text
open System.IO

open Chessie.ErrorHandling
open Paket.Domain
open Paket.Xml
open Paket.Logging
open Paket.Utils

let private rootElement = "configuration"

let private getConfigNode (nodeName : string) =
    let rootNode = 
        let doc = new XmlDocument()
        if File.Exists Constants.PaketConfigFile then 
            try 
                doc.Load Constants.PaketConfigFile
                ok doc.DocumentElement
            with _ -> fail ConfigFileParseError
        else
            let element = doc.CreateElement rootElement
            doc.AppendChild(element) |> ignore
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
        do! saveFile Constants.PaketConfigFile (node.OwnerDocument.OuterXml)
    }

let private cryptoServiceProvider = new RNGCryptoServiceProvider()

let private getRandomSalt() =
    let saltSize = 8
    let saltBytes = Array.create saltSize ( new Byte() )
    cryptoServiceProvider.GetNonZeroBytes(saltBytes)
    saltBytes

/// Encrypts a string with a user specific keys
let Encrypt (password : string) = 
    let salt = getRandomSalt()
    let encryptedPassword = ProtectedData.Protect(Encoding.UTF8.GetBytes password, salt, DataProtectionScope.CurrentUser)
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
    Console.Write(message)
    let mutable continueLooping = true
    let mutable password = ""
    while continueLooping do
        let key = Console.ReadKey(true)
        continueLooping <- key.Key <> ConsoleKey.Enter
        if continueLooping then 
            password <- 
                if key.Key <> ConsoleKey.Backspace then 
                    Console.Write("*")
                    password + key.KeyChar.ToString()
                else if password.Length > 0 then 
                    Console.Write("\b \b")
                    password.Substring(0, (password.Length - 1))
                else ""
        else Console.Write("\r")
    password

let getAuthFromNode (node : XmlNode) = 
    let username = node.Attributes.["username"].Value
    let password = node.Attributes.["password"].Value
    let salt = node.Attributes.["salt"].Value

    username, Decrypt salt password

let private createSourceNode (credentialsNode : XmlNode) source =
    let node = credentialsNode.OwnerDocument.CreateElement("credential")
    node.SetAttribute("source", source)
    credentialsNode.AppendChild node |> ignore
    node

let private setCredentials (username : string) (password : string) (node : XmlElement) =
    let salt, encrypedPassword = Encrypt password
    node.SetAttribute("username", username)
    node.SetAttribute("password", encrypedPassword)
    node.SetAttribute("salt", salt)
    node


/// Check if the provided credentials for a specific source are correct
let checkCredentials(url, cred) = 
    let client = Utils.createWebClient(url,cred)
    try 
        client.DownloadData(Uri(url)) |> ignore
        true
    with _ -> false

let getSourceNodes (credentialsNode : XmlNode) (source) = 
    credentialsNode.SelectNodes "//credential"
    |> Seq.cast<XmlElement>
    |> Seq.filter (fun n -> n.Attributes.["source"].Value = source)
    |> Seq.toList


/// Get the credential from the credential store for a specific source
let GetCredentials (source : string) =
    let credentialsNode = getConfigNode "credentials" |> returnOrFail
    
    match getSourceNodes credentialsNode source with
    | sourceNode::_ ->
        let username,password = getAuthFromNode sourceNode
        let auth = {Username = username; Password = password}
        if checkCredentials(source, Some(auth)) then
            Some(username,password)
        else
            traceWarnfn "credentials for %s source are invalid" source  
            None
    | [] -> None

let AddCredentials (source, username, password) = trial {
        let! credentialsNode = getConfigNode "credentials"
        
        let newCredentials = 
            match getSourceNodes credentialsNode source |> Seq.firstOrDefault with
            | None -> createSourceNode credentialsNode source |> Some
            | Some existingNode ->
                let _,existingPassword = getAuthFromNode existingNode

                if existingPassword <> password then
                    existingNode |> Some
                else None
            |> Option.map (setCredentials username password)

        match newCredentials with
        | Some credentials -> 
            do! saveConfigNode credentials
        | None -> ()
    }

let askAndAddAuth (source : string) (username : string) = 
    let username =
        if(username = "") then
            Console.Write("Username: ")
            Console.ReadLine()
        else 
            username

    let password = readPassword "Password: "
    AddCredentials (source, username, password)