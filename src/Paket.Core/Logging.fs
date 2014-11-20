module Paket.Logging

open System

let mutable verbose = false

/// [omit]
let monitor = new Object()

/// [omit]
let inline tracen (s : string) = lock monitor (fun () -> printfn "%s" s)

/// [omit]
let inline tracefn fmt = Printf.ksprintf tracen fmt

/// [omit]
let inline trace (s : string) = lock monitor (fun () -> printf "%s" s)

/// [omit]
let inline tracef fmt = Printf.ksprintf trace fmt


/// [omit]
let inline traceVerbose (s : string) =
    if verbose then
        lock monitor (fun () -> printfn "%s" s)

/// [omit]
let inline verbosefn fmt = Printf.ksprintf traceVerbose fmt

/// [omit]
let inline traceColored color (s: string) = 
    lock monitor 
        (fun () ->
            let curColor = Console.ForegroundColor
            if curColor <> color then Console.ForegroundColor <- color
            printfn "%s" s
            if curColor <> color then Console.ForegroundColor <- curColor)

/// [omit]
let inline traceError s = traceColored ConsoleColor.Red s

/// [omit]
let inline traceWarn s = traceColored ConsoleColor.Yellow s

/// [omit]
let inline traceErrorfn fmt = Printf.ksprintf traceError fmt

/// [omit]
let inline traceWarnfn fmt = Printf.ksprintf traceWarn fmt