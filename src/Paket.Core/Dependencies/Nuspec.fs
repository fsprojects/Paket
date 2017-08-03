namespace Paket

open System
open System.Xml
open System.IO
open Pri.LongPath
open Xml
open Paket.Requirements
open Paket.Domain

///  Nuspec reference type inside of nuspec files.
[<RequireQualifiedAccess>]
type NuspecReferences = 
    | All
    | Explicit of string list

/// Framework assembly reference inside of nuspec files.
type FrameworkAssemblyReference = {
    AssemblyName: string
    FrameworkRestrictions : FrameworkRestrictions }

module internal NuSpecParserHelper =
    let getDependency fileName node = 
        let name = 
            match node |> getAttribute "id" with
            | Some name -> PackageName name
            | None -> failwithf "unable to find dependency id in %s" fileName
        let version = 
            match node |> getAttribute "version" with
            | Some version -> VersionRequirement.Parse version
            | None -> VersionRequirement.Parse "0"

        let parent = node.ParentNode 
        match parent.Name, parent |> getAttribute "targetFramework" with
        | n , Some framework when String.equalsIgnoreCase n "group" -> 
            let framework = framework.Replace(".NETPortable0.0","portable")
            PlatformMatching.extractPlatforms framework
            |> Option.map (fun pp -> name, version, pp)
        | _ -> Some(name,version, PlatformMatching.ParsedPlatformPath.Empty)

    let getAssemblyRefs node =
        let name = node |> getAttribute "assemblyName"
        let targetFrameworks = node |> getAttribute "targetFramework"
        match name,targetFrameworks with
        | Some name, Some targetFrameworks when targetFrameworks = "" ->
            [{ AssemblyName = name; FrameworkRestrictions = ExplicitRestriction FrameworkRestriction.NoRestriction }]
        | Some name, None ->
            [{ AssemblyName = name; FrameworkRestrictions = ExplicitRestriction FrameworkRestriction.NoRestriction }]
        | Some name, Some targetFrameworks ->
            targetFrameworks.Split([|','; ' '|],System.StringSplitOptions.RemoveEmptyEntries)
            |> Array.choose FrameworkDetection.Extract
            |> Array.map (fun fw -> { AssemblyName = name; FrameworkRestrictions = ExplicitRestriction (FrameworkRestriction.Exactly fw) })
            |> Array.toList
        | _ -> []

type Nuspec = 
    { References : NuspecReferences 
      Dependencies : (PackageName * VersionRequirement * FrameworkRestrictions) list
      OfficialName : string
      // Currently only used for testing
      Version : string
      LicenseUrl : string
      IsDevelopmentDependency : bool
      FrameworkAssemblyReferences : FrameworkAssemblyReference list }
    static member All = { Version = ""; References = NuspecReferences.All; Dependencies = []; FrameworkAssemblyReferences = []; OfficialName = ""; LicenseUrl = ""; IsDevelopmentDependency = false }
    static member Explicit references = { Version = ""; References = NuspecReferences.Explicit references; Dependencies = []; FrameworkAssemblyReferences = []; OfficialName = ""; LicenseUrl = ""; IsDevelopmentDependency = false }
    
    static member Load(root,groupName,version,includeVersionInPath,name:PackageName) =
        let folder = DirectoryInfo(getTargetFolder root groupName name version includeVersionInPath).FullName
        let nuspec = Path.Combine(folder,sprintf "%O.nuspec" name)
        Nuspec.Load nuspec
    
    /// load the file from an XmlDocument. The fileName is only used for error reporting.
    static member private Load(fileName:string, doc:XmlDocument) =
        let frameworks =
            doc 
            |> getDescendants "group" 
            |> List.choose (fun node ->
                match node |> getAttribute "targetFramework" with
                | Some framework ->
                    let framework = framework.ToLower().Replace(".netportable","portable").Replace("netportable","portable")
                    PlatformMatching.extractPlatforms framework
                | _ -> Some PlatformMatching.ParsedPlatformPath.Empty)

        let rawDependencies =
            doc 
            |> getDescendants "dependency"
            |> List.choose (NuSpecParserHelper.getDependency fileName)

        let dependencies = addFrameworkRestrictionsToDependencies rawDependencies frameworks
            
        let references = 
            doc
            |> getDescendants "reference"
            |> List.choose (getAttribute "file")
           
        { References = if references = [] then NuspecReferences.All else NuspecReferences.Explicit references
          Dependencies = dependencies
          OfficialName = 
            match doc |> getNode "package" |> optGetNode "metadata" |> optGetNode "id" with
            | Some node -> node.InnerText
            | None -> failwithf "unable to find package id in %s" fileName
          Version = 
            match doc |> getNode "package" |> optGetNode "metadata" |> optGetNode "version" with
            | Some node -> node.InnerText
            | None -> failwithf "unable to find package version in %s" fileName
          LicenseUrl = 
            match doc |> getNode "package" |> optGetNode "metadata" |> optGetNode "licenseUrl" with
            | Some link -> link.InnerText
            | None -> ""
          IsDevelopmentDependency =
            match doc |> getNode "package" |> optGetNode "metadata" |> optGetNode "developmentDependency" with
            | Some link -> String.equalsIgnoreCase link.InnerText "true"
            | None -> false
          FrameworkAssemblyReferences = 
            let grouped =
                doc
                |> getDescendants "frameworkAssembly"
                |> List.collect NuSpecParserHelper.getAssemblyRefs
                |> List.groupBy (fun r -> r.AssemblyName)

            [for name,restrictions in grouped do
                yield { AssemblyName = name
                        FrameworkRestrictions =
                            ExplicitRestriction(
                                restrictions
                                |> List.map (fun x -> x.FrameworkRestrictions |> getExplicitRestriction)
                                |> List.fold FrameworkRestriction.combineRestrictionsWithOr FrameworkRestriction.EmptySet) } ] }

    /// load the file from an nuspec text stream. The fileName is only used for error reporting.
    static member internal Load(fileName:string, f:Stream) =
        let doc = new XmlDocument()
        doc.Load f
        Nuspec.Load (fileName, doc)
        
    /// load the file from an xml text. The fileName is only used for error reporting.
    static member internal Load(fileName:string, text:string) =
        let doc = new XmlDocument()
        doc.LoadXml text
        Nuspec.Load (fileName, doc)

    /// load the file from a given file.
    static member Load(fileName : string) = 
        let fi = FileInfo(fileName)
        if not fi.Exists then Nuspec.All
        else
            try
                use f = File.OpenRead(fi.FullName)
                Nuspec.Load(fileName, f)
            with
            | exn ->
                try
                    let text = File.ReadAllText(fi.FullName)  // work around mono bug https://github.com/fsprojects/Paket/issues/1189
                    Nuspec.Load(fileName, text)
                with
                | ex ->
                    raise (IOException("Cannot load " + fileName + Environment.NewLine + "Message: " + ex.Message))