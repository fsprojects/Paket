// --------------------------------------------------------------------------------------
// FAKE build script
// --------------------------------------------------------------------------------------

#r @"packages/build/FAKE/tools/FakeLib.dll"

open Fake
open System
open System.IO

open System.Threading

/// Helper that can be used for writing CPS-style code that resumes
/// on the same thread where the operation was started.
let synchronize f = 
    let ctx = System.Threading.SynchronizationContext.Current
    f (fun g -> 
        let nctx = System.Threading.SynchronizationContext.Current
        if ctx <> null && ctx <> nctx then ctx.Post((fun _ -> g()), null)
        else g())

type Microsoft.FSharp.Control.Async with
    /// Behaves like AwaitObservable, but calls the specified guarding function
    /// after a subscriber is registered with the observable.
    static member GuardedAwaitObservable (ev1 : IObservable<'T1>) guardFunction = 
        let removeObj : IDisposable option ref = ref None
        let removeLock = new obj()
        let setRemover r = lock removeLock (fun () -> removeObj := Some r)
        
        let remove() = 
            lock removeLock (fun () -> 
                match !removeObj with
                | Some d -> 
                    removeObj := None
                    d.Dispose()
                | None -> ())
        synchronize (fun f -> 
            let workflow = 
                Async.FromContinuations((fun (cont, econt, ccont) -> 
                    let rec finish cont value = 
                        remove()
                        f (fun () -> cont value)
                    setRemover <| ev1.Subscribe({ new IObserver<_> with
                                                      member x.OnNext(v) = finish cont v
                                                      member x.OnError(e) = finish econt e
                                                      member x.OnCompleted() = 
                                                          let msg = 
                                                              "Cancelling the workflow, because the Observable awaited using AwaitObservable has completed."
                                                          finish ccont (new System.OperationCanceledException(msg)) })
                    guardFunction()))
            async { 
                let! cToken = Async.CancellationToken
                let token : CancellationToken = cToken
                use registration = token.Register(fun () -> remove())
                return! workflow
            })


let private formatArgs args = 
    let delimit (str : string) = 
        if isLetterOrDigit (str.Chars(str.Length - 1)) then str + " "
        else str
    args
    |> Seq.map (fun (k, v) -> delimit k + quoteIfNeeded v)
    |> separated " "

open System.Diagnostics

/// Execute an external program asynchronously and return the exit code,
/// logging output and error messages to FAKE output. You can compose the result
/// with Async.Parallel to run multiple external programs at once, but be
/// sure that none of them depend on the output of another.
let asyncShellExec (args : ExecParams) = 
    async { 
        if isNullOrEmpty args.Program then invalidArg "args" "You must specify a program to run!"
        let commandLine = args.CommandLine + " " + formatArgs args.Args
        let info = 
            ProcessStartInfo
                (args.Program, UseShellExecute = false, 
                 RedirectStandardError = true, RedirectStandardOutput = true, RedirectStandardInput = true,
                 WindowStyle = ProcessWindowStyle.Hidden, WorkingDirectory = args.WorkingDirectory, 
                 Arguments = commandLine)
        use proc = new Process(StartInfo = info)
        proc.ErrorDataReceived.Add(fun e -> 
            if e.Data <> null then traceError e.Data)
        proc.OutputDataReceived.Add(fun e -> 
            if e.Data <> null then log e.Data)
        start proc
        proc.BeginOutputReadLine()
        proc.BeginErrorReadLine()
        proc.StandardInput.Close()
        // attaches handler to Exited event, enables raising events, then awaits event
        // the event gets triggered even if process has already finished
        let! _ = Async.GuardedAwaitObservable proc.Exited (fun _ -> proc.EnableRaisingEvents <- true)
        return proc.ExitCode
    }

/// Execute an external program and return the exit code.
/// [omit]
let shellExec args = args |> asyncShellExec |> Async.RunSynchronously

/// Allows to exec shell operations synchronously and asynchronously.
type Shell() = 
    
    static member private GetParams(cmd, ?args, ?dir) = 
        let args = defaultArg args ""
        let dir = defaultArg dir (Directory.GetCurrentDirectory())
        { WorkingDirectory = dir
          Program = cmd
          CommandLine = args
          Args = [] }
    
    /// Runs the given process, waits for it's completion and returns the exit code.
    /// ## Parameters
    ///
    ///  - `cmd` - The command which should be run in elavated context.
    ///  - `args` - The process arguments (optional).
    ///  - `directory` - The working directory (optional).
    static member Exec(cmd, ?args, ?dir) = shellExec (Shell.GetParams(cmd, ?args = args, ?dir = dir))
    
    /// Runs the given process asynchronously.
    /// ## Parameters
    ///
    ///  - `cmd` - The command which should be run in elavated context.
    ///  - `args` - The process arguments (optional).
    ///  - `directory` - The working directory (optional).
    static member AsyncExec(cmd, ?args, ?dir) = asyncShellExec (Shell.GetParams(cmd, ?args = args, ?dir = dir))

let Exec command args =
    let result = Shell.Exec(command, args)
    if result <> 0 then failwithf "%s exited with error %d" command result

let ExecutePlistBuddy key value path =
    Exec "/usr/libexec/PlistBuddy" ("-c 'Set :" + key + " " + value + "' " + path)
    
ExecutePlistBuddy "CFBundleIdentifier" "test1" "Some/Info.plist"
// Remove the next line, and we're good to go.
ExecutePlistBuddy "CFBundleDisplayName" "test2" "Some/Info.plist"
