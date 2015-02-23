namespace Paket

open System
open System.Xml
open System.IO
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

type Nuspec = 
    { References : NuspecReferences 
      Dependencies : (PackageName * VersionRequirement * FrameworkRestrictions) list
      OfficialName : string
      FrameworkAssemblyReferences : FrameworkAssemblyReference list }

    static member All = { References = NuspecReferences.All; Dependencies = []; FrameworkAssemblyReferences = []; OfficialName = "" }
    static member Explicit references = { References = NuspecReferences.Explicit references; Dependencies = []; FrameworkAssemblyReferences = []; OfficialName = "" }
    static member Load(fileName : string) = 
        let fi = FileInfo(fileName)
        if not fi.Exists then Nuspec.All
        else 
            let doc = new XmlDocument()
            doc.Load fi.FullName

            let officialName = 
                match doc |> getNode "package" |> optGetNode "metadata" |> optGetNode "id" with
                | Some node -> node.InnerText
                | None -> failwithf "unable to find package id in %s" fileName

            let dependency node = 
                let name = 
                    match node |> getAttribute "id" with
                    | Some name -> PackageName name
                    | None -> failwithf "unable to find dependency id in %s" fileName                            
                let version = 
                    match node |> getAttribute "version" with
                    | Some version -> VersionRequirement.Parse version
                    | None ->         VersionRequirement.Parse "0"
                let restriction =
                    let parent = node.ParentNode 
                    match parent.Name.ToLower(), parent |> getAttribute "targetFramework" with
                    | "group", Some framework -> 
                        match FrameworkDetection.Extract framework with
                        | Some x -> [FrameworkRestriction.Exactly x]
                        | None -> []
                    | _ -> []
                name,version,restriction

            let frameworks =
                doc 
                |> getDescendants "group" 
                |> Seq.map (fun node ->
                    match node |> getAttribute "targetFramework" with
                    | Some framework ->
                        match FrameworkDetection.Extract framework with
                        | Some x -> [PackageName "",VersionRequirement.NoRestriction,[FrameworkRestriction.Exactly x]]
                        | None -> []
                    | _ -> [])
                |> Seq.concat
                |> Seq.toList

            let dependencies = 
                doc 
                |> getDescendants "dependency"
                |> List.map dependency
                |> List.append frameworks
                |> Requirements.optimizeRestrictions 
            
            let references = 
                doc
                |> getDescendants "reference"
                |> List.choose (getAttribute "file")

            let assemblyRefs node =
                let name = node |> getAttribute "assemblyName"
                let targetFrameworks = node |> getAttribute "targetFramework"
                match name,targetFrameworks with
                | Some name, Some targetFrameworks when targetFrameworks = "" ->
                    [{ AssemblyName = name; FrameworkRestrictions = [] }]
                | Some name, None ->                     
                    [{ AssemblyName = name; FrameworkRestrictions = [] }]
                | Some name, Some targetFrameworks ->                     
                    targetFrameworks.Split([|','; ' '|],System.StringSplitOptions.RemoveEmptyEntries)
                    |> Array.choose FrameworkDetection.Extract
                    |> Array.map (fun fw -> { AssemblyName = name; FrameworkRestrictions = [FrameworkRestriction.Exactly fw] })
                    |> Array.toList
                | _ -> []

            let frameworkAssemblyReferences =
                let grouped =
                    doc
                    |> getDescendants "frameworkAssembly"
                    |> List.collect assemblyRefs
                    |> Seq.groupBy (fun r -> r.AssemblyName)

                [for name,restrictions in grouped do
                    yield { AssemblyName = name
                            FrameworkRestrictions = 
                                restrictions 
                                |> Seq.collect (fun x -> x.FrameworkRestrictions) 
                                |> Seq.toList} ]
           
            { References = if references = [] then NuspecReferences.All else NuspecReferences.Explicit references
              Dependencies = dependencies
              OfficialName = officialName
              FrameworkAssemblyReferences = frameworkAssemblyReferences }