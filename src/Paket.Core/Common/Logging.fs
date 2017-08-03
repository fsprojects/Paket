module Paket.Logging

open System
open System.IO
open Pri.LongPath
open System.Diagnostics

/// [omit]
let mutable verbose = false

/// [omit]
type Trace = {
    Level: TraceLevel
    Text: string
    NewLine: bool }

/// [omit]
let event = Event<Trace>()


/// [omit]
let tracen s = event.Trigger { Level = TraceLevel.Info; Text = s; NewLine = true }

/// [omit]
let tracefn fmt = Printf.ksprintf tracen fmt

/// [omit]
let trace s = event.Trigger { Level = TraceLevel.Info; Text = s; NewLine = false }

/// [omit]
let tracef fmt = Printf.ksprintf trace fmt

/// [omit]
let traceVerbose s =
    if verbose then
        event.Trigger { Level = TraceLevel.Verbose; Text = s; NewLine = true }

/// [omit]
let verbosefn fmt = Printf.ksprintf traceVerbose fmt

/// [omit]
let traceError s = event.Trigger { Level = TraceLevel.Error; Text = s; NewLine = true }

/// [omit]
let traceWarn s = event.Trigger { Level = TraceLevel.Warning; Text = s; NewLine = true }

/// [omit]
let traceErrorfn fmt = Printf.ksprintf traceError fmt

/// [omit]
let traceWarnfn fmt = Printf.ksprintf traceWarn fmt


// Console Trace

/// [omit]
let traceColored color (s:string) = 
    let curColor = Console.ForegroundColor
    if curColor <> color then Console.ForegroundColor <- color
    use textWriter = 
        match color with
        | ConsoleColor.Red -> Console.Error
        | ConsoleColor.Yellow -> Console.Out
        | _ -> Console.Out

    textWriter.WriteLine s
    if curColor <> color then Console.ForegroundColor <- curColor

/// [omit]
let monitor = new Object()

/// [omit]
let traceToConsole (trace:Trace) =
    lock monitor
        (fun () ->
            match trace.Level with
            | TraceLevel.Warning -> traceColored ConsoleColor.Yellow trace.Text
            | TraceLevel.Error -> traceColored ConsoleColor.Red trace.Text
            | _ ->
                if trace.NewLine then Console.WriteLine trace.Text
                else Console.Write trace.Text )


// Log File Trace

/// [omit]
let mutable logFile : string option = None

/// [omit]
let traceToFile (trace:Trace) =
    match logFile with
    | Some fileName -> try File.AppendAllLines(fileName,[trace.Text]) with | _ -> ()
    | _ -> ()

/// [omit]
let setLogFile fileName =
    let fi = FileInfo fileName
    logFile <- Some fi.FullName
    if fi.Exists then
        fi.Delete()
    else
        if fi.Directory.Exists |> not then
            fi.Directory.Create()
    event.Publish |> Observable.subscribe traceToFile

/// [omit]
[<RequireQualifiedAccess>]
type private ExnType =
    | First
    | Aggregated
    | Inner

/// [omit]
let printErrorExt printFirstStack printAggregatedStacks printInnerStacks (exn:exn) =
    let defaultMessage = AggregateException().Message
    let rec printErrorHelper exnType useArrow indent (exn:exn) =
        let handleError () =
            let s = if useArrow then "->" else "- "
            let indentString = new String('\t', indent)
            let splitMsg = exn.Message.Split([|"\r\n"; "\n"|], StringSplitOptions.None)
            let typeString =
                let t = exn.GetType()
                if t = typeof<Exception> || t = typeof<AggregateException> then
                    ""
                else sprintf "%s: " t.Name
            traceErrorfn "%s%s %s%s" indentString s typeString (String.Join(sprintf "%s%s   " Environment.NewLine indentString , splitMsg))

            let printStack =
                match String.IsNullOrWhiteSpace exn.StackTrace, exnType with
                | false, ExnType.First when printFirstStack -> true
                | false, ExnType.Aggregated when printAggregatedStacks -> true
                | false, ExnType.Inner when printInnerStacks -> true
                | _ -> false

            if printStack then
                traceErrorfn "%s   StackTrace:" indentString
                let split = exn.StackTrace.Split([|"\r\n"; "\n"|], StringSplitOptions.None)
                traceErrorfn "%s     %s" indentString (String.Join(sprintf "%s%s     " Environment.NewLine indentString, split))

        match exn with
        | :? AggregateException as aggr ->
            if aggr.InnerExceptions.Count = 1 then
                let inner = aggr.InnerExceptions.[0]
                if aggr.Message = defaultMessage || aggr.Message = inner.Message then
                    // skip as no new information is available.
                    printErrorHelper exnType useArrow indent inner
                else
                    handleError()
                    printErrorHelper ExnType.Aggregated true indent inner
            else
                handleError()
                for inner in aggr.InnerExceptions do
                    printErrorHelper ExnType.Aggregated false (indent + 1) inner
        | _ ->
            handleError()
            if not (isNull exn.InnerException) then
                printErrorHelper ExnType.Inner true indent exn.InnerException

    printErrorHelper ExnType.First true 0 exn

/// [omit]
let printError (exn:exn) =
    printErrorExt verbose verbose false exn