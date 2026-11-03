module Paket.Git.Handling
open Paket.Utils
open System
open System.IO
open Paket.Logging
open Paket

let paketCheckoutTag = "paket/lock"

type GitLinkOrigin =
| RemoteGitOrigin of string
| LocalGitOrigin  of string

/// The path of a local git repository from its file:// URL: "file:///tmp/x" is "/tmp/x",
/// "file:///C:/x" the Windows path "C:/x", and "file:///tmp/a%20b" is "/tmp/a b"
let internal localPathOfFileUrl (url: string) =
    let path =
        match url with
        | String.RemovePrefix "file://" path -> Uri.UnescapeDataString path
        | _ -> url
    if path.Length >= 3 && path.[0] = '/' && Char.IsAsciiLetter path.[1] && path.[2] = ':' then path.Substring 1
    else path

let extractUrlParts (gitConfig:string) =
    let isOperator operator = VersionRange.BasicOperators |> List.exists ((=) operator)
    let url,commit,options =
        match gitConfig.Split([|' '|],StringSplitOptions.RemoveEmptyEntries) |> List.ofArray with
        | part::_ ->
            let rest = gitConfig.Substring(part.Length)
            let parts =
                [ let current = Text.StringBuilder()
                  let mutable quoted = false
                  for x in rest do
                    if x = '"' then
                        quoted <- not quoted

                    if (x = ':' || x = ' ') && not quoted then
                        yield current.ToString()
                        if x = ':' then
                            yield ":"
                        current.Clear() |> ignore
                    else
                        current.Append x |> ignore

                  yield current.ToString()]
                |> List.filter (String.IsNullOrWhiteSpace >> not)

            match parts with
            | k::colon::_ when colon = ":" && (k.ToLower() = "build" || k.ToLower() = "os" || k.ToLower() = "packages")  ->
                part,None,rest
            | operator::version::operator2::version2::prerelease::options when
                    isOperator operator && isOperator operator2 &&
                      not (prerelease.ToLower() = "build" || prerelease.ToLower() = "os" || prerelease.ToLower() = "packages" || prerelease = ":")
               ->
                let startPos = gitConfig.Substring(part.Length).IndexOf(prerelease)
                let options = gitConfig.Substring(part.Length + startPos + prerelease.Length)
                part,Some(operator + " " + version + " " + operator2 + " " + version2 +  " " + prerelease),options
            | operator::version::prerelease::options when
                    isOperator operator && not (isOperator prerelease) &&
                      not (prerelease.ToLower() = "build" || prerelease.ToLower() = "os" || prerelease.ToLower() = "packages" || prerelease = ":")
               ->
                let startPos = gitConfig.Substring(part.Length).IndexOf(prerelease)
                let options = gitConfig.Substring(part.Length + startPos + prerelease.Length)
                part,Some(operator + " " + version +  " " + prerelease),options
            | operator::version::operator2::version2::options when isOperator operator && isOperator operator2 ->
                let startPos = gitConfig.Substring(part.Length).IndexOf(version2)
                let options = gitConfig.Substring(part.Length + startPos + version2.Length)
                part,Some(operator + " " + version + " " + operator2 + " " + version2),options
            | operator::version::options when isOperator operator ->
                let startPos = gitConfig.Substring(part.Length).IndexOf(version)
                let options = gitConfig.Substring(part.Length + startPos + version.Length)
                part,Some(operator + " " + version),options
            | commit::options ->
                let startPos = gitConfig.Substring(part.Length).IndexOf(commit)
                let options = gitConfig.Substring(part.Length + startPos + commit.Length)
                part,Some commit,options
            | _ -> gitConfig,None,""
        | _ -> gitConfig,None,""

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

    let origin =
        match url with
        | String.RemovePrefix @"file:///" _ ->
            LocalGitOrigin url
        | _ ->
            RemoteGitOrigin url

    let server =
        match origin with
        | LocalGitOrigin _ ->
            "localfilesystem"
        | _ ->
            match url.Replace(":","/").LastIndexOf('/') with
            | -1 -> url
            | pos -> url.Substring(0, pos)

    let server =
        match server.IndexOf("://") with
        | -1 -> server
        | pos -> server.Substring(pos + 3).Replace(":","") |> removeInvalidChars
        |> fun s -> s.Replace("git@","").Replace(":","/").TrimStart('/')

    let project =
        match origin with
        | LocalGitOrigin _ ->
            // the folder name, from a Windows or a Unix path, even one that ends with a separator or doesn't exist
            let path = (localPathOfFileUrl url).TrimEnd('/', '\\')
            path.Substring(path.LastIndexOfAny [| '/'; '\\' |] + 1).Replace(".git","")
        | RemoteGitOrigin _ ->
            let project = url.Substring(url.LastIndexOf('/')+1).Replace(".git","")
            if Directory.Exists project then Path.GetFileName project else project

    server,commit,project,origin,buildCommand,operatingSystemRestriction,packagePath

let getHash repoFolder commitish =
    try
        if System.IO.Directory.Exists repoFolder |> not then None else
        Some (CommandHelper.runSimpleGitCommand repoFolder ("rev-parse --verify " + commitish))
    with
    | _ -> None

let getCurrentHash repoFolder =
    getHash repoFolder "HEAD"

/// The commit checked out in the paket-files clone at repoFolder: None when it has no checkout, as a clone
/// that a run left without one, whose HEAD names a commit that none of its files are from
let getCheckedOutCommit repoFolder =
    let gitPath = Path.Combine(repoFolder, ".git")
    // only the clone --no-checkout of a run, whose .git is a folder, has no index
    let hasCheckout = File.Exists gitPath || File.Exists(Path.Combine(gitPath, "index"))
    if hasCheckout then getCurrentHash repoFolder else None

let getHashFromRemote url branch =
    let result =
        let hash = CommandHelper.runSimpleGitCommand "" (sprintf "ls-remote %s %s" url branch)
        if String.IsNullOrWhiteSpace hash then
            branch
        else
            hash
    if result.Contains "\t" then
        result.Substring(0,result.IndexOf '\t')
    else
        if result.Contains " " then
            result.Substring(0,result.IndexOf ' ')
        else
            result


let fetchCache repoCacheFolder cloneUrl =
    try
        // RunInLockedAccessMode has already created the folder for its lock file, and "clone --mirror"
        // refuses a folder that isn't empty: the mirror is set up in place
        Directory.CreateDirectory repoCacheFolder |> ignore
        let git command = CommandHelper.runSimpleGitCommand repoCacheFolder command |> ignore
        if not (Directory.Exists(Path.Combine(repoCacheFolder, "objects"))) then
            git "init --bare"

        // a cache left without origin by a failed run is set up again
        let hasOrigin = CommandHelper.runFullGitCommand repoCacheFolder "remote" |> Array.contains "origin"
        if not hasOrigin then
            tracefn "Cloning %s to %s" cloneUrl repoCacheFolder
            git ("remote add --mirror=fetch origin " + quote cloneUrl)
        else
            git ("remote set-url origin " + quote cloneUrl)
            if verbose then
                verbosefn "Fetching %s to %s" cloneUrl repoCacheFolder

        git "remote update --prune"
    with
    | exn -> failwithf "Fetching the git cache at %s failed.%sMessage: %s" repoCacheFolder Environment.NewLine exn.Message

let checkForUncommittedChanges repoFolder =
    try
        tracefn "Checking for uncommitted changes in %s" repoFolder
        CommandHelper.gitCommand repoFolder "diff-index --quiet HEAD --" |> ignore
    with
    | exn -> failwithf "It seems there are uncommitted changes in the repository. The changes must be committed/discarded first.%sMessage: %s" Environment.NewLine exn.Message

let checkForCommitsMadeInDetachedHeadState repoFolder =
    try
        tracefn "Checking for commits made in detached HEAD state in %s" repoFolder
        CommandHelper.gitCommand repoFolder "name-rev --no-undefined HEAD --" |> ignore
    with
    | exn -> failwithf "It seems that some commits would become unreachable after checkout. Create branch for the commits or reset them first and try again.%sMessage: %s" Environment.NewLine exn.Message

let tagCommitForCheckout repoFolder commit =
    try
        CommandHelper.gitCommand repoFolder (sprintf "tag -f %s %s" paketCheckoutTag commit)
    with
    | exn -> failwithf "Updating the git cache at %s failed.%sMessage: %s" repoFolder Environment.NewLine exn.Message

// git checkout reports its failures with "error:", which runSimpleGitCommand doesn't see: the exit code tells
let checkoutTaggedCommit repoFolder =
    CommandHelper.gitCommand repoFolder ("checkout " + paketCheckoutTag)

let checkoutToPaketFolder repoFolder cloneUrl cacheCloneUrl commit =
    let mutable cloned = false
    try
        // only a checkout can hold changes that the checkout of the commit would lose
        let hasCheckout = (getCheckedOutCommit repoFolder).IsSome
        // checkout to local folder
        if Directory.Exists repoFolder then
            if verbose then
                verbosefn "Fetching %s to %s" cacheCloneUrl repoFolder
            CommandHelper.runSimpleGitCommand repoFolder (sprintf "fetch --tags --prune %s +refs/heads/*:refs/remotes/origin/*" (quote cacheCloneUrl)) |> ignore
        else
            let destination = DirectoryInfo(repoFolder).Parent.FullName
            if not (Directory.Exists destination) then
                Directory.CreateDirectory destination |> ignore
            if verbose then
                verbosefn "Cloning %s to %s" cacheCloneUrl repoFolder
            // the HEAD of the cache may name a branch that it doesn't have: the commit is checked out below
            CommandHelper.runSimpleGitCommand destination (sprintf "clone --no-checkout %s %s" (quote cacheCloneUrl) (quote repoFolder)) |> ignore
            cloned <- true
            CommandHelper.runSimpleGitCommand repoFolder (sprintf "remote set-url origin %s" (quote cloneUrl)) |> ignore

        if hasCheckout then
            checkForUncommittedChanges repoFolder
            checkForCommitsMadeInDetachedHeadState repoFolder

        tracefn "Setting %s to %s" repoFolder commit
        tagCommitForCheckout repoFolder commit
        checkoutTaggedCommit repoFolder

    with
    | exn ->
        // the next run clones again, rather than finding a clone without checkout
        if cloned then
            try deleteDir (DirectoryInfo repoFolder) with _ -> ()
        failwithf "Checkout to %s failed.%sMessage: %s" repoFolder Environment.NewLine exn.Message