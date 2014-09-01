[<AutoOpen>]
module Paket.Utils

open System
open System.IO

let monitor = new Object()

let trace (s:string) = lock monitor (fun () -> printfn "%s" s)

let tracefn fmt = Printf.ksprintf trace fmt

/// Creates a directory if it does not exist.
let CreateDir path = 
    let dir = DirectoryInfo path
    if not dir.Exists then 
        dir.Create()

/// Cleans a directory by removing all files and sub-directories.
let CleanDir path = 
    let di = DirectoryInfo path
    if di.Exists then 
        // delete all files
        Directory.GetFiles(path, "*.*", SearchOption.AllDirectories) 
        |> Seq.iter (fun file -> 
                        let fi = FileInfo file
                        fi.IsReadOnly <- false
                        fi.Delete())

        // deletes all subdirectories
        let rec deleteDirs actDir = 
            Directory.GetDirectories(actDir) |> Seq.iter deleteDirs
            Directory.Delete(actDir, true)
        Directory.GetDirectories path |> Seq.iter deleteDirs
    else CreateDir path
    // set writeable
    File.SetAttributes(path, FileAttributes.Normal)