module internal Paket.Logging

open System

let mutable verbose = false

/// [omit]
let monitor = new Object()

/// [omit]
let tracen (s : string) = lock monitor (fun () -> printfn "%s" s)

/// [omit]
let tracefn fmt = Printf.ksprintf tracen fmt

/// [omit]
let trace (s : string) = lock monitor (fun () -> printf "%s" s)

/// [omit]
let tracef fmt = Printf.ksprintf trace fmt


/// [omit]
let traceVerbose (s : string) =
    if verbose then
        lock monitor (fun () -> printfn "%s" s)

/// [omit]
let verbosefn fmt = Printf.ksprintf traceVerbose fmt

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
let traceErrorfn fmt = Printf.ksprintf traceError fmt

/// [omit]
let traceWarnfn fmt = Printf.ksprintf traceWarn fmt