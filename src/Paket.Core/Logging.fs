module Paket.Logging

open System
open System.IO

let mutable verbose = false

let mutable logFile : string option = None

let setLogFile fileName =
    let fi = FileInfo fileName
    logFile <- Some fi.FullName
    if fi.Exists then
        fi.Delete()
    else
        if fi.Directory.Exists |> not then
            fi.Directory.Create()

let inline traceToFile (text:string) =
    match logFile with
    | Some fileName -> try File.AppendAllLines(fileName,[text]) with | _ -> ()
    | _ -> ()

/// [omit]
let monitor = new Object()

/// [omit]
let inline tracen (s : string) = lock monitor (fun () -> traceToFile s; Console.WriteLine s)

/// [omit]
let inline tracefn fmt = Printf.ksprintf tracen fmt

/// [omit]
let inline trace (s : string) = lock monitor (fun () -> traceToFile s; Console.Write s)

/// [omit]
let inline tracef fmt = Printf.ksprintf trace fmt

/// [omit]
let inline traceVerbose (s : string) =
    if verbose then
        lock monitor (fun () -> traceToFile s; Console.WriteLine s)

/// [omit]
let inline verbosefn fmt = Printf.ksprintf traceVerbose fmt

/// [omit]
let inline traceColored color (s: string) = 
    lock monitor 
        (fun () ->
            let curColor = Console.ForegroundColor
            if curColor <> color then Console.ForegroundColor <- color
            traceToFile s
            use textWriter = 
                match color with
                | ConsoleColor.Red -> Console.Error
                | _ -> Console.Out
            textWriter.WriteLine s
            if curColor <> color then Console.ForegroundColor <- curColor)

/// [omit]
let inline traceError s = traceColored ConsoleColor.Red s

/// [omit]
let inline traceWarn s = traceColored ConsoleColor.Yellow s

/// [omit]
let inline traceErrorfn fmt = Printf.ksprintf traceError fmt

/// [omit]
let inline traceWarnfn fmt = Printf.ksprintf traceWarn fmt