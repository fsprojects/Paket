[<AutoOpen>]
module internal Paket.Logging

open System

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