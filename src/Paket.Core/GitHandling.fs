module Paket.Git.Handling
open Paket.Utils

let extractUrlParts (url:string) =
    let url = url.TrimEnd '/'
    let projectSpec', commit =
        let start = 
            match url.IndexOf("://") with
            | -1 -> 8 // 8 = "https://".Length
            | pos -> pos + 3
                                                
        match url.Replace(":","/").IndexOf('/', start) with 
        | -1 -> url, "/"
        | pos -> url.Substring(0, pos), url.Substring(pos)
                        
    let owner = 
        match projectSpec'.IndexOf("://") with
        | -1 -> projectSpec'
        | pos -> projectSpec'.Substring(pos + 3) |> removeInvalidChars
        |> fun s -> s.Replace("git@","")

    let project = url.Substring(url.LastIndexOf('/')+1).Replace(".git","")

    owner,commit,project,url

let getCurrentHash repoFolder = 
    try
        if System.IO.Directory.Exists repoFolder |> not then None else
        Some (CommandHelper.runSimpleGitCommand repoFolder ("rev-parse head"))
    with
    | _ -> None