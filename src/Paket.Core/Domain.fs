module Paket.Domain

/// Represents a NuGet package name
[<System.Diagnostics.DebuggerDisplay("{Item}")>]
type PackageName =
| PackageName of string

    member this.Id = 
        match this with
        | PackageName id -> id

/// Active recognizer to convert a NuGet package name into a string
let (|PackageName|) (PackageName.PackageName name) = name

/// Function to convert a string into a NuGet package name
let PackageName name = PackageName.PackageName name

/// Represents a normalized NuGet package name
[<System.Diagnostics.DebuggerDisplay("{Item}")>]
type NormalizedPackageName =
| NormalizedPackageName of string

/// Active recognizer to convert a NuGet package name into a normalized one
let (|NormalizedPackageName|) (PackageName name) =
    NormalizedPackageName.NormalizedPackageName(name.ToLowerInvariant())

/// Function to convert a NuGet package name into a normalized one
let NormalizedPackageName = (|NormalizedPackageName|)
