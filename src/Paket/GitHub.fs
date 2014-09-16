module Paket.GitHub

open Paket

/// Gets a single file from github.
let downloadFile remoteFile =    
    sprintf "https://github.com/%s/%s/raw/%s/%s" remoteFile.Owner remoteFile.Project remoteFile.CommitWithDefault remoteFile.Path
    |> getFromUrl