/// Contains NuGet support.
module Paket.NuGetV3

open Newtonsoft.Json
open System.Collections.Generic

type JSONResource = 
    { Type : string;
      ID: string }

type JSONVersionData = 
    { Data : string [] }

let getJSONLDDetails (data : string) = JsonConvert.DeserializeObject<JSONVersionData>(data).Data

type JSONRootData = 
    { Resources : JSONResource [] }

let getSearchAutocompleteService (data : string) =  
    JsonConvert.DeserializeObject<JSONRootData>(data.Replace("@id","ID").Replace("@type","Type")).Resources
    |> Array.tryFind (fun x -> x.Type <> null && x.Type.ToLower() = "searchautocompleteservice")
    |> Option.map (fun x -> x.ID)

let private searchDict = new System.Collections.Concurrent.ConcurrentDictionary<_,_>()

let calculateNuGet3Path nugetUrl = 
    match nugetUrl with
    | "http://nuget.org/api/v2" -> Some "http://preview.nuget.org/ver3-preview/index.json"
    | "https://nuget.org/api/v2" -> Some "http://preview.nuget.org/ver3-preview/index.json"
    | _ -> None

let getSeachAPI(auth,nugetUrl) = 
    match searchDict.TryGetValue nugetUrl with
    | true,v -> v
    | _ ->
        let result = 
            match calculateNuGet3Path nugetUrl with
            | None -> None
            | Some v3Path ->
                let serviceData =
                    safeGetFromUrl(auth,v3Path) 
                    |> Async.RunSynchronously

                match serviceData with
                | None -> None
                | Some data -> getSearchAutocompleteService data

        searchDict.[nugetUrl] <- result
        result


let getViaNuGet3(auth, nugetURL, package) =
    async {
        match getSeachAPI(auth,nugetURL) with        
        | Some url -> return! safeGetFromUrl(auth,sprintf "%s?id=%s&take=10000" url package)
        | None -> return None
    }
