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
    static member All = { References = NuspecReferences.All; FrameworkAssemblyReferences = []}
    static member Load(fileName : string) = 
        let fi = FileInfo(fileName)
        if not fi.Exists then Nuspec.All
        else 
            let doc = new XmlDocument()
            doc.Load fi.FullName
            let manager = new XmlNamespaceManager(doc.NameTable)
            manager.AddNamespace("ns1", "http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd")
            manager.AddNamespace("ns2", "http://schemas.microsoft.com/packaging/2011/08/nuspec.xsd")
            manager.AddNamespace("ns3", "http://schemas.microsoft.com/packaging/2013/01/nuspec.xsd")

            let referencesNodes = 
                [ for node in doc.SelectNodes("//ns1:references", manager) do
                    yield node
                  for node in doc.SelectNodes("//ns2:references", manager) do
                    yield node
                  for node in doc.SelectNodes("//ns3:references", manager) do
                    yield node]

            let references =
                if List.isEmpty referencesNodes then NuspecReferences.All else 
                let files = 
                    [ for node in doc.SelectNodes("//ns1:reference", manager) do
                        yield node.Attributes.["file"].InnerText                      
                      for node in doc.SelectNodes("//ns2:reference", manager) do
                        yield node.Attributes.["file"].InnerText 
                        for node in doc.SelectNodes("//ns3:reference", manager) do
                        yield node.Attributes.["file"].InnerText ]
                NuspecReferences.Explicit files

            let frameworkAssemblyNodes = 
                [ for node in doc.SelectNodes("//ns1:frameworkAssemblies", manager) do
                    yield node
                  for node in doc.SelectNodes("//ns2:frameworkAssemblies", manager) do
                    yield node
                  for node in doc.SelectNodes("//ns3:frameworkAssemblies", manager) do
                    yield node ]

            let frameworkAssemblyReferences = 
                if List.isEmpty frameworkAssemblyNodes then [] else 
                [ for node in doc.SelectNodes("//ns1:frameworkAssembly", manager) do
                      yield { AssemblyName = node.Attributes.["assemblyName"].InnerText
                              TargetFramework = node.Attributes.["targetFramework"].InnerText }
                  for node in doc.SelectNodes("//ns2:frameworkAssembly", manager) do
                      yield { AssemblyName = node.Attributes.["assemblyName"].InnerText
                              TargetFramework = node.Attributes.["targetFramework"].InnerText } 
                  for node in doc.SelectNodes("//ns3:frameworkAssembly", manager) do
                      yield { AssemblyName = node.Attributes.["assemblyName"].InnerText
                              TargetFramework = node.Attributes.["targetFramework"].InnerText }                               ]

            { References = references; FrameworkAssemblyReferences = frameworkAssemblyReferences }