module Paket.Git.Handling
open Paket.Utils
open System

let extractUrlParts (url:string) =
    let url,commit,options = 
        match url.Split([|' '|],StringSplitOptions.RemoveEmptyEntries) |> List.ofArray with
        | urlPart::_ ->
            let rest = url.Substring(urlPart.Length)
            match rest.Replace(":"," : ").Split([|' '|],StringSplitOptions.RemoveEmptyEntries) |> List.ofArray with
            | k::colon::_ when colon = ":" && (k.ToLower() = "build" || k.ToLower() = "os" || k.ToLower() = "packages") -> 
                urlPart,None,rest
            | commit::options -> 
                let startPos = url.Substring(urlPart.Length).IndexOf(commit)
                let options = url.Substring(urlPart.Length + startPos + commit.Length)
                urlPart,Some commit,options
            | _ -> url,None,""
        | _ -> url,None,""
    
    let kvPairs = parseKeyValuePairs options

    let buildCommand = 
        match kvPairs.TryGetValue "build" with
        | true,x -> Some (x.Trim('\"'))
        | _ -> None

    let operatingSystemRestriction = 
        match kvPairs.TryGetValue "os" with
        | true,x -> Some (x.Trim('\"'))
        | _ -> None

    let packagePath = 
        match kvPairs.TryGetValue "packages" with
        | true,x -> Some (x.Trim('\"'))
        | _ -> None

    let url = url.TrimEnd '/'
    let url = 
        match url.Split ' ' |> Array.toList with 
        | [url; commit] -> url
        | _ -> url

    let server =
        let start = 
            match url.IndexOf("://") with
            | -1 -> 8 // 8 = "https://".Length
            | pos -> pos + 3

        match url.Replace(":","/").IndexOf('/', start) with 
        | -1 -> url
        | pos -> url.Substring(0, pos)

                        
    let server = 
        match server.IndexOf("://") with
        | -1 -> server
        | pos -> server.Substring(pos + 3) |> removeInvalidChars
        |> fun s -> s.Replace("git@","")

    let project = url.Substring(url.LastIndexOf('/')+1).Replace(".git","")

    server,commit,project,url,buildCommand,operatingSystemRestriction,packagePath

let getCurrentHash repoFolder = 
    try
        if System.IO.Directory.Exists repoFolder |> not then None else
        Some (CommandHelper.runSimpleGitCommand repoFolder ("rev-parse --verify HEAD"))
    with
    | _ -> None