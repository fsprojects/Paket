module Paket.NupkgWriter

open System.IO
open System.Xml.Linq
open Ionic.Zip
open Paket

let private nuspecId = "nuspec"
let private corePropsId = "coreProp"

let private contentTypePath = "/[Content_Types].xml"

let private contentTypeDoc fileList =
    let declaration = XDeclaration("1.0", "UTF-8", "yes")
    let ns = XNamespace.Get "http://schemas.openxmlformats.org/package/2006/content-types"
    let root = XElement(ns + "Types")

    let defaultNode extension contentType =
        let def = XElement(ns + "Default")
        def.SetAttributeValue (XName.Get "Extension", extension)
        def.SetAttributeValue (XName.Get "ContentType", contentType)
        def

    let knownExtensions =
        Map.ofList [
            "rels", "application/vnd.openxmlformats-package.relationships+xml"
            "psmdcp", "application/vnd.openxmlformats-package.core-properties+xml"
        ]

    let ext path = Path.GetExtension(path).TrimStart([|'.'|]).ToLowerInvariant()

    let fType ext = 
        knownExtensions |> Map.tryFind ext
        |> function | Some ft -> ft | None -> "application/octet"

    let contentTypes =
        fileList
        |> Seq.map (fun f ->
                        let e = ext f
                        e, fType e)
        |> Seq.distinct
        |> Seq.iter (fun (ex, ct) -> defaultNode ex ct |> root.Add)

    XDocument(declaration, box root)

let private nuspecPath (core : CompleteCoreInfo) =
    sprintf "/%s.%O.nuspec" core.Id core.Version

let private nuspecDoc (core : CompleteCoreInfo) optional =
    let declaration = XDeclaration("1.0", "UTF-8", "yes")
    let ns = XNamespace.Get "http://schemas.microsoft.com/packaging/2011/10/nuspec.xsd"
    let root = XElement(ns + "package")

    let addChildNode (parent : XElement) name value =
        let node = XElement(ns + name)
        node.SetValue value
        parent.Add node

    let metadataNode = XElement(ns + "metadata")
    root.Add metadataNode
    
    let (!!) =
        addChildNode metadataNode

    let (!!?) nodeName strOpt =
        match strOpt with
        | Some s ->
            addChildNode metadataNode nodeName s
        | None ->
            ()

    let buildDependencyNode (Id, (VersionRequirement (range, _))) =
        let dep = XElement(ns + "dependency")
        dep.SetAttributeValue(XName.Get "id", Id)
        let versionStr =
            match range with
            | Minimum v ->
                v.ToString()
            | GreaterThan v ->
                sprintf "(%A,)" v
            | Maximum v ->
                sprintf "(,%A]" v
            | LessThan v ->
                sprintf "(,%A)" v
            | OverrideAll v
            | Specific v ->
                sprintf "[%A]" v
            | Range (fromB, from, to', toB) ->
                let opening =
                    match fromB with
                    | VersionRangeBound.Excluding -> "("
                    | VersionRangeBound.Including -> "["
                let closing =
                    match toB with
                    | VersionRangeBound.Excluding -> ")"
                    | VersionRangeBound.Including -> "]"
                sprintf "%s%A, %A%s" opening from to' closing
        match versionStr with
        | "0" -> ()
        | _ ->
            dep.SetAttributeValue(XName.Get "version", versionStr)
        dep

    let buildDependenciesNode dependencyList =
        let d = XElement(ns + "dependencies")
        dependencyList
        |> List.iter (buildDependencyNode >> d.Add)
        metadataNode.Add d

    !! "id" core.Id
    !! "version" <| core.Version.ToString()
    !!? "title" optional.Title
    !! "authors" (core.Authors |> String.concat ", ")
    !!? "owners" (optional.Owners |> Option.map (fun o -> o |> String.concat ", "))
    !!? "licenseUrl" optional.LicenseUrl
    !!? "projectUrl" optional.ProjectUrl
    !!? "iconUrl" optional.IconUrl
    !!? "requireLicenseAcceptance" (optional.RequireLicenseAcceptance |> Option.map (fun b -> b.ToString()))
    !! "description" core.Description
    !!? "summary" optional.Summary
    !!? "releaseNotes" optional.ReleaseNotes
    !!? "copyright" optional.Copyright
    !!? "language" optional.Language
    !!? "tags" (optional.Tags |> Option.map (fun t -> t |> String.concat " "))
    !!? "developmentDependency" (optional.DevelopmentDependency |> Option.map (fun b -> b.ToString()))
    optional.Dependencies |> Option.iter buildDependenciesNode

    XDocument(declaration, box root)

let private corePropsPath =
    sprintf "/package/services/metadata/core-properties/%s.psmdcp" corePropsId

let private corePropsDoc (core : CompleteCoreInfo) =
    let declaration = XDeclaration("1.0", "UTF-8", "yes")
    let ns = XNamespace.Get "http://schemas.openxmlformats.org/package/2006/metadata/core-properties"
    let dc = XNamespace.Get "http://purl.org/dc/elements/1.1/"
    let dcterms = XNamespace.Get "http://purl.org/dc/terms/"
    let xsi = XNamespace.Get "http://www.w3.org/2001/XMLSchema-instance"
    let root = XElement(ns + "Relationships",
                XAttribute(XName.Get "xmlns", ns.NamespaceName),
                XAttribute(XNamespace.Xmlns + "dc", dc.NamespaceName),
                XAttribute(XNamespace.Xmlns + "dcterms", dcterms.NamespaceName),
                XAttribute(XNamespace.Xmlns + "xsi", xsi.NamespaceName))
    let (!!) (ns : XNamespace) name value =
        let node = XElement(ns + name)
        node.SetValue value
        root.Add node
    !! dc "creator" core.Authors
    !! dc "description" core.Description
    !! dc "identifier" core.Id
    !! ns "version" core.Version
    XElement(ns + "keywords") |> root.Add
    !! dc "title" core.Id
    !! ns "lastModifiedBy" "paket"

    XDocument(declaration, box root)

let private relsPath = "/_rels/.rels"

let private relsDoc core =
    let declaration = XDeclaration("1.0", "UTF-8", "yes")
    let ns = XNamespace.Get "http://schemas.openxmlformats.org/package/2006/relationships"
    let root = XElement(ns + "Relationships")

    let r type' target id' =
        let rel = XElement(ns + "Relationship")
        rel.SetAttributeValue(XName.Get "Type", type')
        rel.SetAttributeValue(XName.Get "Target", target)
        rel.SetAttributeValue(XName.Get "Id", id')
        root.Add rel

    r
        "http://schemas.microsoft.com/packaging/2010/07/manifest"
        (nuspecPath core)
        nuspecId

    r
        "http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties"
        corePropsPath
        corePropsId

    XDocument(declaration, box root)

let private xDocWriter (xDoc : XDocument) (stream : System.IO.Stream) =
    let xmlWriter = new System.Xml.XmlTextWriter(stream, System.Text.Encoding.UTF8)
    xDoc.WriteTo xmlWriter
    xmlWriter.Flush()

let private writeNupkg core optional =
    [
        nuspecPath core, nuspecDoc core optional |> xDocWriter
        corePropsPath, corePropsDoc core |> xDocWriter
        relsPath, relsDoc core |> xDocWriter
    ]

let Write (core : CompleteCoreInfo) optional workingDir outputDir =
    let outputPath = Path.Combine(outputDir, sprintf "%s.%O.nupkg" core.Id core.Version)
    if File.Exists outputPath then
        File.Delete outputPath
    use zipFile = new ZipFile(outputPath)
    let addEntry (zipFile : ZipFile) path writer =
        let writeDel _ stream =
            writer stream
        zipFile.AddEntry(path, WriteDelegate(writeDel))

    optional.Files
    |> Option.iter (fun files ->
            files
            |> List.iter (fun (f, t) ->
                  let source = Path.Combine(workingDir, f)
                  if Directory.Exists source then
                      zipFile.AddDirectory(source, t.Replace(" ", "%20")) |> ignore
                  else if File.Exists source then
                      zipFile.AddFile(source, t.Replace(" ", "%20")) |> ignore
                  else failwithf "Could not find source file %s" source))

    writeNupkg core optional
    |> List.iter (fun (path, writer) -> addEntry zipFile path writer |> ignore)

    let fileList =
        zipFile.Entries
        |> Seq.filter (fun e -> not e.IsDirectory)
        |> Seq.map (fun e -> e.FileName)

    let contentTypesDoc =
        addEntry zipFile contentTypePath (contentTypeDoc fileList |> xDocWriter)

    zipFile.Save()

