module Paket.Nuget

open System.IO
open System.Net
open System.Xml
open Newtonsoft.Json

let private get (url : string) = 
    use client = new WebClient()
    try 
        use stream = client.OpenRead(url)
        use reader = new StreamReader(stream)
        reader.ReadToEnd()
    with exn -> 
        // TODO: Handle HTTP 404 errors gracefully and return an empty string to indicate there is no content.
        ""
/// Gets versions of the given package.
let getAllVersions nugetURL package =
    let raw = sprintf "%s/package-versions/%s" nugetURL package |> get
    if raw = "" then Seq.empty else
    JsonConvert.DeserializeObject<string[]>(raw) |> Array.toSeq

let parseVersionRange (text:string) =
    if text = "" then Latest else
    if text.StartsWith "[" && text.EndsWith "]" then Exactly (text.Replace("[","").Replace("]","")) else AtLeast text

/// Gets all dependencies of the given package version.
let getDependencies nugetURL package version = 
    // TODO: this is a very very naive implementation
    let raw = sprintf "%s/Packages(Id='%s',Version='%s')/Dependencies" nugetURL package version |> get
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
    |> Array.map (fun d -> d.Split ':')
    |> Array.filter (fun d -> Array.isEmpty d |> not && d.[0] <> "")
    |> Array.map (fun a -> 
           let v = if a.Length > 1 then a.[1] else ""
           { Name = a.[0]
           // TODO: Parse nuget version ranges - see http://docs.nuget.org/docs/reference/versioning
             VersionRange = parseVersionRange v
             SourceType = "nuget"
             Source = nugetURL })

let NugetDiscovery() = 
    { new IDiscovery with
          
          member __.GetDirectDependencies(sourceType, source, package, version) = 
              if sourceType <> "nuget" then failwithf "invalid sourceType %s" sourceType
              getDependencies source package version |> Array.toList
          
          member __.GetVersions(sourceType, source, package) = 
              if sourceType <> "nuget" then failwithf "invalid sourceType %s" sourceType
              getAllVersions source package }