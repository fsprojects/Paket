module Paket.GitHub

open Paket
open Newtonsoft.Json.Linq
open System.IO

// Gets the sha1 of a branch
let getSHA1OfBranch owner project branch = 
    async { 
        let! document = getFromUrl(None,sprintf "https://api.github.com/repos/%s/%s/commits/%s" owner project branch)
        let json = JObject.Parse(document)
        return json.["sha"].ToString()
    }


/// Gets a dependencies file from github.
let downloadDependenciesFile(remoteFile:ResolvedSourceFile) = async {
    let fi = FileInfo(remoteFile.Name)

    let dependenciesFileName = remoteFile.Name.Replace(fi.Name,"paket.dependencies")

    let url = sprintf "https://github.com/%s/%s/raw/%s/%s" remoteFile.Owner remoteFile.Project remoteFile.Commit dependenciesFileName
    let! result = safeGetFromUrl(None,url)
    match result with
    | Some text -> return text
    | None -> return "" }

/// Gets a single file from github.
let downloadSourceFile remoteFile = getFromUrl(None,sprintf "https://github.com/%s/%s/raw/%s/%s" remoteFile.Owner remoteFile.Project remoteFile.Commit remoteFile.Name)