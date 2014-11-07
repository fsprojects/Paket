module Paket.ConfigFile

open System
open System.Xml
open System.Security.Cryptography
open System.Text
open System.IO

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

    Some { Username = AuthEntry.Create username
           Password = AuthEntry.Create <| Decrypt salt password}
           
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

let private askAndAddAuth (source : string) (credentialsNode : XmlNode) = 
    if not Environment.UserInteractive then
        failwithf "No credentials could be found for source %s" source

    Console.Write("Username: ")
    let userName = Console.ReadLine()
    let password = readPassword "Password: "
    let node = saveCredentials source userName password credentialsNode
    getAuthFromNode (node :> XmlNode)

/// Check if the provided credentials for a specific source are correct
let checkCredentials source cred = 
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


/// Get the credential from the credential store for a specific sourcee
let GetCredentials (source : string) =
    let credentialsNode = getConfigNode "credentials"
    
    let sourceNodes = getSourceNodes credentialsNode source
    if sourceNodes.IsEmpty then 
        askAndAddAuth source credentialsNode
    else 
        let creds = getAuthFromNode sourceNodes.Head
        if checkCredentials source creds then creds
        else 
            credentialsNode.RemoveChild sourceNodes.Head |> ignore
            askAndAddAuth source credentialsNode
