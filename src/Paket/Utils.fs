[<AutoOpen>]
/// Contains methods for IO.
module Paket.Utils

open System
open System.IO
open System.Net
open System.Xml

/// [omit]
let monitor = new Object()

/// [omit]
let trace (s : string) = lock monitor (fun () -> printfn "%s" s)

/// [omit]
let tracefn fmt = Printf.ksprintf trace fmt

/// [omit]
let traceColored color (s: string) = 
    lock monitor 
        (fun () ->
            let curColor = Console.ForegroundColor
            if curColor <> color then Console.ForegroundColor <- color
            printfn "%s" s
            if curColor <> color then Console.ForegroundColor <- curColor)

/// [omit]
let traceError = traceColored ConsoleColor.Red

/// [omit]
let traceWarn = traceColored ConsoleColor.Yellow

/// [omit]
let addAttribute name value (node:XmlElement) =
    node.SetAttribute(name, value) |> ignore
    node

/// [omit]
let addChild child (node:XmlElement) =
    node.AppendChild(child) |> ignore
    node

/// [omit]
let traceErrorfn fmt = Printf.ksprintf traceError fmt

/// [omit]
let traceWarnfn fmt = Printf.ksprintf traceWarn fmt

/// Creates a directory if it does not exist.
let CreateDir path = 
    let dir = DirectoryInfo path
    if not dir.Exists then dir.Create()

/// Cleans a directory by removing all files and sub-directories.
let CleanDir path = 
    let di = DirectoryInfo path
    if di.Exists then 
        // delete all files
        let files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
        files |> Seq.iter (fun file -> 
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

/// [omit]
let createRelativePath root path =
    Uri(root).MakeRelativeUri(Uri(path)).ToString().Replace("/", "\\")

/// [omit]
let normalizeXml(doc:XmlDocument) =
    use stringWriter = new StringWriter()
    let settings = XmlWriterSettings()
    settings.Indent <- true
        
    use xmlTextWriter = XmlWriter.Create(stringWriter, settings)
    doc.WriteTo(xmlTextWriter)
    xmlTextWriter.Flush()
    stringWriter.GetStringBuilder().ToString()

let createWebClient() =
    let client = new WebClient()
    client.Headers.Add("user-agent", "Paket")
    client

/// [omit]
let getFromUrl (url : string) = 
    async { 
        try
            use client = createWebClient()
            return! client.AsyncDownloadString(Uri(url))
        with
        | exn -> 
            failwithf "Could not retrieve data from %s%s  Message: %s" url Environment.NewLine exn.Message
            return ""
    }

/// [omit]
let safeGetFromUrl (url : string) = 
    async { 
        try 
            use client = createWebClient()
            let! raw = client.AsyncDownloadString(Uri(url))
            return Some raw
        with _ -> return None
    }

/// Enumerates all files with the given pattern
let FindAllFiles(folder, pattern) = DirectoryInfo(folder).EnumerateFiles(pattern, SearchOption.AllDirectories)

module Seq = 
    let firstOrDefault seq = Seq.tryFind (fun _ -> true) seq
