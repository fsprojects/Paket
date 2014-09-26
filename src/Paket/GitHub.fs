module Paket.GitHub

open Paket
open Newtonsoft.Json.Linq

// Gets the sha1 of a branch
let getSHA1OfBranch owner project branch = 
    async { 
        let! document = getFromUrl(None,sprintf "https://api.github.com/repos/%s/%s/commits/%s" owner project branch)
        let json = JObject.Parse(document)
        return json.["sha"].ToString()
    }

/// Gets a single file from github.
let downloadFile remoteFile =
    match remoteFile.Commit with
    | Some commit -> getFromUrl(None,sprintf "https://github.com/%s/%s/raw/%s/%s" remoteFile.Owner remoteFile.Project commit remoteFile.Name)
    | None -> failwith "Can't download %s. No commit specified" (remoteFile.ToString())