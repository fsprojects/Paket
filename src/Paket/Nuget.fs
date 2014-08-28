module Paket.Nuget

open System.IO
open System.Net
open System.Xml

let nugetURL = "https://nuget.org/api/v2/"

let private get (url : string) = 
    use client = new WebClient()
    try 
        use stream = client.OpenRead(url)
        use reader = new StreamReader(stream)
        reader.ReadToEnd()
    with exn -> 
        // TODO: Handle HTTP 404 errors gracefully and return an empty string to indicate there is no content.
        ""

let getAllVersions package = 
    // TODO: this is a very very naive implementation
    let raw = sprintf "%sPackages()?$filter=Id eq '%s'&$select=Version" nugetURL package |> get
    let doc = XmlDocument()
    doc.LoadXml raw
    let manager = new XmlNamespaceManager(doc.NameTable)
    manager.AddNamespace("ns", "http://www.w3.org/2005/Atom")
    manager.AddNamespace("d", "http://schemas.microsoft.com/ado/2007/08/dataservices")
    manager.AddNamespace("m", "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata")
    seq { 
        for node in doc.SelectNodes("//ns:feed//ns:entry//m:properties//d:Version", manager) do
            yield node.InnerText
    }
/// Gets dependencies of a the package.
let getDependencies package version = 
    // TODO: this is a very very naive implementation
    let raw = sprintf "%sPackages(Id='%s',Version='%s')/Dependencies" nugetURL package version |> get
    let doc = XmlDocument()
    doc.LoadXml raw
    let manager = new XmlNamespaceManager(doc.NameTable)
    manager.AddNamespace("ns", "http://www.w3.org/2005/Atom")
    manager.AddNamespace("d", "http://schemas.microsoft.com/ado/2007/08/dataservices")
    manager.AddNamespace("m", "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata")
    seq { 
        for node in doc.SelectNodes("//d:Dependencies", manager) do
            yield node.InnerText
    }
    |> Seq.head
    |> fun s -> s.Split([| '|' |], System.StringSplitOptions.RemoveEmptyEntries)
    |> Array.map (fun d -> d.Split([| ':' |], System.StringSplitOptions.RemoveEmptyEntries))
    |> Array.filter (fun x -> x.Length = 2)
    |> Array.map (fun a -> 
           { Name = a.[0]
           // TODO: Parse nuget version ranges - see http://docs.nuget.org/docs/reference/versioning
             VersionRange = if a.[1].StartsWith "[" && a.[1].EndsWith "]" then Exactly (a.[1].Replace("[","").Replace("]","")) else AtLeast a.[1] 
             SourceType = "nuget"
             Source = nugetURL })



let NugetDiscovery() = 
    { new IDiscovery with
          member __.GetDirectDependencies(sourceType, source, package, version) = getDependencies package version |> Array.toList

          member __.GetVersions package = getAllVersions package } 
