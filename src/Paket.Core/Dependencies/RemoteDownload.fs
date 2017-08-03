module Paket.RemoteDownload

open Paket
open Newtonsoft.Json.Linq
open System
open System.IO
open Paket.Logging
open Paket.ModuleResolver
open System.IO.Compression
open Pri.LongPath
open Paket.Domain
open Paket.Git.CommandHelper
open Paket.Git.Handling


let private safeGetFromUrlCached = memoizeAsync <| safeGetFromUrl

let private lookupDocument (auth,url : string)  = 
    safeGetFromUrlCached(auth,url,null)

let private auth key url = 
    key
    |> Option.bind (fun key -> ConfigFile.GetAuthenticationForUrl(key,url))


// Gets the sha1 of a branch
let getSHA1OfBranch origin owner project (versionRestriction:VersionRestriction) authKey = 
    async { 
        match origin with
        | ModuleResolver.Origin.GitHubLink -> 
            let branch = ModuleResolver.getVersionRequirement versionRestriction
            let url = sprintf "https://api.github.com/repos/%s/%s/commits/%s" owner project branch
            let! document = lookupDocument(auth authKey url,url)
            match document with
            | SuccessResponse (document) ->
                let json = JObject.Parse(document)
                return json.["sha"].ToString()
            | NotFound ->
                return raise <| new Exception(sprintf "Could not find (404) hash for %s" url)
            | UnknownError err ->
                return raise <| new Exception(sprintf "Could not find hash for %s" url, err.SourceException)
        | ModuleResolver.Origin.GistLink ->
            let branch = ModuleResolver.getVersionRequirement versionRestriction
            let url = sprintf "https://api.github.com/gists/%s/%s" project branch
            let! document = lookupDocument(auth authKey url,url)
            match document with
            | SuccessResponse document ->
                let json = JObject.Parse(document)
                let latest = json.["history"].First.["version"]
                return latest.ToString()
            | NotFound ->
                return raise <| new Exception(sprintf "Could not find hash for %s" url)
            | UnknownError err ->
                return raise <| new Exception(sprintf "Could not find hash for %s" url, err.SourceException)
        | ModuleResolver.Origin.GitLink (LocalGitOrigin path) ->
            let path = path.Replace(@"file:///", "")
            let branch = 
                match versionRestriction with
                | VersionRestriction.NoVersionRestriction -> "master"
                | VersionRestriction.Concrete branch      -> branch
                | _ -> failwith "unexpected version restriction"
            
            match Git.Handling.getHash path branch with
                | Some hash -> return hash
                | None -> return failwithf "Could not find hash for %s in '%s'" branch path
        | ModuleResolver.Origin.GitLink (RemoteGitOrigin url) ->
            return
                match versionRestriction with
                | VersionRestriction.NoVersionRestriction -> Git.Handling.getHashFromRemote url ""
                | VersionRestriction.Concrete branch -> Git.Handling.getHashFromRemote url branch
                | VersionRestriction.VersionRequirement vr -> 
                    let repoCacheFolder = Path.Combine(Constants.GitRepoCacheFolder,project)
                    Paket.Git.Handling.fetchCache repoCacheFolder url

                    let tags = Git.CommandHelper.runFullGitCommand repoCacheFolder "tag"
                    let matchingVersions =
                        tags
                        |> Array.choose (fun s -> try Some(SemVer.Parse s) with | _ -> None)
                        |> Array.filter vr.IsInRange

                    match matchingVersions with
                    | [||] -> failwithf "No tags in %s match %O. Tags: %A" url vr tags
                    | _ -> 
                        let tag = matchingVersions |> Array.max |> string 
                        match Git.Handling.getHash repoCacheFolder tag with
                        | None -> failwithf "Could not resolve hash for tag %s in %s." tag url
                        | Some hash -> hash

        | ModuleResolver.Origin.HttpLink _ -> return ""
    }

let private rawFileUrl owner project branch fileName =
    sprintf "https://raw.githubusercontent.com/%s/%s/%s/%s" owner project branch fileName

let private rawGistFileUrl owner project fileName =
    sprintf "https://gist.githubusercontent.com/%s/%s/raw/%s" owner project fileName

/// Gets a dependencies file from the remote source and tries to parse it.
let downloadDependenciesFile(force,rootPath,groupName,parserF,remoteFile:ModuleResolver.ResolvedSourceFile) = async {
    match remoteFile.Origin with
    | ModuleResolver.GitLink _ -> return parserF ""
    | _ ->
        let fi = FileInfo(remoteFile.Name)

        let dependenciesFileName = remoteFile.Name.Replace(fi.Name,Constants.DependenciesFileName)
        let destination = FileInfo(remoteFile.ComputeFilePath(rootPath,groupName,dependenciesFileName))

        let url = 
            match remoteFile.Origin with
            | ModuleResolver.GitHubLink -> 
                rawFileUrl remoteFile.Owner remoteFile.Project remoteFile.Commit dependenciesFileName
            | ModuleResolver.GistLink -> 
                rawGistFileUrl remoteFile.Owner remoteFile.Project dependenciesFileName
            | ModuleResolver.HttpLink url -> 
                url.Replace(remoteFile.Name,Constants.DependenciesFileName)
            | ModuleResolver.GitLink _ -> failwithf "Can't compute dependencies file url for %O" remoteFile

        let auth = 
            try
                remoteFile.AuthKey
                |> Option.bind (fun key -> ConfigFile.GetAuthenticationForUrl(key,url))
            with
            | _ -> None
  
        let exists =
            let di = destination.Directory
            let versionFile = FileInfo(Path.Combine(di.FullName, Constants.PaketVersionFileName))
            not force &&
              not (String.IsNullOrWhiteSpace remoteFile.Commit) && 
              destination.Exists &&
              versionFile.Exists && 
              File.ReadAllText(versionFile.FullName).Contains(remoteFile.Commit)

    
        if exists then
            return parserF (File.ReadAllText(destination.FullName))
        else
            let! text,depsFile = async {
                // TODO: Fixme, something is wrong on testcase #1341
                if url <> "file://" then
                    let! result = lookupDocument(auth,url)

                    match result with
                    | SuccessResponse text ->
                            try
                                return text,parserF text
                            with
                            | _ -> return  "",parserF ""
                    | NotFound -> return "", parserF ""
                    | UnknownError e ->
                        Logging.traceWarnfn "Error while retrieving '%s': %O" url e.SourceException
                        return  "",parserF ""
                else
                    Logging.traceWarnfn "Fixme #1341, proper search for dependencies file when using 'http file:///'"
                    return "",parserF "" }

            Directory.CreateDirectory(destination.FullName |> Path.GetDirectoryName) |> ignore
            File.WriteAllText(destination.FullName, text)

            return depsFile }


let rec DirectoryCopy(sourceDirName, destDirName, copySubDirs) =
    let dir = DirectoryInfo(sourceDirName)
    let dirs = dir.GetDirectories()

    if not <| Directory.Exists(destDirName) then
        Directory.CreateDirectory(destDirName) |> ignore

    for file in dir.GetFiles() do
        file.CopyTo(Path.Combine(destDirName, file.Name), true) |> ignore

    // If copying subdirectories, copy them and their contents to new location. 
    if copySubDirs then
        for subdir in dirs do
            DirectoryCopy(subdir.FullName, Path.Combine(destDirName, subdir.Name), copySubDirs)

/// Retrieves RemoteFiles
let downloadRemoteFiles(remoteFile:ResolvedSourceFile,destination) = async {
    let targetFolder = FileInfo(destination).Directory
    if not targetFolder.Exists then
        targetFolder.Create()

    match remoteFile.Origin, remoteFile.Name with
    | Origin.GitLink (RemoteGitOrigin cloneUrl), _
    | Origin.GitLink (LocalGitOrigin cloneUrl), _ ->
        if not <| Utils.isMatchingPlatform remoteFile.OperatingSystemRestriction then () else
        let cloneUrl = cloneUrl.TrimEnd('/')
        
        let repoCacheFolder = Path.Combine(Constants.GitRepoCacheFolder,remoteFile.Project)
        let repoFolder = Path.Combine(destination,remoteFile.Project)
        let cacheCloneUrl = "file:///" + repoCacheFolder

        Paket.Git.Handling.fetchCache repoCacheFolder cloneUrl
        Paket.Git.Handling.checkoutToPaketFolder repoFolder cloneUrl cacheCloneUrl remoteFile.Commit

        match remoteFile.Command with
        | None -> ()
        | Some command ->
            
            let command,args =
                match command.IndexOf ' ' with
                | -1 -> command,""
                | p -> command.Substring(0,p),command.Substring(p+1)
            
            let command = 
                if Path.IsPathRooted command then command else
                let p = Path.Combine(repoFolder,command)
                if File.Exists p then p else command
            let tCommand = if String.IsNullOrEmpty args then command else command + " " + args

            try
                tracefn "Running \"%s\"" tCommand
                let processResult = 
                    ExecProcessAndReturnMessages (fun info ->
                        info.FileName <- command
                        info.WorkingDirectory <- repoFolder
                        info.Arguments <- args) gitTimeOut

                let ok,msg,errors = processResult.OK,processResult.Messages,toLines processResult.Errors
               
                let errorText = toLines msg + Environment.NewLine + errors
                if not ok then failwith errorText
                if ok && msg.Count = 0 then tracefn "Done." else
                if verbose then
                    msg |> Seq.iter (tracefn "%s")
            with 
            | exn -> failwithf "Could not run \"%s\".\r\nError: %s" tCommand exn.Message
    | Origin.GistLink, Constants.FullProjectSourceFileName ->
        let fi = FileInfo(destination)
        let projectPath = fi.Directory.FullName

        let url = sprintf "https://api.github.com/gists/%s/%s" remoteFile.Project remoteFile.Commit
        let authentication = auth remoteFile.AuthKey url
        let! document = getFromUrl(authentication, url, null)
        let json = JObject.Parse(document)
        let files = json.["files"] |> Seq.map (fun i -> i.First.["filename"].ToString(), i.First.["raw_url"].ToString())

        let task = 
            files |> Seq.map (fun (filename, url) -> 
                async {
                    let path = Path.Combine(projectPath,filename)
                    do! downloadFromUrl(None, url) path
                } 
            ) |> Async.Parallel
        task |> Async.RunSynchronously |> ignore
    | Origin.GitHubLink, Constants.FullProjectSourceFileName -> 
        let fi = FileInfo(destination)
        let projectPath = fi.Directory.FullName
        let zipFile = Path.Combine(projectPath,sprintf "%s.zip" remoteFile.Commit)
        let downloadUrl = sprintf "https://github.com/%s/%s/archive/%s.zip" remoteFile.Owner remoteFile.Project remoteFile.Commit
        let authentication = auth remoteFile.AuthKey downloadUrl
        CleanDir projectPath
        do! downloadFromUrl(authentication, downloadUrl) zipFile
        ZipFile.ExtractToDirectory(zipFile,projectPath)

        let source = Path.Combine(projectPath, sprintf "%s-%s" remoteFile.Project remoteFile.Commit)
        DirectoryCopy(source,projectPath,true)
    | Origin.GistLink, _ -> 
        let downloadUrl = rawGistFileUrl remoteFile.Owner remoteFile.Project remoteFile.Name
        let authentication = auth remoteFile.AuthKey downloadUrl
        return! downloadFromUrl(authentication, downloadUrl) destination
    | Origin.GitHubLink, _ ->
        let url = rawFileUrl remoteFile.Owner remoteFile.Project remoteFile.Commit remoteFile.Name
        let authentication = auth remoteFile.AuthKey url
        return! downloadFromUrl(authentication, url) destination
    | Origin.HttpLink(origin), _ ->
        let url = origin + remoteFile.Commit
        let authentication = auth remoteFile.AuthKey url
        match Path.GetExtension(destination).ToLowerInvariant() with
        | ".zip" ->
            do! downloadFromUrl(authentication, url) destination
            ZipFile.ExtractToDirectory(destination, targetFolder.FullName)
        | _ -> do! downloadFromUrl(authentication, url) destination
}

let DownloadSourceFiles(rootPath, groupName, force, sourceFiles:ModuleResolver.ResolvedSourceFile list) =
    let remoteFiles,gitRepos = 
        sourceFiles
        |> List.partition (fun x -> match x.Origin with | GitLink _ -> false | _ -> true)

    gitRepos
    |> List.map (fun gitRepo ->
        async {
            let repoFolder = gitRepo.FilePath(rootPath,groupName)
            let destination = DirectoryInfo(repoFolder).Parent.FullName

            let isInCorrectVersion =
                if force then false else
                match Git.Handling.getCurrentHash repoFolder with
                | Some hash -> 
                    match gitRepo.Command, gitRepo.PackagePath with
                    | Some _, Some path when not (DirectoryInfo(repoFolder + path).Exists) ->
                        false
                    | _ ->
                        hash = gitRepo.Commit
                | None -> 
                    // something is wrong with the repo
                    Utils.deleteDir (DirectoryInfo repoFolder)
                    false

            if isInCorrectVersion then
                if verbose then
                    verbosefn "%s is already up-to-date." repoFolder
            else
                do! downloadRemoteFiles(gitRepo,destination) 
        })
    |> Async.Parallel
    |> Async.RunSynchronously
    |> ignore

    remoteFiles
    |> List.map (fun source ->
        let destination = source.FilePath(rootPath,groupName)
        let destinationDir = FileInfo(destination).Directory.FullName

        (destinationDir, source.Commit), (destination, source))
    |> List.groupBy fst
    |> List.sortBy (fst >> fst)
    |> List.map (fun ((destinationDir, version), sources) ->
        let versionFile = FileInfo(Path.Combine(destinationDir, Constants.PaketVersionFileName))
        let isInRightVersion = versionFile.Exists && File.ReadAllText(versionFile.FullName).Contains(version)

        if not isInRightVersion then
            CleanDir destinationDir

        (versionFile, version), sources)
    |> List.iter (fun ((versionFile, version), sources) ->
        sources
        |> List.iter (fun (_, (destination, source)) ->
            let exists =
                if destination.EndsWith Constants.FullProjectSourceFileName then
                    let di = FileInfo(destination).Directory
                    di.Exists && FileInfo(Path.Combine(di.FullName, Constants.PaketVersionFileName)).Exists
                else
                    File.Exists destination

            if not force && exists then
                if verbose then
                    verbosefn "Sourcefile %O is already there." source
            else 
                tracefn "Downloading %O to %s" source destination
                Async.RunSynchronously <| downloadRemoteFiles(source,destination))

        if File.Exists(versionFile.FullName) then
            if not <| File.ReadAllText(versionFile.FullName).Contains(version) then
                File.AppendAllLines(versionFile.FullName, [version])
        else
            File.AppendAllLines(versionFile.FullName, [version]))
