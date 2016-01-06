module Paket.Git.Handling
open Paket.Utils

let extractUrlParts (url:string) =
    let url = url.TrimEnd '/'
    let url,commit = 
        match url.Split ' ' |> Array.toList with 
        | [url; commit] -> url, Some commit
        | _ -> url, None

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

    server,commit,project,url

let getCurrentHash repoFolder = 
    try
        if System.IO.Directory.Exists repoFolder |> not then None else
        Some (CommandHelper.runSimpleGitCommand repoFolder ("rev-parse --verify HEAD"))
    with
    | _ -> None