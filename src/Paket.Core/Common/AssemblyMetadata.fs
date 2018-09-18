module Paket.AssemblyMetadata

open System
open System.IO
open System.Globalization
open System.Reflection
open System.Reflection.Metadata
open System.Reflection.PortableExecutable
open System.Runtime.CompilerServices
open Chessie.ErrorHandling

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

type private ParameterValueProvider(reader: BlobReader) =
    let getValueReader (reader: BlobReader) = 
        let prolog = reader.ReadUInt16() |> int
        if prolog <> 1 then 
            raise(ArgumentException(sprintf "prolog=%d" prolog))
        reader; 

    let value = getValueReader reader
    
    let getPrimitiveType (typeCode: PrimitiveTypeCode) = 
        match typeCode with 
        | PrimitiveTypeCode.Boolean -> value.ReadBoolean().ToString()
        | PrimitiveTypeCode.Char -> value.ReadChar().ToString()
        | PrimitiveTypeCode.SByte -> value.ReadSByte().ToString()
        | PrimitiveTypeCode.Byte -> value.ReadByte().ToString()
        | PrimitiveTypeCode.Int16 -> value.ReadInt16().ToString()
        | PrimitiveTypeCode.UInt16 -> value.ReadUInt16().ToString()
        | PrimitiveTypeCode.Int32 -> value.ReadInt32().ToString()
        | PrimitiveTypeCode.UInt32 -> value.ReadUInt32().ToString()
        | PrimitiveTypeCode.Int64 -> value.ReadInt64().ToString()
        | PrimitiveTypeCode.UInt64 -> value.ReadUInt64().ToString()
        | PrimitiveTypeCode.Single -> value.ReadSingle().ToString()
        | PrimitiveTypeCode.Double -> value.ReadDouble().ToString()
        | PrimitiveTypeCode.String -> value.ReadSerializedString()
        | PrimitiveTypeCode.Void
        | PrimitiveTypeCode.TypedReference 
        | PrimitiveTypeCode.IntPtr 
        | PrimitiveTypeCode.UIntPtr 
        | PrimitiveTypeCode.Object 
        | _ -> Enum.Format(typeof<PrimitiveTypeCode>, typeCode, "F")
            // raise(NotSupportedException(sprintf "%A" typeCode))

    let fixKnownTypeName (typename: String) =
        match typename with
        | null -> null
        | t when t.StartsWith("FSharp.", StringComparison.Ordinal) || 
          t.StartsWith("Microsoft.FSharp.", StringComparison.Ordinal) ->
            typename + ",FSharp.Core"
        | _ -> typename

    let getNamedTypeValue typename rawTypeKind =
        match rawTypeKind with 
        | 17uy -> // SignatureTypeKind.ValueType 
            try
                let fullname = fixKnownTypeName typename
                let realtype = Type.GetType(fullname, false)
                if isNull realtype then None
                else
                    let enumtype = Enum.GetUnderlyingType(realtype)
                    if isNull enumtype then None
                    else
                        match PrimitiveTypeCode.TryParse enumtype.Name with
                        | true, typecode ->
                            let intvalue =
                                match typecode with
                                | PrimitiveTypeCode.SByte -> value.ReadSByte() |> int
                                | PrimitiveTypeCode.Byte -> value.ReadByte() |> int
                                | PrimitiveTypeCode.Int16 -> value.ReadInt16() |> int
                                | PrimitiveTypeCode.UInt16 -> value.ReadUInt16() |> int
                                | PrimitiveTypeCode.Int32 -> value.ReadInt32() |> int
                                | PrimitiveTypeCode.UInt32 -> value.ReadUInt32() |> int
                                | _ -> raise(ArgumentException(enumtype.FullName))
                            let realname = 
                                match realtype.Name with | null -> typename | n -> n  
                            Some(realname + "." + Enum.Format(realtype, intvalue, "F"))
                        | false, _ -> None      
            with 
            | ex -> ex |> ignore; None
        | 18uy -> Some("(" + typename + ")")
        | _ -> None
    
    member private this.ThrowNotSupported([<CallerMemberName>] ?name: string) =
        raise(NotSupportedException(defaultArg name ""))
        
    interface ISignatureTypeProvider<string, int> with 
        member x.GetPrimitiveType(typeCode: PrimitiveTypeCode) = getPrimitiveType typeCode
                    
        member x.GetPinnedType(elementType: string) = elementType + "^"
        member x.GetByReferenceType(elementType: string) = elementType + "&"
        member x.GetPointerType(elementType: string) = elementType + "*"
        member x.GetSZArrayType(elementType: string) = elementType + "[]"
            
        member x.GetFunctionPointerType(signature: MethodSignature<string>) = "?"
        member x.GetGenericMethodParameter(genericContext: int, index: int) = "?"
        member x.GetGenericTypeParameter(genericContext: int, index: int) = "?"
        
        member x.GetModifiedType(modifier: string, unmodifiedType: string, isRequired: bool) = "?"            
        member x.GetTypeFromSpecification
              (reader: MetadataReader, genericContext: int, handle: TypeSpecificationHandle, rawTypeKind: byte) = "?"            
        member x.GetTypeFromDefinition(reader: MetadataReader, handle: TypeDefinitionHandle, rawTypeKind: byte) = "?"
           
        member x.GetTypeFromReference(reader: MetadataReader, handle: TypeReferenceHandle, rawTypeKind: byte) =
            if handle.IsNil then "nil" else
            let typespec = reader.GetTypeReference(handle)
            let typename =
                let basename = reader.GetString(typespec.Name)
                if typespec.Namespace.IsNil then
                    try
                        match typespec.ResolutionScope.Kind with 
                        | HandleKind.TypeReference ->
                            let typeref = TypeReferenceHandle.op_Explicit(typespec.ResolutionScope)
                            let outertype = reader.GetTypeReference(typeref)
                            let outername = reader.GetString(outertype.Name)
                            let outerfull = 
                                match outertype.Namespace with 
                                | ns when ns.IsNil -> outername
                                | ns -> reader.GetString(ns) + "." + outername
                            outerfull + "+" + basename
                        | _ -> basename
                    with 
                    | _ -> basename
                else reader.GetString(typespec.Namespace) + "." + basename
            match getNamedTypeValue typename rawTypeKind with 
            | Some rawvalue -> "(" + rawvalue + ")"
            | None -> sprintf "(:ref:(%s#%d))" typename rawTypeKind           
            
        member x.GetGenericInstantiation
            (genericType: string, typeArguments: Collections.Immutable.ImmutableArray<string>) = "?"
            
        member x.GetArrayType(elementType: string, shape: ArrayShape) = "?"
            
type AssemblyDefinition =
    | AssemblyFile of String
    | AssemblyInfo of FileInfo
    | AssemblyName of AssemblyName        

type AssemblyMetadataReader(assembly:AssemblyDefinition) =
    let assemblyFilePath = 
        match assembly with 
        | AssemblyFile file -> file
        | AssemblyInfo info -> info.FullName
        | AssemblyName name -> Uri(name.CodeBase).LocalPath
        
    let newAssemblyReader filePath =
        let fileStream = File.OpenRead(assemblyFilePath)
        try // PEStreamOptions.Prefetch* close the stream
            new PEReader(fileStream, PEStreamOptions.PrefetchMetadata)
        with 
        | ex -> 
            fileStream.Dispose(); 
            reraise()

    let fileReader = newAssemblyReader assemblyFilePath
    let reader = fileReader.GetMetadataReader() 
    let definition = reader.GetAssemblyDefinition()
    
    member val AssemblyPath = assemblyFilePath with get

    member this.AssemblyName =
        reader.GetString(definition.Name)

    member this.AssemblyVersion =
        Some definition.Version // already System.Version

    member this.AssemblyDetails = 
        this.AssemblyName, this.AssemblyVersion, this.AssemblyPath

    member this.getAssemblyAttributes =
        let getAttributeData (attr:CustomAttribute) = 
            try
                let ctor = attr.Constructor                
                match ctor.Kind with
                | HandleKind.MemberReference ->
                    let ctor_ref = 
                        attr.Constructor
                        |> MemberReferenceHandle.op_Explicit
                        |> reader.GetMemberReference
                        
                    let type_ref = 
                        ctor_ref.Parent
                        |> TypeReferenceHandle.op_Explicit
                        |> reader.GetTypeReference
                        
                    let typeName = reader.GetString(type_ref.Name)
                
                    let firstArg =    
                        if attr.Value.IsNil then ""
                        else         
                        try         
                            let attrBlob = reader.GetBlobReader(attr.Value)
                            let provider = ParameterValueProvider(attrBlob)
                                                           
                            let methSign = ctor_ref.DecodeMethodSignature(
                                                    provider, 0)
                            
                            match methSign.ParameterTypes.Length with
                            | 0 -> ""
                            | _ -> Seq.head(methSign.ParameterTypes)
                        with 
                        | ex -> sprintf "(%A)" ex
                    
                    typeName, firstArg
                | _ -> 
                    ctor.ToString(), (sprintf "(%A)" ctor.Kind)
            with
            | ex -> attr.ToString(), (sprintf "(%A)" ex)
        
        let mapCustomAttribute (hatt:CustomAttributeHandle) =
            reader.GetCustomAttribute(hatt) |> getAttributeData
    
        let handles = reader.CustomAttributes |> Seq.toList
        let results = handles |> List.map mapCustomAttribute
        results;

    member this.getAssemblyReferences =
        reader.AssemblyReferences
        |> Seq.map (fun aref -> 
            try
                let reference = reader.GetAssemblyReference(aref)
                pass(getAssemblyName reader reference)
            with
            | ex -> fail ex)
        |> collect
    
    interface IDisposable with
        member this.Dispose() =
            if not (isNull fileReader) then fileReader.Dispose()

/// Uses the assembly file metadata from the provided source, 
/// to return AssemblyName list of its referenced assemblies.
let getAssemblyReferences (assembly:AssemblyDefinition) =
    try
        use reader = new AssemblyMetadataReader(assembly)
        reader.getAssemblyReferences
    with
    | ex -> fail ex
