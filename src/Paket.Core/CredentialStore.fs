module Paket.CredentialStore

open System
open System.Xml
open System.Security.Cryptography
open System.Text
open System.IO

let credentialStoreDirectory =   
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Paket")
let credentialStoreFile = 
    Path.Combine(credentialStoreDirectory, "credentials.xml")
let entropyBytes = Encoding.UTF8.GetBytes("Paket");

// Encrypts a string with a user specific keys
let encrypt (password : string)= 
    Convert.ToBase64String(
        ProtectedData.Protect(
            Encoding.UTF8.GetBytes(password), 
            entropyBytes,
            DataProtectionScope.CurrentUser))

// decrypt a encrypted string with a user specific keys
let decrypt (encrypted) = 
    Encoding.UTF8.GetString( 
        ProtectedData.Unprotect(
            Convert.FromBase64String(encrypted), 
            entropyBytes, 
            DataProtectionScope.CurrentUser))
            
let getAuthFromNode (node : XmlNode) =
    Some { Username = AuthEntry.Create <| node.Attributes.["username"].Value
           Password = AuthEntry.Create <| decrypt(node.Attributes.["password"].Value)}

let askAndAddAuth (source : string) (doc : XmlDocument)=
    if(System.Environment.UserInteractive) then
        System.Console.Write("Username: ")
        let userName = System.Console.ReadLine()
        System.Console.Write("Password: ")
        // TODO not cleartext
        let password = System.Console.ReadLine()
        let node = doc.CreateElement("credential")
        node.SetAttribute("source", source)
        node.SetAttribute("username", userName)
        node.SetAttribute("password", encrypt(password))
        doc.DocumentElement.AppendChild node |> ignore
        doc.Save credentialStoreFile
        getAuthFromNode (node :> XmlNode)
    else
        failwith  ("No credentials could be found for source " + source)

// check if the provided credentials for a specific source are correct
let checkCredentials source cred = 
    let client = Utils.createWebClient cred
    try
        client.DownloadData(Uri(source)) |> ignore
        true
    with
    | exn -> false

let getSourceNodes (doc: XmlDocument) (source) =
    doc.SelectNodes("/credentials/credential")
        |> Seq.cast<XmlNode>  
        |> Seq.filter (fun n -> n.Attributes.["source"].Value = source)
        |> Seq.toList
    
// get the credential from the creedential store for a specific sourcee
let getFromCredentialStore (source :string)=
    let doc =
        if System.IO.File.Exists credentialStoreFile then
            let doc = new XmlDocument()
            doc.Load credentialStoreFile
            doc
        else
            if not (Directory.Exists credentialStoreFile) then
                Directory.CreateDirectory credentialStoreDirectory |> ignore

            let doc = new XmlDocument()
            let el = doc.CreateElement("credentials")
            doc.AppendChild(el) |> ignore
            doc.Save credentialStoreFile
            doc
    
    let sourceNodes = getSourceNodes doc source
   
    if(sourceNodes.IsEmpty) then     
        askAndAddAuth source doc
    else
        let creds = getAuthFromNode sourceNodes.Head
        if checkCredentials source creds then
            creds
        else 
            doc.DocumentElement.RemoveChild sourceNodes.Head |> ignore
            askAndAddAuth source doc
