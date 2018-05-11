module Paket.AssemblyMetadata

open System
open System.IO
open System.Globalization
open System.Reflection
open System.Reflection
open System.Reflection.Metadata
open System.Reflection.PortableExecutable

/// Calculates the short form of the public key token for use with binding redirects, if it exists.
let getPublicKeyToken (assembly:AssemblyName) =
    ("", assembly.GetPublicKeyToken())
    ||> Array.fold(fun state b -> state + b.ToString("X2"))
    |> function
    | "" -> None
    | token -> Some (token.ToLower())

let getAssemblyName (reader:MetadataReader) (reference:AssemblyReference) = 
    let assemblyName = reader.GetString(reference.Name)
    let cultureInfo = 
        if reference.Culture.IsNil then null 
        else CultureInfo(reader.GetString(reference.Culture))
    let hasPublicKey = reference.Flags.HasFlag(AssemblyFlags.PublicKey)
    let assemblyFlag = 
        if hasPublicKey then AssemblyNameFlags.PublicKey else AssemblyNameFlags.None
    let assemblyName = 
        new AssemblyName(
            Name = assemblyName,
            Flags = assemblyFlag,
            Version = reference.Version,
            CultureInfo = cultureInfo)
    let keyOrToken = 
        let keyOrTokenHandle = reference.PublicKeyOrToken
        if keyOrTokenHandle.IsNil then null else reader.GetBlobBytes(keyOrTokenHandle)
    if hasPublicKey then
        assemblyName.SetPublicKey(keyOrToken)
    else 
        assemblyName.SetPublicKeyToken(keyOrToken)
    assemblyName;

let getAssemblyReferences (assemblyName:AssemblyName) =
    let code = Uri(assemblyName.CodeBase).LocalPath
    use reader = new PEReader(File.OpenRead(code))
    let metadataReader = reader.GetMetadataReader()
    metadataReader.AssemblyReferences 
    |> Seq.map (fun r -> 
        let reference = metadataReader.GetAssemblyReference(r) 
        getAssemblyName metadataReader reference)
    |> Seq.toList
