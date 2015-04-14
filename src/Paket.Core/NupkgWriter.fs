module internal Paket.NupkgWriter

open System
open System.IO
open System.Xml.Linq
open System.IO.Compression
open Paket
open System.Text
open System.Xml

let nuspecId = "nuspec"
let corePropsId = "coreProp"
let contentTypePath = "[Content_Types].xml"

let contentTypeDoc fileList = 
    let declaration = XDeclaration("1.0", "UTF-8", "yes")
    let ns = XNamespace.Get "http://schemas.openxmlformats.org/package/2006/content-types"
    let root = XElement(ns + "Types")
    
    let defaultNode extension contentType = 
        let def = XElement(ns + "Default")
        def.SetAttributeValue(XName.Get "Extension", extension)
        def.SetAttributeValue(XName.Get "ContentType", contentType)
        def
    
    let knownExtensions = 
        Map.ofList [ "rels", "application/vnd.openxmlformats-package.relationships+xml"
                     "psmdcp", "application/vnd.openxmlformats-package.core-properties+xml" ]
    
    let ext path = Path.GetExtension(path).TrimStart([| '.' |]).ToLowerInvariant()
    
    let fType ext = 
        knownExtensions
        |> Map.tryFind ext
        |> function 
        | Some ft -> ft
        | None -> "application/octet"
    
    let contentTypes = 
        fileList
        |> Seq.map (fun f -> 
               let e = ext f
               e, fType e)
        |> Seq.distinct
        |> Seq.iter (fun (ex, ct) -> defaultNode ex ct |> root.Add)
    
    XDocument(declaration, box root)

let nuspecDoc (info:CompleteInfo) = 
    let core,optional = info
    let declaration = XDeclaration("1.0", "UTF-8", "yes")
    let ns = XNamespace.Get "http://schemas.microsoft.com/packaging/2011/10/nuspec.xsd"
    let root = XElement(ns + "package")
    
    let addChildNode (parent : XElement) name value = 
        let node = XElement(ns + name)
        node.SetValue value
        parent.Add node
    
    let metadataNode = XElement(ns + "metadata")
    root.Add metadataNode
    let (!!) = addChildNode metadataNode
    
    let (!!?) nodeName strOpt = 
        match strOpt with
        | Some s -> addChildNode metadataNode nodeName s
        | None -> ()
    
    let buildFrameworkReferencesNode libName = 
        let element = XElement(ns + "frameworkAssembly")
        element.SetAttributeValue(XName.Get "assemblyName", libName)
        element
    
    let buildFrameworkReferencesNode frameworkAssembliesList = 
        if frameworkAssembliesList = [] then () else
        let d = XElement(ns + "frameworkAssemblies")
        frameworkAssembliesList |> List.iter (buildFrameworkReferencesNode >> d.Add)
        metadataNode.Add d

    let buildDependencyNode (Id, (VersionRequirement(range, _))) = 
        let dep = XElement(ns + "dependency")
        dep.SetAttributeValue(XName.Get "id", Id)

        match range.FormatInNuGetSyntax() with
        | "0" -> ()
        | versionStr -> dep.SetAttributeValue(XName.Get "version", versionStr)
        dep
    
    let buildDependenciesNode dependencyList = 
        if dependencyList = [] then () else
        let d = XElement(ns + "dependencies")
        dependencyList |> List.iter (buildDependencyNode >> d.Add)
        metadataNode.Add d

    let buildReferenceNode (fileName) = 
        let dep = XElement(ns + "reference")
        dep.SetAttributeValue(XName.Get "file", fileName)
        dep

    let buildReferencesNode referenceList = 
        if referenceList = [] then () else
        let d = XElement(ns + "references")
        referenceList |> List.iter (buildReferenceNode >> d.Add)
        metadataNode.Add d
    
    !! "id" core.Id
    match core.Version with
    | Some v -> !! "version" <| v.ToString()
    | None -> failwithf "No version was given for %s" core.PackageFileName
    (!!?) "title" optional.Title
    !! "authors" (core.Authors |> String.concat ", ")
    if optional.Owners <> [] then !! "owners" (String.Join(", ",optional.Owners))
    (!!?) "licenseUrl" optional.LicenseUrl
    (!!?) "projectUrl" optional.ProjectUrl
    (!!?) "iconUrl" optional.IconUrl
    if optional.RequireLicenseAcceptance then
        !! "requireLicenseAcceptance" "true"
    !! "description" core.Description
    (!!?) "summary" optional.Summary
    (!!?) "releaseNotes" optional.ReleaseNotes
    (!!?) "copyright" optional.Copyright
    (!!?) "language" optional.Language
    if optional.Tags <> [] then !! "tags" (String.Join(" ",optional.Tags))
    if optional.DevelopmentDependency  then
        !! "developmentDependency" "true"

    optional.References |> buildReferencesNode
    optional.FrameworkAssemblyReferences |> buildFrameworkReferencesNode
    optional.Dependencies |> buildDependenciesNode
    XDocument(declaration, box root)

let corePropsPath = sprintf "package/services/metadata/core-properties/%s.psmdcp" corePropsId

let corePropsDoc (core : CompleteCoreInfo) = 
    let declaration = XDeclaration("1.0", "UTF-8", "yes")
    let ns = XNamespace.Get "http://schemas.openxmlformats.org/package/2006/metadata/core-properties"
    let dc = XNamespace.Get "http://purl.org/dc/elements/1.1/"
    let dcterms = XNamespace.Get "http://purl.org/dc/terms/"
    let xsi = XNamespace.Get "http://www.w3.org/2001/XMLSchema-instance"
    let root = 
        XElement
            (ns + "Relationships", XAttribute(XName.Get "xmlns", ns.NamespaceName), 
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

let relsPath = "_rels/.rels"

let relsDoc (core : CompleteCoreInfo) = 
    let declaration = XDeclaration("1.0", "UTF-8", "yes")
    let ns = XNamespace.Get "http://schemas.openxmlformats.org/package/2006/relationships"
    let root = XElement(ns + "Relationships")
    
    let r type' target id' = 
        let rel = XElement(ns + "Relationship")
        rel.SetAttributeValue(XName.Get "Type", type')
        rel.SetAttributeValue(XName.Get "Target", target)
        rel.SetAttributeValue(XName.Get "Id", id')
        root.Add rel
    r "http://schemas.microsoft.com/packaging/2010/07/manifest" ("/" + core.NuspecFileName) nuspecId
    r "http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" ("/" + corePropsPath) corePropsId
    XDocument(declaration, box root)

let xDocWriter (xDoc : XDocument) (stream : System.IO.Stream) = 
    let settings = new XmlWriterSettings(Indent = true, Encoding = Encoding.UTF8)
    use xmlWriter = XmlWriter.Create(stream, settings)
    xDoc.WriteTo xmlWriter
    xmlWriter.Flush()

let writeNupkg  (core : CompleteCoreInfo) optional = 
    [ core.NuspecFileName, nuspecDoc(core,optional) |> xDocWriter
      corePropsPath, corePropsDoc core |> xDocWriter
      relsPath, relsDoc core |> xDocWriter ]

let Write (core : CompleteCoreInfo) optional workingDir outputDir = 
    let outputPath = Path.Combine(outputDir, core.PackageFileName)
    if File.Exists outputPath then
        File.Delete outputPath

    use zipToCreate = new FileStream(outputPath, FileMode.Create)
    use zipFile = new ZipArchive(zipToCreate,ZipArchiveMode.Create)

    let entries = System.Collections.Generic.List<_>()
    
    let addEntry (zipFile : ZipArchive) path writerF = 
        entries.Add path |> ignore
        let entry = zipFile.CreateEntry(path)        
        use stream = entry.Open()
        writerF stream
        stream.Close()

    let ensureValidTargetName (target:string) =
        match target.Replace(" ", "%20") with
        | t when t.EndsWith("/")         -> t
        | t when String.IsNullOrEmpty(t) -> ""
        | "."                            -> ""
        | t                              -> t + "/"

    // adds all files in a directory to the zipFile
    let rec addDir source target =
        for file in Directory.EnumerateFiles(source,"*.*",SearchOption.TopDirectoryOnly) do
            let fi = FileInfo file
            let path = Path.Combine(target,fi.Name)
            entries.Add path |> ignore
            zipFile.CreateEntryFromFile(fi.FullName,path) |> ignore

        for dir in Directory.EnumerateDirectories(source,"*",SearchOption.TopDirectoryOnly) do
            let di = DirectoryInfo dir
            addDir di.FullName (Path.Combine(target,di.Name))

    // add files
    for fileName,targetFileName in optional.Files do
        let targetFileName = ensureValidTargetName targetFileName
        let source = Path.Combine(workingDir, fileName)
        if Directory.Exists source then
            addDir source targetFileName
        else 
            if File.Exists source then
                let fi = FileInfo(source)
                let path = Path.Combine(targetFileName,fi.Name)
                entries.Add path |> ignore
                zipFile.CreateEntryFromFile(source, path) |> ignore
            else 
                failwithf "Could not find source file %s" source

    // add metadata
    for path, writer in writeNupkg core optional do 
        addEntry zipFile path writer
    
    contentTypeDoc (Seq.toList entries)
    |> xDocWriter
    |> addEntry zipFile contentTypePath
