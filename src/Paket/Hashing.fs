module Hashing

open System.Security.Cryptography
open System.Xml.Linq
open System.Net
open System
open System.IO
    
/// Gets hash value and algorithm from Nuget.
let getDetailsFromNuget name version = 
    async { 
        use wc = new WebClient()
        let! data = sprintf "https://www.nuget.org/api/v2/Packages(Id='%s',Version='%s')" name version
                    |> wc.DownloadStringTaskAsync
                    |> Async.AwaitTask
        let data = XDocument.Parse data
            
        let getAttribute = 
            let rootNs = XName.Get("entry", "http://www.w3.org/2005/Atom")
            let propertiesNs = 
                XName.Get("properties", "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata")
            let attributesNs attribute = 
                XName.Get(attribute, "http://schemas.microsoft.com/ado/2007/08/dataservices")
                
            let properties = 
                rootNs
                |> data.Element
                |> fun entry -> entry.Element(propertiesNs)
            fun attribute -> properties.Element(attributesNs attribute).Value
        return (getAttribute "PackageHash", getAttribute "PackageHashAlgorithm")
    }
    
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
let compareWith (localFile : FileInfo) nugetHashDetails = 
    let nugetHashValue, algorithmName = nugetHashDetails
    match algorithmName |> getAlgorithm with
    | Some computeHash -> 
        use stream = localFile.FullName |> File.OpenRead
        let localHash = stream |> computeHash
        if localHash <> nugetHashValue then Some "downloaded package hash does not match nuget"
        else None
    | None -> Some "unknown hashing algorithm used"

