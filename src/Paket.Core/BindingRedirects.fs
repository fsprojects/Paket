module Paket.BindingRedirects

open System
open System.Text
open System.Xml
open System.Xml.Linq
open System.IO
open System.Reflection
open Paket.Xml.Linq

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
                dependentAssembly.Add(createElementWithNs "assemblyIdentity" ([ "name", bindingRedirect.AssemblyName
                                                                                "publicKeyToken", bindingRedirect.PublicKeyToken
                                                                                "culture", defaultArg bindingRedirect.Culture "neutral" ]))
                assemblyBinding.Add(dependentAssembly)
                dependentAssembly
                
    let newRedirect = createElementWithNs "bindingRedirect" [ "oldVersion", "0.0.0.0-999.999.999.999"
                                                              "newVersion", bindingRedirect.Version ]
    match dependentAssembly |> tryGetElementWithNs "bindingRedirect" with
    | Some redirect -> redirect.ReplaceWith(newRedirect)
    | None -> dependentAssembly.Add(newRedirect)
    doc

let internal indentAssemblyBindings config =
    let assemblyBinding = ensureAssemblyBinding config
    
    let sb = StringBuilder()
    let xmlWriterSettings = XmlWriterSettings()
    xmlWriterSettings.Indent <- true 
    using (XmlWriter.Create(sb, xmlWriterSettings)) (fun writer -> 
                                                        let tempAssemblyBindingNode = XElement.Parse(assemblyBinding.ToString())
                                                        tempAssemblyBindingNode.WriteTo writer)
    let parent = assemblyBinding.Parent
    assemblyBinding.Remove()
    let newAssemblyBindingNode = XElement.Parse(sb.ToString(), LoadOptions.PreserveWhitespace)
    parent.Add(newAssemblyBindingNode)

let private configFiles = [ "app"; "web" ] |> Set.ofList
let private projectFiles = [ ".csproj"; ".vbproj"; ".fsproj"; ".wixproj" ] |> Set.ofList
let private toLower (s:string) = s.ToLower()
let private isAppOrWebConfig = configFiles.Contains << (Path.GetFileNameWithoutExtension >> toLower)
let private isDotNetProject = projectFiles.Contains << (Path.GetExtension >> toLower)
let internal getConfig getFiles directory  =
    getFiles(directory, "*.config", SearchOption.AllDirectories)
    |> Seq.tryFind isAppOrWebConfig
let internal getProjectFilesWithPaketReferences getFiles rootPath  =
    getFiles(rootPath, Constants.ReferencesFile, SearchOption.AllDirectories)
    |> Seq.map Path.GetDirectoryName
    |> Seq.choose(fun directory -> getFiles(directory, "*proj", SearchOption.TopDirectoryOnly) |> Seq.tryFind (Path.GetExtension >> isDotNetProject))
    |> Seq.toList
let private getExistingConfigFiles getFiles rootPath = 
    getFiles(rootPath, "*.config", SearchOption.AllDirectories)
    |> Seq.filter isAppOrWebConfig
let private baseConfig = """<?xml version="1.0" encoding="utf-8"?>
<configuration>
</configuration>
"""
let private createAppConfigInDirectory folder =
    let config = Path.Combine(folder, "app.config")
    File.WriteAllText(config, baseConfig)
    config
let private getProjectFilesInDirectory folder =
    Directory.GetFiles(folder, "*proj")
    |> Seq.filter (Path.GetExtension >> isDotNetProject)
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
        project.Save())

/// Applies a set of binding redirects to a single configuration file.
let private applyBindingRedirects bindingRedirects (configFilePath:string) =
    let config = 
        try
            XDocument.Load(configFilePath, LoadOptions.PreserveWhitespace)
        with
        | exn -> failwithf "Parsing of %s failed.%s%s" configFilePath Environment.NewLine exn.Message

    let config = Seq.fold setRedirect config bindingRedirects
    indentAssemblyBindings config
    config.Save configFilePath

/// Applies a set of binding redirects to all .config files in a specific folder.
let applyBindingRedirectsToFolder createNewBindingFiles rootPath bindingRedirects =
    let applyBindingRedirects projectFile =
        let bindingRedirects = bindingRedirects projectFile
        if Seq.isEmpty bindingRedirects |> not then
            let path = Path.GetDirectoryName projectFile.FileName
            match getConfig Directory.GetFiles path with
            | Some c -> Some c
            | None -> 
                match createNewBindingFiles with
                | false -> None
                | true ->
                    let config = createAppConfigInDirectory path
                    addConfigFileToProject projectFile
                    Some config
            |> Option.iter (applyBindingRedirects bindingRedirects)

    rootPath
    |> getProjectFilesWithPaketReferences Directory.GetFiles
    |> Seq.map ProjectFile.TryLoad
    |> Seq.choose id
    |> Seq.iter (applyBindingRedirects)

/// Calculates the short form of the public key token for use with binding redirects, if it exists.
let getPublicKeyToken (assembly:Assembly) =
    ("", assembly.GetName().GetPublicKeyToken())
    ||> Array.fold(fun state b -> state + b.ToString("X2"))
    |> function
    | "" -> None
    | token -> Some <| token.ToLower()
