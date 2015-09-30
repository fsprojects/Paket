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
let private projectFiles = [ ".csproj"; ".vbproj"; ".fsproj" ] |> Set.ofList
let private toLower (s:string) = s.ToLower()
let private isAppOrWebConfig = configFiles.Contains << (Path.GetFileNameWithoutExtension >> toLower)
let private isDotNetProject = projectFiles.Contains << (Path.GetExtension >> toLower)
let internal getFoldersWithPaketReferencesAndNoConfig getFiles rootPath  =
    getFiles(rootPath, Constants.ReferencesFile, SearchOption.AllDirectories)
    |> Seq.map Path.GetDirectoryName
    |> Seq.filter(fun directory -> getFiles(directory, "*.config", SearchOption.TopDirectoryOnly) |> Seq.forall (not << isAppOrWebConfig))
    |> Seq.toList
let private getExistingConfigFiles getFiles rootPath = 
    getFiles(rootPath, "*.config", SearchOption.AllDirectories)
    |> Seq.filter isAppOrWebConfig
let private baseConfig = """<?xml version="1.0" encoding="utf-8"?>
<configuration>
</configuration>
"""
let private createAppConfigInDirectory folder =
    File.WriteAllText(Path.Combine(folder, "app.config"), baseConfig)
    folder
let private getProjectFilesInDirectory folder =
    Directory.GetFiles(folder, "*proj")
    |> Seq.filter (Path.GetExtension >> isDotNetProject)
let private addConfigFileToProject projectFile =
    ProjectFile.Load projectFile
    |> Option.bind(fun project ->
        project.ProjectNode
        |> Xml.getNodes "ItemGroup"
        |> List.tryHead
        |> Option.map(fun g -> project, g))
    |> Option.iter(fun (project, itemGroup) ->
        project.CreateNode "Content"
        |> Xml.addAttribute "Include" "app.config"
        |> itemGroup.AppendChild
        |> ignore
        project.Save())

/// Applies a set of binding redirects to a single configuration file.
let private applyBindingRedirects bindingRedirects (configFilePath:string) =
    let projectFile =
        getProjectFilesInDirectory (Path.GetDirectoryName(configFilePath))
        |> Seq.map ProjectFile.Load
        |> Seq.tryHead
        |> Option.bind id
    
    let bindingRedirects =
        match projectFile with
        | None -> Seq.empty
        | Some p -> 
            p.GetTargetProfile()
            |> bindingRedirects

    if Seq.isEmpty bindingRedirects then ()
    else
        let config = 
            try 
                XDocument.Load(configFilePath, LoadOptions.PreserveWhitespace)
            with
            | :? System.Xml.XmlException as ex ->
                Logging.verbosefn "Illegal XML in file: %s" configFilePath
                raise ex
        let config = Seq.fold setRedirect config bindingRedirects
        indentAssemblyBindings config
        config.Save configFilePath

/// Applies a set of binding redirects to all .config files in a specific folder.
let applyBindingRedirectsToFolder createNewBindingFiles rootPath bindingRedirects =
    if createNewBindingFiles then
        // First create missing configuration files.
        rootPath
        |> getFoldersWithPaketReferencesAndNoConfig Directory.GetFiles
        |> Seq.collect (createAppConfigInDirectory >> getProjectFilesInDirectory)
        |> Seq.iter addConfigFileToProject

    // Now ensure all configuration files have binding redirects.
    rootPath
    |> getExistingConfigFiles Directory.GetFiles
    |> Seq.iter (applyBindingRedirects bindingRedirects)

/// Calculates the short form of the public key token for use with binding redirects, if it exists.
let getPublicKeyToken (assembly:Assembly) =
    ("", assembly.GetName().GetPublicKeyToken())
    ||> Array.fold(fun state b -> state + b.ToString("X2"))
    |> function
    | "" -> None
    | token -> Some <| token.ToLower()
