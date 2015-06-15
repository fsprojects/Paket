[<AutoOpen>]
module Paket.PowerShell.CmdletExt

open System.Management.Automation
open System
open System.Diagnostics
open Paket

// add F# printf write extensions
type Cmdlet with

    member x.WritefCommandDetail format =
        Printf.ksprintf (fun s -> x.WriteCommandDetail s |> ignore) format

    member x.WritefDebug format =
        Printf.ksprintf (fun s -> x.WriteDebug s |> ignore) format

    member x.WritefVebose format =
        Printf.ksprintf (fun s -> x.WriteVerbose s |> ignore) format

    member x.WritefWarning format =
        Printf.ksprintf (fun s -> x.WriteWarning s |> ignore) format

type PSCmdlet with
    
    // Common Parameters http://ss64.com/ps/common.html

    member x.Verbose
        with get() =
            let bps = x.MyInvocation.BoundParameters
            if bps.ContainsKey "Verbose" then
                (bps.["Verbose"] :?> SwitchParameter).ToBool()
            else false

    member x.Debug
        with get() =
            let bps = x.MyInvocation.BoundParameters
            if bps.ContainsKey "Debug" then
                (bps.["Debug"] :?> SwitchParameter).ToBool()
            else false

    member x.SetCurrentDirectoryToLocation() =
        Environment.CurrentDirectory <- x.SessionState.Path.CurrentFileSystemLocation.Path

    member x.RegisterTrace() =
        Logging.verbose <- x.Verbose
        let id = Threading.Thread.CurrentThread.ManagedThreadId
        Logging.subscribe (fun trace ->
            if id = Threading.Thread.CurrentThread.ManagedThreadId then
                match trace.Level with
                | TraceLevel.Warning -> x.WriteWarning trace.Text
                | TraceLevel.Error -> x.WriteWarning trace.Text
                | _ -> x.WriteObject trace.Text
            else
                Diagnostics.Debug.Write(sprintf "not on main PS thread: %A" trace)
        )