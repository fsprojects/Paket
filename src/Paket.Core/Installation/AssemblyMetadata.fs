/// Reads the metadata of an assembly file without loading it.
module internal Paket.AssemblyMetadata

open System.Reflection
open Mono.Cecil

type ReferencedAssembly =
    { RefName : string
      RefVersion : System.Version }

type AssemblyMetadata =
    { Name : string
      Version : System.Version
      PublicKeyToken : string option
      References : ReferencedAssembly list
      /// The assembly attributes with their first constructor argument, by type name without namespace
      Attributes : (string * string) list }

let rec private typeName (t : TypeReference) =
    match t.DeclaringType with
    | null -> t.Name
    | declaringType -> typeName declaringType + "+" + t.Name

let rec private argumentText (value : obj) =
    match value with
    | null -> ""
    | :? CustomAttributeArgument as argument -> argumentText argument.Value
    | :? TypeReference -> ""
    | value -> value.ToString()

let private attributes (attributeNames : Set<string>) (assembly : AssemblyDefinition) =
    [ for attribute in assembly.CustomAttributes do
        let name = typeName attribute.AttributeType
        // only the requested attributes are decoded: an enum argument resolves its type in other assemblies,
        // which can fail, and the attribute is skipped then
        if attributeNames.Contains name then
            let firstArgument =
                try
                    if attribute.HasConstructorArguments then Some (argumentText attribute.ConstructorArguments.[0].Value)
                    else None
                with _ -> None
            match firstArgument with
            | Some argument -> yield name, argument
            | None -> () ]

let private publicKeyToken (name : AssemblyNameDefinition) =
    let assemblyName = AssemblyName(Name = name.Name, Version = name.Version)
    if not (isNull name.PublicKey) && name.PublicKey.Length > 0 then
        assemblyName.SetPublicKey name.PublicKey
    BindingRedirects.getPublicKeyToken assemblyName

let private readFile attributeNames (fileName : string) =
    // InMemory closes the file right away, even when Cecil throws without disposing the module
    use assembly = AssemblyDefinition.ReadAssembly(fileName, ReaderParameters(ReadingMode.Deferred, InMemory = true))
    { Name = assembly.Name.Name
      Version = assembly.Name.Version
      PublicKeyToken = publicKeyToken assembly.Name
      References =
        [ for reference in assembly.MainModule.AssemblyReferences ->
            { RefName = reference.Name; RefVersion = reference.Version } ]
      Attributes = attributes attributeNames assembly }

/// Reads the name, version, public key token and references of an assembly
let read fileName = readFile Set.empty fileName

/// Reads the name, version, public key token and references of an assembly, with the given attributes
let readWithAttributes attributeNames fileName = readFile attributeNames fileName
