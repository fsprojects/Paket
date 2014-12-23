module Paket.ConfigFile

open System
open System.Xml
open System.Security.Cryptography
open System.Text
open System.IO
open Xml
open Logging

let private rootElement = "configuration"

let private getConfigNode (nodeName : string) =
    let rootNode = 
        if File.Exists Constants.PaketConfigFile then 
            let doc = new XmlDocument()
            doc.Load Constants.PaketConfigFile
            doc.DocumentElement
        else 
            if not (Directory.Exists Constants.PaketConfigFile) then 
                Directory.CreateDirectory Constants.PaketConfigFolder |> ignore

            let doc = new XmlDocument()
            let element = doc.CreateElement rootElement
            doc.AppendChild(element) |> ignore
            doc.Save Constants.PaketConfigFile
            element

    let node = rootNode.SelectSingleNode(sprintf "//%s" nodeName)
    if node <> null then
        node
    else
        rootNode.OwnerDocument.CreateElement nodeName
        |> rootNode.AppendChild


let private saveConfigNode (node : XmlNode) = node.OwnerDocument.Save Constants.PaketConfigFile

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

let getAuthFromNode (node : XmlNode) = 
    let username = node.Attributes.["username"].Value
    let password = node.Attributes.["password"].Value
    let salt = node.Attributes.["salt"].Value

    username, Decrypt salt password
           
let private saveCredentials (source : string) (username : string) (password : string) (credentialsNode : XmlNode) =
    let salt, encrypedPassword = Encrypt password
    let node = credentialsNode.OwnerDocument.CreateElement("credential")
    node.SetAttribute("source", source)
    node.SetAttribute("username", username)
    node.SetAttribute("password", encrypedPassword)
    node.SetAttribute("salt", salt)
    credentialsNode.AppendChild node |> ignore
    saveConfigNode credentialsNode
    node

/// Check if the provided credentials for a specific source are correct
let checkCredentials(source, cred) = 
    let client = Utils.createWebClient cred
    try 
        client.DownloadData(Uri(source)) |> ignore
        true
    with _ -> false

let getSourceNodes (credentialsNode : XmlNode) (source) = 
    credentialsNode.SelectNodes "//credential"
    |> Seq.cast<XmlNode>
    |> Seq.filter (fun n -> n.Attributes.["source"].Value = source)
    |> Seq.toList


/// Get the credential from the credential store for a specific source
let GetCredentials (source : string) =
    let credentialsNode = getConfigNode "credentials"
    
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

let AddCredentials (source, username, password) =
    let credentialsNode = getConfigNode "credentials"
    
    match getSourceNodes credentialsNode source with
    | existingNode::_ ->
        let existingPassword = 
            Decrypt 
                existingNode.Attributes.["salt"].Value
                existingNode.Attributes.["password"].Value

        if existingPassword <> password then
            let salt, encrypted = Encrypt password
            existingNode.Attributes.["username"].Value <- username
            existingNode.Attributes.["password"].Value <- encrypted
            existingNode.Attributes.["salt"].Value <- salt
            saveConfigNode credentialsNode
            
        existingNode
    | [] -> 
        saveCredentials source username password credentialsNode :> XmlNode