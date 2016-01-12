module Paket.Git.Handling
open Paket.Utils
open System
open System.IO
open Paket.Logging
open Paket

let extractUrlParts (url:string) =
    let url,commit,options = 
        match url.Split([|' '|],StringSplitOptions.RemoveEmptyEntries) |> List.ofArray with
        | urlPart::_ ->
            let rest = url.Substring(urlPart.Length)
            match rest.Replace(":"," : ").Split([|' '|],StringSplitOptions.RemoveEmptyEntries) |> List.ofArray with
            | k::colon::_ when colon = ":" && (k.ToLower() = "build" || k.ToLower() = "os" || k.ToLower() = "packages") -> 
                urlPart,None,rest
            | operator::commit::prerelease::options when 
                    VersionRange.BasicOperators |> List.exists ((=) operator) && 
                      not (prerelease.ToLower() = "build" || prerelease.ToLower() = "os" || prerelease.ToLower() = "packages") 
               -> 
                let startPos = url.Substring(urlPart.Length).IndexOf(prerelease)
                let options = url.Substring(urlPart.Length + startPos + prerelease.Length)
                urlPart,Some(operator + " " + commit +  " " + prerelease),options
            | operator::commit::options when VersionRange.BasicOperators |> List.exists ((=) operator) -> 
                let startPos = url.Substring(urlPart.Length).IndexOf(commit)
                let options = url.Substring(urlPart.Length + startPos + commit.Length)
                urlPart,Some(operator + " " + commit),options
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
        match url.Replace(":","/").LastIndexOf('/') with 
        | -1 -> url
        | pos -> url.Substring(0, pos)

                        
    let server = 
        match server.IndexOf("://") with
        | -1 -> server
        | pos -> server.Substring(pos + 3).Replace(":","") |> removeInvalidChars
        |> fun s -> s.Replace("git@","").Replace(":","/").TrimStart('/')

    let project = url.Substring(url.LastIndexOf('/')+1).Replace(".git","")

    server,commit,project,url,buildCommand,operatingSystemRestriction,packagePath

let getCurrentHash repoFolder = 
    try
        if System.IO.Directory.Exists repoFolder |> not then None else
        Some (CommandHelper.runSimpleGitCommand repoFolder ("rev-parse --verify HEAD"))
    with
    | _ -> None


let fetchCache repoCacheFolder tempBranchName cloneUrl commit =
    try
        if Directory.Exists repoCacheFolder then
            CommandHelper.runSimpleGitCommand repoCacheFolder ("remote set-url origin " + cloneUrl) |> ignore
            verbosefn "Fetching %s to %s" cloneUrl repoCacheFolder 
            CommandHelper.runSimpleGitCommand repoCacheFolder "fetch -f" |> ignore
        else
            if not <| Directory.Exists Constants.GitRepoCacheFolder then
                Directory.CreateDirectory Constants.GitRepoCacheFolder |> ignore
            tracefn "Cloning %s to %s" cloneUrl repoCacheFolder
            CommandHelper.runSimpleGitCommand Constants.GitRepoCacheFolder ("clone " + cloneUrl) |> ignore
        CommandHelper.runSimpleGitCommand repoCacheFolder ("branch -f " + tempBranchName + " " + commit) |> ignore
    with
    | exn -> failwithf "Updating the git cache at %s failed.%sMessage: %s" repoCacheFolder Environment.NewLine exn.Message

let checkoutToPaketFolder repoFolder cloneUrl cacheCloneUrl commit =
    try
        // checkout to local folder
        if Directory.Exists repoFolder then
            CommandHelper.runSimpleGitCommand repoFolder ("remote set-url origin " + cacheCloneUrl) |> ignore
            verbosefn "Fetching %s to %s" cacheCloneUrl repoFolder 
            CommandHelper.runSimpleGitCommand repoFolder "fetch origin -f" |> ignore
        else
            let destination = DirectoryInfo(repoFolder).Parent.FullName
            if not <| Directory.Exists destination then
                Directory.CreateDirectory destination |> ignore
            verbosefn "Cloning %s to %s" cacheCloneUrl repoFolder
            CommandHelper.runSimpleGitCommand destination ("clone " + cacheCloneUrl) |> ignore
            CommandHelper.runSimpleGitCommand repoFolder ("remote add upstream " + cloneUrl) |> ignore

        tracefn "Setting %s to %s" repoFolder commit
        CommandHelper.runSimpleGitCommand repoFolder ("reset --hard " + commit) |> ignore
    with
    | exn -> failwithf "Checkout to %s failed.%sMessage: %s" repoFolder Environment.NewLine exn.Message