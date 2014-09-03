module Paket.Hashing

open System.Security.Cryptography
open System
open System.IO
    
let private getAlgorithm algorithmName = 
    match algorithmName with
    | "SHA512" -> Some(SHA512.Create() :> HashAlgorithm)
    | "SHA256" -> Some(SHA256.Create() :> HashAlgorithm)
    | _ -> None
    |> Option.map (fun algo (data : FileStream) -> 
           data
           |> algo.ComputeHash
           |> Convert.ToBase64String)

/// Compares a nuget hash with a local file
let compareWith packageName (localFile : FileInfo) (nugetHashDetails : PackageHash) = 
    match getAlgorithm nugetHashDetails.Algorithm with
    | Some computeHash -> 
        use stream = localFile.FullName |> File.OpenRead
        let localHash = stream |> computeHash
        if localHash <> nugetHashDetails.Hash then 
            Some(sprintf "downloaded package hash does not match nuget for package %s" packageName)
        else None
    | None -> Some "unknown hashing algorithm used"
