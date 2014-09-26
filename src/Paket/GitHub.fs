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
let downloadDependenciesFile remoteFile = async {
    match remoteFile.Commit with
    | Some commit -> 
        let fi = FileInfo(remoteFile.Name)

        let dependenciesFileName = remoteFile.Name.Replace(fi.Name,"paket.dependencies")

        let url = sprintf "https://github.com/%s/%s/raw/%s/%s" remoteFile.Owner remoteFile.Project commit dependenciesFileName
        let! result = safeGetFromUrl(None,url)
        match result with
        | Some text -> return text
        | None -> return ""
    | None -> return "" }

/// Gets a single file from github.
let downloadSourceFile remoteFile =
    match remoteFile.Commit with
    | Some commit -> getFromUrl(None,sprintf "https://github.com/%s/%s/raw/%s/%s" remoteFile.Owner remoteFile.Project commit remoteFile.Name)
    | None -> failwith "Can't download %s. No commit specified" (remoteFile.ToString())