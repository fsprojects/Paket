module Paket.BindingRedirects

open System
open System.Text
open System.Xml
open System.Xml.Linq
open System.IO
open Paket.Xml.Linq
open System.Xml.XPath
open System.Reflection

/// Represents a binding redirection
type BindingRedirect = 
    { AssemblyName : string
      Version : string
      PublicKeyToken : string
      Culture : string option }

let private bindingNs = "urn:schemas-microsoft-com:asm.v1"

let private ensureAssemblyBinding doc = 
    doc |> ensurePathExists ("/configuration/runtime/assemblyBinding!" + bindingNs)

/// Updates the supplied MSBuild document with the supplied binding redirect.
let internal setRedirect (doc:XDocument) bindingRedirect =
    let createElementWithNs = createElement (Some bindingNs)
    let tryGetElementWithNs = tryGetElement (Some bindingNs)
    let getElementsWithNs = getElements (Some bindingNs)

    let assemblyBinding = ensureAssemblyBinding doc
    let dependentAssembly =
        assemblyBinding
        |> getElementsWithNs "dependentAssembly"
        |> Seq.tryFind(fun dependentAssembly ->
            defaultArg
                (dependentAssembly
                 |> tryGetElementWithNs "assemblyIdentity"
                 |> Option.bind(tryGetAttribute "name")
                 |> Option.map(fun attribute -> attribute.Value = bindingRedirect.AssemblyName))
                false)
        |> function
           | Some dependentAssembly -> dependentAssembly
           | None ->
                let dependentAssembly = createElementWithNs "dependentAssembly" []
                dependentAssembly.Add(createElementWithNs "assemblyIdentity" [ "name", bindingRedirect.AssemblyName
                                                                               "publicKeyToken", bindingRedirect.PublicKeyToken
                                                                               "culture", defaultArg bindingRedirect.Culture "neutral" ])
                assemblyBinding.Add(dependentAssembly)
                dependentAssembly
                
    // According to MSDN (https://msdn.microsoft.com/en-us/library/eftw1fys(v=vs.110).aspx),
    // "The format of an assembly version number is major.minor.build.revision. Valid values for each part of this version number are 0 to 65535"
    let newRedirect = createElementWithNs "bindingRedirect" [ "oldVersion", "0.0.0.0-65535.65535.65535.65535"
                                                              "newVersion", bindingRedirect.Version ]

    match tryGetElementWithNs "Paket" dependentAssembly with
    | Some e -> e.Value <- "True"
    | None -> dependentAssembly.AddFirst(XElement(XName.Get("Paket", bindingNs), "True"))

    match dependentAssembly |> tryGetElementWithNs "bindingRedirect" with
    | Some redirect -> redirect.ReplaceWith(newRedirect)
    | None -> dependentAssembly.Add(newRedirect)
    doc

let internal indentAssemblyBindings config =
    let assemblyBinding = ensureAssemblyBinding config
    
    let sb = StringBuilder()
    let xmlWriterSettings = XmlWriterSettings()
    xmlWriterSettings.Indent <- true 
    ( use writer = XmlWriter.Create(sb, xmlWriterSettings)
      let tempAssemblyBindingNode = XElement.Parse(assemblyBinding.ToString())
      tempAssemblyBindingNode.WriteTo writer)

    let parent = assemblyBinding.Parent
    assemblyBinding.Remove()
    let newText = sb.ToString()
    let newAssemblyBindingNode = XElement.Parse(newText, LoadOptions.PreserveWhitespace)
    
    if newAssemblyBindingNode.HasElements then
        parent.Add(newAssemblyBindingNode)
    else
        if not parent.HasElements then
            parent.Remove()

let private baseConfig = """<?xml version="1.0" encoding="utf-8"?>
<configuration>
</configuration>
"""
let private createAppConfigInDirectory folder =
    let config = Path.Combine(folder, "app.config")
    File.WriteAllText(config, baseConfig)
    config

let private addConfigFileToProject project =
    project.ProjectNode
    |> Xml.getNodes "ItemGroup"
    |> List.tryHead
    |> Option.map(fun g -> project, g)
    |> Option.iter(fun (project, itemGroup) ->
        project.CreateNode "Content"
        |> Xml.addAttribute "Include" "app.config"
        |> itemGroup.AppendChild
        |> ignore
        project.Save(false))

/// Applies a set of binding redirects to a single configuration file.
let private applyBindingRedirects isFirstGroup cleanBindingRedirects (allKnownLibNames:string seq) bindingRedirects (projectFile:ProjectFile) (configFilePath:string) =
    let config =
        try
            XDocument.Load(configFilePath, LoadOptions.PreserveWhitespace)
        with
        | exn -> failwithf "Parsing of %s failed.%s%s" configFilePath Environment.NewLine exn.Message

    use originalContents = new StringReader(config.ToString())
    let original = XDocument.Load(originalContents, LoadOptions.None).ToString()

    let isMarked e =
        match tryGetElement (Some bindingNs) "Paket" e with
        | Some e -> String.equalsIgnoreCase (e.Value.Trim()) "true"
        | None -> false

    let libIsContained e =
        let haystack = e.ToString().ToLower()
        allKnownLibNames 
        |> Seq.exists (fun b -> 
            let needle = (sprintf "name=\"%s\"" b).ToLower()
            haystack.Contains needle)

    let nsManager = XmlNamespaceManager(NameTable())
    nsManager.AddNamespace("bindings", bindingNs)
    config.XPathSelectElements("//bindings:assemblyBinding", nsManager)
    |> Seq.collect (fun e -> e.Elements(XName.Get("dependentAssembly", bindingNs)))
    |> List.ofSeq // strict evaluation, as otherwise e.Remove() also changes the order!
    |> List.filter (fun e -> isFirstGroup && (cleanBindingRedirects || isMarked e) && libIsContained e)
    |> List.iter (fun e -> e.Remove())

    let config = Seq.fold setRedirect config bindingRedirects

    try
        indentAssemblyBindings config
    with
    | exn -> failwithf "Indenting binding redirects of %s failed.%s%s" configFilePath Environment.NewLine exn.Message

    use newContents = new StringReader(config.ToString())
    let newText = XDocument.Load(newContents, LoadOptions.None).ToString()
    if newText <> original then
        use f = File.Open(configFilePath, FileMode.Create)
        config.Save(f, SaveOptions.DisableFormatting)

    projectFile.SetOrCreateAutoGenerateBindingRedirects()

let findAllReferencesFiles root =
    let findRefFile (p:ProjectFile) =
        match p.FindReferencesFile() with
        | Some fileName -> 
            try
                Some(p)
            with e ->
                None
        | None ->
            None
            
    ProjectFile.FindAllProjects root
    |> Array.choose findRefFile


/// Applies a set of binding redirects to all .config files in a specific folder.
let applyBindingRedirectsToFolder isFirstGroup createNewBindingFiles cleanBindingRedirects rootPath allKnownLibNames bindingRedirects =
    let applyBindingRedirects projectFile =
        let bindingRedirects = bindingRedirects projectFile |> Seq.toList
        let path = Path.GetDirectoryName projectFile.FileName
        match projectFile.TryFindConfigFile() with
        | Some c -> Some c.FullName
        | None -> 
            match createNewBindingFiles, List.isEmpty bindingRedirects with
            | true, false ->
                let config = createAppConfigInDirectory path
                addConfigFileToProject projectFile
                Some config
            | _ -> None
        |> Option.iter (applyBindingRedirects isFirstGroup cleanBindingRedirects allKnownLibNames bindingRedirects projectFile)

    for p in findAllReferencesFiles rootPath do
        applyBindingRedirects p

/// Calculates the short form of the public key token for use with binding redirects, if it exists.
let getPublicKeyToken (assembly:AssemblyName) =
    assembly.GetPublicKeyToken()
    |> Option.ofObj
    |> Option.map (Array.fold(fun state b -> state + b.ToString("X2")) "")
    |> Option.bind (function | "" -> None | token -> Some (token.ToLower()))