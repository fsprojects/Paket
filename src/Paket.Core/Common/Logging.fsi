module Paket.Logging

open System
open System.Diagnostics

val mutable verbose : bool

val mutable verboseWarnings : bool


val tracen : string -> unit

val tracefn : Printf.StringFormat<'a,unit> -> 'a

val trace : string -> unit

val tracef : Printf.StringFormat<'a,unit> -> 'a

val traceVerbose : string -> unit

val verbosefn : Printf.StringFormat<'a,unit> -> 'a

val traceError : string -> unit

val traceWarn : string -> unit

val traceErrorfn : Printf.StringFormat<'a,unit> -> 'a

val traceWarnfn : Printf.StringFormat<'a,unit> -> 'a

val traceWarnIfNotBefore : 'a ->  Printf.StringFormat<'b,unit> -> 'b
val traceErrorIfNotBefore : 'a ->  Printf.StringFormat<'b,unit> -> 'b

val getOmittedWarningCount : unit -> int

type Trace = {
    Level: TraceLevel
    Text: string
    NewLine: bool }

val event : Event<Trace>

val traceToConsole : Trace -> unit

val setLogFile : string -> IDisposable

val printError : exn -> unit

val printErrorExt : printFirstStack:bool -> printAggregatedStacks:bool -> printInnerStacks:bool -> exn -> unit