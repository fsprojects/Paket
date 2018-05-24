module Paket.AssemblyMetadata

open Chessie.ErrorHandling
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

/// Creates AssemblyName of AssemblyReference opened on the MetadataReader; 
/// expected to be part of next release of the Metadata support, so we can remove this. 
/// Can throw AccessViolation if used incorrectly, with unrelated or disposed reader. 
let private getAssemblyName (reader:MetadataReader) (reference:AssemblyReference) = 
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

type AssemblyDefinition =
    | AssemblyFile of String
    | AssemblyInfo of FileInfo
    | AssemblyName of AssemblyName

/// Uses the assembly file metadata from the provided source, 
/// to return AssemblyName list of its referenced assemblies.
let getAssemblyReferences (assembly:AssemblyDefinition) =
    try
        let assemblyFilePath = 
            match assembly with 
            | AssemblyFile file -> file
            | AssemblyInfo info -> info.FullName
            | AssemblyName name -> Uri(name.CodeBase).LocalPath
        use file = File.OpenRead(assemblyFilePath)
        use reader = new PEReader(file, PEStreamOptions.PrefetchMetadata)
        let metadataReader = reader.GetMetadataReader()
        metadataReader.AssemblyReferences 
        |> Seq.map (fun aref -> 
            try
                let reference = metadataReader.GetAssemblyReference(aref) 
                pass(getAssemblyName metadataReader reference)
            with
            | ex -> fail ex)
        |> collect
    with
    | ex -> fail ex
        

