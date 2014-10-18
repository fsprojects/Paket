module Paket.BindingRedirects

open Paket
open Paket.BindingRedirects
open NUnit.Framework
open System.Xml.Linq
open FsUnit

let defaultRedirect =
    {   AssemblyName = "Assembly"
        Version = "1.0.0"
        PublicKeyToken = None
        Culture = None }

let sampleDoc() = """<?xml version="1.0" encoding="utf-8"?>
<configuration>
</configuration>""" |> XDocument.Parse

let private containsDescendents count elementName (doc:XDocument) =
    Assert.AreEqual(count, doc.Descendants(XName.Get elementName) |> Seq.length)
let private containsSingleDescendent = containsDescendents 1
let private createSimpleBindingRedirectXml assembly version = sprintf "<dependentAssembly>\r\n  <assemblyIdentity name=\"%s\" />\r\n  <bindingRedirect oldVersion=\"0.0.0.0-%s\" newVersion=\"%s\" />\r\n</dependentAssembly>" assembly version version
let private createFullBindingRedirectXml assembly version culture publicKey = sprintf "<dependentAssembly>\r\n  <assemblyIdentity name=\"%s\" publicKeyToken=\"%s\" culture=\"%s\" />\r\n  <bindingRedirect oldVersion=\"0.0.0.0-%s\" newVersion=\"%s\" />\r\n</dependentAssembly>" assembly publicKey culture version version

[<Test>]
let ``add missing elements to configuration file``() = 
    let doc = sampleDoc()

    // Act
    setRedirect doc defaultRedirect |> ignore

    // Assert
    doc |> containsSingleDescendent "runtime"
    doc |> containsSingleDescendent "assemblyBinding"

[<Test>]
let ``add new binding redirect to configuration file``() = 
    let doc = sampleDoc()

    // Act
    setRedirect doc defaultRedirect |> ignore

    // Assert
    doc |> containsSingleDescendent "dependentAssembly"

[<Test>]
let ``correctly creates a simple binding redirect``() = 
    let doc = sampleDoc()
    setRedirect doc defaultRedirect |> ignore

    // Act
    let dependency = doc.Descendants(XName.Get "dependentAssembly") |> Seq.head

    // Assert
    dependency.ToString() |> shouldEqual (createSimpleBindingRedirectXml "Assembly" "1.0.0")

[<Test>]
let ``correctly creates a full binding redirect``() = 
    let doc = sampleDoc()
    setRedirect doc { defaultRedirect with Culture = Some "en-gb"; PublicKeyToken = Some "123456" } |> ignore

    // Act
    let dependency = doc.Descendants(XName.Get "dependentAssembly") |> Seq.head

    // Assert
    dependency.ToString() |> shouldEqual (createFullBindingRedirectXml "Assembly" "1.0.0" "en-gb" "123456")

[<Test>]
let ``does not overwrite existing binding redirects for a different assembly``() = 
    let doc = sampleDoc()
    setRedirect doc defaultRedirect |> ignore

    // Act
    setRedirect doc { defaultRedirect with AssemblyName = "OtherAssembly" } |> ignore

    // Assert
    doc |> containsDescendents 2 "dependentAssembly"

[<Test>]
let ``does not add a new binding redirect if one already exists for the assembly``() = 
    let doc = sampleDoc()
    setRedirect doc defaultRedirect |> ignore

    // Act
    setRedirect doc { defaultRedirect with Version = "2.0.0" } |> ignore

    // Assert
    doc |> containsSingleDescendent "dependentAssembly"

[<Test>]
let ``correctly updates an existing binding redirect``() = 
    let doc = sampleDoc()
    setRedirect doc defaultRedirect |> ignore

    // Act
    setRedirect doc { defaultRedirect with Version = "2.0.0" } |> ignore

    // Assert
    let dependency = doc.Descendants(XName.Get "dependentAssembly") |> Seq.head
    dependency.ToString() |> shouldEqual (createSimpleBindingRedirectXml "Assembly" "2.0.0")

