module Paket.BindingRedirects

open Paket
open Paket.BindingRedirects
open NUnit.Framework
open System.Xml.Linq
open FsUnit
open System.Xml
open System.IO
open Pri.LongPath

let defaultRedirect = 
    { AssemblyName = "Assembly"
      Version = "1.0.0"
      PublicKeyToken = "PUBLIC_KEY"
      Culture = None }

let emptySampleDoc() = 
    let doc = """<?xml version="1.0" encoding="utf-8"?>
<configuration>
</configuration>"""
    XDocument.Parse(doc,LoadOptions.PreserveWhitespace)

let sampleDocWithRuntime() = 
    let doc = """<?xml version="1.0" encoding="utf-8"?>
<configuration><runtime></runtime>
</configuration>""" 
    XDocument.Parse(doc,LoadOptions.PreserveWhitespace)

let sampleDoc() = 
    let doc = """<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <appSettings>
    <!-- For Goldmine tests -->
    <add key="GoldmineEntityType" value="Proxy"/>
    <!-- Google apps API keys -->
    <add key="GoogleAppMigrationWizProjectApiKey-1" value="key1"/>
    <add key="GoogleAppMigrationWizProjectApiKey-2" value="key2"/>
    <add key="GoogleAppMigrationWizProjectApiKey-3" value="key3"/>
    <add key="SendEmails" value="false"/>
    <add key="BitTitanDropBoxAppKey" value="BitTitan"/>
    <add key="BitTitanDropBoxSecretKey" value="BitTitan"/>
  </appSettings>
  <startup>
    <supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.5.2"/>
  </startup>
</configuration>"""
    XDocument.Parse(doc,LoadOptions.PreserveWhitespace)

let private bindingNs = "urn:schemas-microsoft-com:asm.v1"
let private containsDescendents count ns elementName (doc:XDocument) =
    Assert.AreEqual(count, doc.Descendants(XName.Get(elementName, ns)) |> Seq.length)
let private containsSingleDescendent = containsDescendents 1 ""
let private containsSingleDescendentWithNs = containsDescendents 1 bindingNs
let private createBindingRedirectXml culture assembly version publicKey = sprintf "<dependentAssembly xmlns=\"urn:schemas-microsoft-com:asm.v1\">\r\n  <Paket>True</Paket>\r\n  <assemblyIdentity name=\"%s\" publicKeyToken=\"%s\" culture=\"%s\" />\r\n  <bindingRedirect oldVersion=\"0.0.0.0-65535.65535.65535.65535\" newVersion=\"%s\" />\r\n</dependentAssembly>" assembly publicKey culture version
let private xNameForNs name = XName.Get(name, bindingNs)

let sampleDocWithNoIndentation() = sprintf """<?xml version="1.0" encoding="utf-8"?>
<configuration>
<runtime><assemblyBinding xmlns="%s">%s</assemblyBinding></runtime></configuration>""" bindingNs (createBindingRedirectXml "cul" "asm" "v" "pKey") |> XDocument.Parse

[<Test>]
let ``add missing elements to configuration file``() = 
    let doc = emptySampleDoc()

    // Act
    setRedirect doc defaultRedirect |> ignore

    // Assert
    doc |> containsSingleDescendent "runtime"
    doc |> containsSingleDescendentWithNs "assemblyBinding"

[<Test>]
let ``add new binding redirect to configuration file``() = 
    let doc = emptySampleDoc()

    // Act
    setRedirect doc defaultRedirect |> ignore

    // Assert
    doc |> containsSingleDescendentWithNs "dependentAssembly"


[<Test>]
let ``correctly creates a binding redirect``() = 
    let doc = emptySampleDoc()
    setRedirect doc { defaultRedirect with Culture = Some "en-gb"; PublicKeyToken = "123456" } |> ignore

    // Act
    let dependency = doc.Descendants(xNameForNs "dependentAssembly") |> Seq.head

    // Assert
    dependency.ToString()
    |> normalizeLineEndings
    |> shouldEqual (createBindingRedirectXml "en-gb" "Assembly" "1.0.0" "123456" |> normalizeLineEndings)

[<Test>]
let ``correctly creates a binding redirect with default culture``() = 
    let doc = emptySampleDoc()
    setRedirect doc defaultRedirect |> ignore

    // Act
    let dependency = doc.Descendants(xNameForNs "dependentAssembly") |> Seq.head

    // Assert
    dependency.ToString() 
    |> normalizeLineEndings
    |> shouldEqual (createBindingRedirectXml "neutral" "Assembly" "1.0.0" "PUBLIC_KEY" |> normalizeLineEndings)

[<Test>]
let ``does not overwrite existing binding redirects for a different assembly``() = 
    let doc = emptySampleDoc()
    setRedirect doc defaultRedirect |> ignore

    // Act
    setRedirect doc { defaultRedirect with AssemblyName = "OtherAssembly" } |> ignore

    // Assert
    doc |> containsDescendents 2 bindingNs "dependentAssembly"

[<Test>]
let ``does not add a new binding redirect if one already exists for the assembly``() = 
    let doc = emptySampleDoc()
    setRedirect doc defaultRedirect |> ignore

    // Act
    setRedirect doc { defaultRedirect with Version = "2.0.0" } |> ignore

    // Assert
    doc |> containsSingleDescendentWithNs "dependentAssembly"

[<Test>]
let ``correctly updates an existing binding redirect``() = 
    let doc = emptySampleDoc()
    setRedirect doc defaultRedirect |> ignore

    // Act
    setRedirect doc { defaultRedirect with Version = "2.0.0" } |> ignore

    // Assert
    let dependency = doc.Descendants(xNameForNs "dependentAssembly") |> Seq.head
    dependency.ToString() 
    |> normalizeLineEndings
    |> shouldEqual (createBindingRedirectXml "neutral" "Assembly" "2.0.0" "PUBLIC_KEY" |> normalizeLineEndings)
    
[<Test>]
let ``redirects got properly indented for readability in empty sample docs``() = 
    let doc = emptySampleDoc()
    setRedirect doc defaultRedirect |> ignore

    // Act
    indentAssemblyBindings doc

    let expected = """
<configuration>
<runtime><assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
  <dependentAssembly>
    <Paket>True</Paket>
    <assemblyIdentity name="Assembly" publicKeyToken="PUBLIC_KEY" culture="neutral" />
    <bindingRedirect oldVersion="0.0.0.0-65535.65535.65535.65535" newVersion="1.0.0" />
  </dependentAssembly>
</assemblyBinding></runtime></configuration>"""

    // Assert
    doc.ToString(SaveOptions.DisableFormatting)
    |> normalizeLineEndings 
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``redirect tags are removed if we have no redirect empty sample docs``() = 
    let doc = emptySampleDoc()

    // Act
    indentAssemblyBindings doc

    let expected = """
<configuration>
</configuration>"""

    // Assert
    doc.ToString(SaveOptions.DisableFormatting)
    |> normalizeLineEndings 
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``redirects got properly indented for readability in sample doc with runtime``() = 
    let doc = sampleDocWithRuntime()
    setRedirect doc defaultRedirect |> ignore

    // Act
    indentAssemblyBindings doc

    let expected = """
<configuration><runtime><assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
  <dependentAssembly>
    <Paket>True</Paket>
    <assemblyIdentity name="Assembly" publicKeyToken="PUBLIC_KEY" culture="neutral" />
    <bindingRedirect oldVersion="0.0.0.0-65535.65535.65535.65535" newVersion="1.0.0" />
  </dependentAssembly>
</assemblyBinding></runtime>
</configuration>"""

    // Assert
    doc.ToString(SaveOptions.DisableFormatting)
    |> normalizeLineEndings 
    |> shouldEqual (normalizeLineEndings expected)

[<Test>]
let ``redirects got properly indented for readability in real world sample docs``() = 
    let doc = sampleDoc()
    setRedirect doc defaultRedirect |> ignore

    // Act
    indentAssemblyBindings doc

    let expected = """
<configuration>
  <appSettings>
    <!-- For Goldmine tests -->
    <add key="GoldmineEntityType" value="Proxy" />
    <!-- Google apps API keys -->
    <add key="GoogleAppMigrationWizProjectApiKey-1" value="key1" />
    <add key="GoogleAppMigrationWizProjectApiKey-2" value="key2" />
    <add key="GoogleAppMigrationWizProjectApiKey-3" value="key3" />
    <add key="SendEmails" value="false" />
    <add key="BitTitanDropBoxAppKey" value="BitTitan" />
    <add key="BitTitanDropBoxSecretKey" value="BitTitan" />
  </appSettings>
  <startup>
    <supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.5.2" />
  </startup>
<runtime><assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
  <dependentAssembly>
    <Paket>True</Paket>
    <assemblyIdentity name="Assembly" publicKeyToken="PUBLIC_KEY" culture="neutral" />
    <bindingRedirect oldVersion="0.0.0.0-65535.65535.65535.65535" newVersion="1.0.0" />
  </dependentAssembly>
</assemblyBinding></runtime></configuration>"""

    // Assert
    doc.ToString(SaveOptions.DisableFormatting)
    |> normalizeLineEndings 
    |> shouldEqual (normalizeLineEndings expected)

let toSafePath = Path.GetFullPath
let buildMockGetFiles outcomes =
    let outcomes =
        outcomes
        |> List.map(fun ((path, extension), results) ->
            (path |> toSafePath, extension), results |> List.map toSafePath)
    fun (path, wildcard, _) ->
        outcomes
        |> List.tryFind (fst >> (=) (path |> toSafePath, wildcard))
        |> Option.map snd
        |> defaultArg <| []
        |> List.toArray
let rootPath = @"C:/rootpath/" |> toSafePath

[<Test>]
let ``project file containing paket.references is marked for binding redirect``() =
    let mockGetFiles =
        buildMockGetFiles
            [ (@"C:/rootpath/", "*.references"), [ @"C:/rootpath/source/paket.references" ]
              (@"C:/rootpath/source", "*proj"), [ @"C:/rootpath/source/Project.fsproj" ]
            ]

    getProjectFilesWithPaketReferences mockGetFiles rootPath
    |> shouldEqual [ @"C:/rootpath/source/Project.fsproj" |> toSafePath ]

[<Test>]
let ``project file not containing paket.references is not marked for binding redirect``() =
    let mockGetFiles =
        buildMockGetFiles
            [ (@"C:/rootpath/", "paket.references"), [] 
              (@"C:/rootpath/source", "*proj"), [ @"C:/rootpath/source/Project.fsproj" ]
            ]
    getProjectFilesWithPaketReferences mockGetFiles rootPath
    |> shouldEqual []

[<Test>]
let ``adds paket's node if one does not exist``() = 
    let doc = emptySampleDoc()
    setRedirect doc defaultRedirect |> ignore

    let dependency = doc.Descendants(xNameForNs "dependentAssembly") |> Seq.head
    dependency.Nodes()
    |> Seq.filter (fun e -> e.NodeType = XmlNodeType.Element)
    |> Seq.map (fun e -> e :?> XElement)
    |> Seq.filter (fun e -> e.Name = XName.Get("Paket"))
    |> List.ofSeq
    |> List.iter (fun e -> e.Remove())

    // Act
    setRedirect doc { defaultRedirect with Version = "2.0.0" } |> ignore

    // Assert
    let dependency = doc.Descendants(xNameForNs "dependentAssembly") |> Seq.head
    dependency.ToString() 
    |> normalizeLineEndings
    |> shouldEqual (createBindingRedirectXml "neutral" "Assembly" "2.0.0" "PUBLIC_KEY" |> normalizeLineEndings)

[<Test>]
let ``replaces paket's node if one already exists``() = 
    let doc = emptySampleDoc()
    setRedirect doc defaultRedirect |> ignore

    let dependency = doc.Descendants(xNameForNs "dependentAssembly") |> Seq.head
    dependency.Nodes()
    |> Seq.filter (fun e -> e.NodeType = XmlNodeType.Element)
    |> Seq.map (fun e -> e :?> XElement)
    |> Seq.filter (fun e -> e.Name = XName.Get("Paket"))
    |> List.ofSeq
    |> List.iter (fun e -> e.Value <- "False")

    // Act
    setRedirect doc { defaultRedirect with Version = "2.0.0" } |> ignore

    // Assert
    let dependency = doc.Descendants(xNameForNs "dependentAssembly") |> Seq.head
    dependency.ToString() 
    |> normalizeLineEndings
    |> shouldEqual (createBindingRedirectXml "neutral" "Assembly" "2.0.0" "PUBLIC_KEY" |> normalizeLineEndings)
