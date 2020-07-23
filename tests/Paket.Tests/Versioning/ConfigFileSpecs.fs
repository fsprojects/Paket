module Paket.ConfigFileSpecs

open Paket
open Paket.Core.Common
open Paket.ConfigFile
open NUnit.Framework
open System.Xml
open FsUnit

#if TESTSUITE_RUNS_ON_DOTNETCORE
open System.Runtime.InteropServices
#endif

#nowarn "25"

let sampleDoc() =
    let doc = XmlDocument()
    doc.LoadXml """<?xml version="1.0" encoding="utf-8"?>
<credentials>
</credentials>
""" 
    doc

[<Test>]
let ``get username, password, and auth type from node``() = 

#if TESTSUITE_RUNS_ON_DOTNETCORE
    if not(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) then
        Assert.Ignore("Encrypt use ProtectedData.Protect that is supported only on windows")
#endif

    let doc = sampleDoc()
    let node = doc.CreateElement("credential")
    node.SetAttribute("username", "demo-user")
    let password, salt = Crypto.encrypt (PlainTextPassword "demopassword")
    node.SetAttribute("password", password.ToString())
    node.SetAttribute("salt", salt.ToString())
    node.SetAttribute("authType", "ntlm")
    // Act
    let (Credentials{Username = username; Password = password; Type = NetUtils.AuthType.NTLM}) = getAuthFromNode node

    // Assert
    username |> shouldEqual  "demo-user"
    password |> shouldEqual  "demopassword"

[<Test>]
let ``get username and password from node without auth type``() = 

#if TESTSUITE_RUNS_ON_DOTNETCORE
    if not(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) then
        Assert.Ignore("Encrypt use ProtectedData.Protect that is supported only on windows")
#endif

    let doc = sampleDoc()
    let node = doc.CreateElement("credential")
    node.SetAttribute("username", "demo-user")
    let password, salt = Crypto.encrypt (PlainTextPassword "demopassword")
    node.SetAttribute("password", password.ToString())
    node.SetAttribute("salt", salt.ToString())
    // Act
    let (Credentials{Username = username; Password = password; Type = NetUtils.AuthType.Basic}) = getAuthFromNode node

    // Assert
    username |> shouldEqual  "demo-user"
    password |> shouldEqual  "demopassword"

    
[<Test>]
let ``get source nodes``() = 
    let doc = sampleDoc()
    let node = doc.CreateElement("credential")
    node.SetAttribute("source", "wrongnode")
    doc.DocumentElement.AppendChild(node) |> ignore
    let node = doc.CreateElement("credential")
    node.SetAttribute("source", "goodnode")
    doc.DocumentElement.AppendChild(node) |> ignore
    // Act
    let nodes = getSourceNodes doc "goodnode" "credential"

    // Assert
    nodes.Length |> shouldEqual 1
    nodes.Head.Attributes.["source"].Value |> shouldEqual  "goodnode"

   
[<Test>]
let ``get token from node``() = 
   let doc = sampleDoc()
   let node = doc.CreateElement "token"
   node.SetAttribute("value", "demotoken")
   let (Token token) = getAuthFromNode node

   token |> shouldEqual "demotoken"