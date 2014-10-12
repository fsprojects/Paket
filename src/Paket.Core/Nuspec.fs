namespace Paket

open System.Xml
open System.IO

[<RequireQualifiedAccess>]
type NuspecReferences = 
    | All
    | Explicit of string list

type FrameworkAssemblyReference = {
    AssemblyName: string
    TargetFramework : string}

type Nuspec = 
    { References : NuspecReferences 
      FrameworkAssemblyReferences : FrameworkAssemblyReference list}
    static member KnownNamespaces =
        ["ns1","http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd"
         "ns2","http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd"
         "ns3","http://schemas.microsoft.com/packaging/2013/01/nuspec.xsd"]

    static member All = { References = NuspecReferences.All; FrameworkAssemblyReferences = []}
    static member Load(fileName : string) = 
        let fi = FileInfo(fileName)
        if not fi.Exists then Nuspec.All
        else 
            let doc = new XmlDocument()
            doc.Load fi.FullName
            let manager = new XmlNamespaceManager(doc.NameTable)

            Nuspec.KnownNamespaces
            |> List.iter (fun (name,ns) -> manager.AddNamespace(name, ns))
            
            let getReferences ns =
                if List.isEmpty [ for node in doc.SelectNodes(sprintf "//%s:references" ns, manager) -> node] then [] else
                    [ for node in doc.SelectNodes(sprintf "//%s:reference" ns, manager) -> 
                        node.Attributes.["file"].InnerText]

            let getframeworkAssemblyReferences ns =
                if List.isEmpty [ for node in doc.SelectNodes(sprintf "//%s:frameworkAssemblies" ns, manager) -> node] then [] else
                    [ for node in doc.SelectNodes(sprintf "//%s:frameworkAssembly" ns, manager) -> 
                        { AssemblyName = node.Attributes.["assemblyName"].InnerText
                          TargetFramework = node.Attributes.["targetFramework"].InnerText }]

            let references =
                Nuspec.KnownNamespaces
                |> List.map (fun (name,ns) -> getReferences name)
                |> List.concat
            
            let frameworkAssemblyReferences =
                Nuspec.KnownNamespaces
                |> List.map (fun (name,ns) -> getframeworkAssemblyReferences name)
                |> List.concat

            { References = if references = [] then NuspecReferences.All else NuspecReferences.Explicit references; 
              FrameworkAssemblyReferences = frameworkAssemblyReferences }