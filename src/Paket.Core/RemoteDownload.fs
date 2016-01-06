module Paket.RemoteDownload

open Paket
open Newtonsoft.Json.Linq
open System
open System.IO
open Paket.Logging
open Paket.ModuleResolver
open System.IO.Compression
open Paket.Domain

let private githubCache = System.Collections.Concurrent.ConcurrentDictionary<_, _>()

let private lookupDocument (auth,url : string)  = async {
    let key = auth,url
    match githubCache.TryGetValue key with
    | true, document -> return document
    | _ ->
        let! document = safeGetFromUrl(auth, url, null)
        githubCache.TryAdd(key, document) |> ignore
        return document
    }

let private auth key url = 
    key
    |> Option.bind (fun key -> ConfigFile.GetAuthenticationForUrl key url)


// Gets the sha1 of a branch
let getSHA1OfBranch origin owner project branch authKey = 
    async { 
        match origin with
        | ModuleResolver.Origin.GitHubLink -> 
            let url = sprintf "https://api.github.com/repos/%s/%s/commits/%s" owner project branch
            let! document = lookupDocument(auth authKey url,url)
            match document with
            | Some document ->
                let json = JObject.Parse(document)
                return json.["sha"].ToString()
            | None -> 
                failwithf "Could not find hash for %s" url
                return ""
        | ModuleResolver.Origin.GistLink ->
            let url = sprintf "https://api.github.com/gists/%s/%s" project branch
            let! document = lookupDocument(auth authKey url,url)
            match document with
            | Some document ->
                let json = JObject.Parse(document)
                let latest = json.["history"].First.["version"]
                return latest.ToString()
            | None -> 
                failwithf "Could not find hash for %s" url
                return ""
        | ModuleResolver.Origin.GitLink url ->
            let result =
                sprintf "ls-remote %s %s" url branch
                |> Git.CommandHelper.runSimpleGitCommand ""

            if String.IsNullOrWhiteSpace result then 
                return branch
            else
                if result.Contains "\t" then
                    return result.Substring(0,result.IndexOf '\t')
                else
                    if result.Contains " " then
                        return result.Substring(0,result.IndexOf ' ')
                    else
                        return result
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
            remoteFile.AuthKey
            |> Option.bind (fun key -> ConfigFile.GetAuthenticationForUrl key url)
            |> function
            | Some(Credentials(_,_)) as credentials -> credentials
            | auth -> auth
  
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
            let! result = lookupDocument(auth,url)

            let text,depsFile =
                match result with
                | Some text -> 
                        try
                            text,parserF text
                        with 
                        | _ -> "",parserF ""
                | _ -> "",parserF ""

            Directory.CreateDirectory(destination.FullName |> Path.GetDirectoryName) |> ignore
            File.WriteAllText(destination.FullName, text)

            return depsFile }


let ExtractZip(fileName : string, targetFolder) = 
    Directory.CreateDirectory(targetFolder) |> ignore
    ZipFile.ExtractToDirectory(fileName,targetFolder)

let rec DirectoryCopy(sourceDirName, destDirName, copySubDirs) =
    let dir = new DirectoryInfo(sourceDirName)
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
    match remoteFile.Origin, remoteFile.Name with
    | Origin.GitLink cloneUrl, _ -> 
        let cloneUrl = cloneUrl.TrimEnd('/')
        
        let repoCacheFolder = Path.Combine(Constants.GitRepoCacheFolder,remoteFile.Project)

        // clone/fetch to cache
        if Directory.Exists repoCacheFolder then
            verbosefn "Fetching %s to %s" cloneUrl repoCacheFolder 
            Git.CommandHelper.runSimpleGitCommand repoCacheFolder ("fetch " + cloneUrl) |> ignore
        else
            if not <| Directory.Exists Constants.GitRepoCacheFolder then
                Directory.CreateDirectory Constants.GitRepoCacheFolder |> ignore
            tracefn "Cloning %s to %s" cloneUrl repoCacheFolder
            Git.CommandHelper.runSimpleGitCommand Constants.GitRepoCacheFolder ("clone " + cloneUrl) |> ignore

        let repoFolder = Path.Combine(destination,remoteFile.Project)
        let cacheCloneUrl = "file:///" + repoCacheFolder

        // checkout to local folder
        if Directory.Exists repoFolder then
            verbosefn "Fetching %s to %s" cacheCloneUrl repoFolder 
            Git.CommandHelper.runSimpleGitCommand repoFolder ("fetch " + cacheCloneUrl) |> ignore
        else
            if not <| Directory.Exists destination then
                Directory.CreateDirectory destination |> ignore
            verbosefn "Cloning %s to %s" cacheCloneUrl repoFolder
            Git.CommandHelper.runSimpleGitCommand destination ("clone " + cacheCloneUrl) |> ignore

        tracefn "Setting %s to %s" repoFolder remoteFile.Commit
        Git.CommandHelper.runSimpleGitCommand repoFolder ("reset --hard " + remoteFile.Commit) |> ignore
    | Origin.GistLink, Constants.FullProjectSourceFileName ->
        tracefn "Downloading %O to %s" remoteFile destination
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

        // GIST currently does not support zip-packages, so now this fetches all files separately.
        // let downloadUrl = sprintf "https://gist.github.com/%s/%s/download" remoteFile.Owner remoteFile.Project //is a tar.gz

    | Origin.GitHubLink, Constants.FullProjectSourceFileName -> 
        tracefn "Downloading %O to %s" remoteFile destination
        let fi = FileInfo(destination)
        let projectPath = fi.Directory.FullName
        let zipFile = Path.Combine(projectPath,sprintf "%s.zip" remoteFile.Commit)
        let downloadUrl = sprintf "https://github.com/%s/%s/archive/%s.zip" remoteFile.Owner remoteFile.Project remoteFile.Commit
        let authentication = auth remoteFile.AuthKey downloadUrl
        do! downloadFromUrl(authentication, downloadUrl) zipFile

        ExtractZip(zipFile,projectPath)

        let source = Path.Combine(projectPath, sprintf "%s-%s" remoteFile.Project remoteFile.Commit)
        DirectoryCopy(source,projectPath,true)
    | Origin.GistLink, _ -> 
        tracefn "Downloading %O to %s" remoteFile destination
        let downloadUrl = rawGistFileUrl remoteFile.Owner remoteFile.Project remoteFile.Name
        let authentication = auth remoteFile.AuthKey downloadUrl
        return! downloadFromUrl(authentication, downloadUrl) destination
    | Origin.GitHubLink, _ ->
        tracefn "Downloading %O to %s" remoteFile destination
        let url = rawFileUrl remoteFile.Owner remoteFile.Project remoteFile.Commit remoteFile.Name
        let authentication = auth remoteFile.AuthKey url
        return! downloadFromUrl(authentication, url) destination
    | Origin.HttpLink(origin), _ ->
        tracefn "Downloading %O to %s" remoteFile destination
        let url = origin + remoteFile.Commit
        let authentication = auth remoteFile.AuthKey url
        do! downloadFromUrl(authentication, url) destination
        match Path.GetExtension(destination).ToLowerInvariant() with
        | ".zip" ->
            let targetFolder = FileInfo(destination).Directory.FullName
            ExtractZip(destination, targetFolder)
        | _ -> ignore()
}

let DownloadSourceFiles(rootPath, groupName, force, sourceFiles:ModuleResolver.ResolvedSourceFile list) =
    let remoteFiles,gitRepos = 
        sourceFiles
        |> List.partition (fun x -> match x.Origin with | GitLink _ -> false | _ -> true)

    let gitDownloads =
        gitRepos
        |> List.map (fun gitRepo ->
            async {
                let repoFolder = gitRepo.FilePath(rootPath,groupName)
                let destination = DirectoryInfo(repoFolder).Parent.FullName

                let isInCorrectVersion =
                    if force then false else
                    match Git.Handling.getCurrentHash repoFolder with
                    | Some hash -> hash = gitRepo.Commit
                    | None -> 
                        // something is wrong with the repo
                        Utils.deleteDir (DirectoryInfo repoFolder)
                        false

                if isInCorrectVersion then
                    verbosefn "%s is already up-to-date." repoFolder
                else
                    do! downloadRemoteFiles(gitRepo,destination) 
            })

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
    |> List.map (fun ((versionFile, version), sources) ->
        async {
            let! downloaded =
                sources
                |> List.map (fun (_, (destination, resolvedSourceFile)) ->
                    async {
                        let exists =
                            if destination.EndsWith Constants.FullProjectSourceFileName then
                                let di = FileInfo(destination).Directory
                                di.Exists && FileInfo(Path.Combine(di.FullName, Constants.PaketVersionFileName)).Exists
                            else
                                File.Exists destination

                        if not force && exists then
                            verbosefn "Sourcefile %O is already there." resolvedSourceFile
                        else 
                            do! downloadRemoteFiles(resolvedSourceFile,destination)
                    })
                |> Async.Parallel

            if File.Exists(versionFile.FullName) then
                if not <| File.ReadAllText(versionFile.FullName).Contains(version) then
                    File.AppendAllLines(versionFile.FullName, [version])
            else
                File.AppendAllLines(versionFile.FullName, [version])
        })
    |> List.append gitDownloads
    |> Async.Parallel