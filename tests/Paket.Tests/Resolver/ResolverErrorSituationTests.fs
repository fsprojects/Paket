module ResolverErrorSituationTests

open Paket
open NUnit.Framework
open FsUnit
open TestHelpers
open Paket.Domain
open Paket.PackageResolver
open Paket.Requirements
open System.Threading.Tasks
open System
open System.Threading
open FSharp.Polyfill

let rec findExnWhichContains (msg: string) (exn:exn) =
    match exn with
    | _ when  exn.Message.Contains msg -> Some exn
    | :? AggregateException as a ->
        a.InnerExceptions
        |> Seq.tryPick (fun e -> findExnWhichContains msg e)
    | _ when not (isNull exn.InnerException) ->
        findExnWhichContains msg exn.InnerException
    | _ -> None

let resolve graph updateMode (cfg : DependenciesFile) =
    let groups = [Constants.MainDependencyGroup, None ] |> Map.ofSeq
    cfg.Resolve(true,noSha1,VersionsFromGraphAsSeq graph, (fun _ _ -> []),PackageDetailsFromGraph graph,(fun _ _ _ -> None),groups,updateMode).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()

let graph1 =
  GraphOfNuspecs [
    """<?xml version="1.0" encoding="utf-8" standalone="yes"?>
<package xmlns="http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd">
  <metadata>
    <id>Chessie</id>
    <version>0.6.0</version>
    <dependencies>
      <group>
        <dependency id="FSharp.Core"></dependency>
      </group>
      <group targetFramework=".NETStandard1.6">
        <dependency id="MyNetStandardDummy" version="[1.6.0, )" />
        <dependency id="FSharp.Core" version="[4.0.1.7-alpha, )"></dependency>
      </group>
    </dependencies>
  </metadata>
</package>
    """
    """<?xml version="1.0"?>
<package xmlns="http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd">
  <metadata>
    <id>FSharp.Core</id>
    <version>4.0.0.1</version>
  </metadata>
</package>
    """
    """<?xml version="1.0" encoding="utf-8"?>
<package xmlns="http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd">
  <metadata>
    <id>FSharp.Core</id>
    <version>4.0.1.7-alpha</version>
    <dependencies>
      <group targetFramework=".NETStandard1.6">
        <dependency id="MyNetStandardDummy" version="[1.6.0, )" />
      </group>
    </dependencies>
  </metadata>
</package>"""
  ]

[<Test>]
#if NO_UNIT_TIMEOUTATTRIBUTE
[<Ignore "TimeoutAttribute not supported by netstandard NUnit">]
#else
[<Timeout 5000>]
#endif
let ``should fallback to timeoutexception when task never cancels``() =
    use consoleTrace = Logging.event.Publish |> Observable.subscribe Logging.traceToConsole
    let config = """
source http://www.nuget.org/api/v2
framework net46

nuget Chessie"""
    let cfg = DependenciesFile.FromSource(config)
    let groups = [Constants.MainDependencyGroup, None ] |> Map.ofSeq
    try
        // NOTE: This test is hard/impossible to debug, because of the Debugger.IsAttached checks in the resolver code!
        System.Environment.SetEnvironmentVariable("PAKET_RESOLVER_TASK_TIMEOUT", "500")
        try
            let groupResults =
                cfg.Resolve(
                    true,noSha1,VersionsFromGraphAsSeq graph1,(fun _ _ -> []),
                    // Will never finish...
                    (fun _ -> (new TaskCompletionSource<_>()).Task |> Async.AwaitTask),
                    (fun _ _ _ -> None),groups, UpdateMode.UpdateAll)
            let resolved = groupResults.[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
            Assert.Fail "Expected exception"
        with e ->
            match findExnWhichContains "Unable to retrieve package details for 'Chessie'-0.6.0" e with
            | Some e -> ()
            | None -> Assert.Fail(sprintf "Expected exception explaining Chessie could not be retrieved, but was %O" e)
    finally
        System.Environment.SetEnvironmentVariable("PAKET_RESOLVER_TASK_TIMEOUT", null)

// This test-cases let you understand better why we need a custom 'StartAsTaskTimeout' implementation and cannot
// use the StartAsTask default implementation, uncomment and run to see the difference in behavior.
//[<Test>]
//let ``check task cancellation``() =
//    let tcs = new TaskCompletionSource<_>()
//    let cts = new CancellationTokenSource()
//    use reg = cts.Token.Register(fun () -> tcs.SetException(Exception "Something bad happened"))
//    let a =
//        async {
//            cts.CancelAfter 500
//            do! tcs.Task |> Async.AwaitTask
//            printfn "test"
//            return! async {
//                do! Async.Sleep 100
//                return 4 }
//        } |> fun a -> Async.RunSynchronously(a, cancellationToken = cts.Token)
//    ()
//
//[<Test>]
//let ``check task cancellation (task)``() =
//    let tcs = new TaskCompletionSource<_>()
//    let cts = new CancellationTokenSource()
//    use reg = cts.Token.Register(fun () -> tcs.SetException(Exception "Something bad happened"))
//    let a =
//        async {
//            cts.CancelAfter 500
//            do! tcs.Task |> Async.AwaitTask
//        } |> fun a -> Async.StartAsTask(a, cancellationToken = cts.Token)
//    a.Result
//    ()
//[<Test>]
//let ``check task cancellation 2``() =
//    let tcs = new TaskCompletionSource<_>()
//    let cts = new CancellationTokenSource()
//    use reg = cts.Token.Register(fun () -> tcs.SetException(Exception "Something bad happened"))
//    let a =
//        async {
//            do! tcs.Task |> Async.AwaitTask
//        }
//
//    async {
//        do! Async.Sleep 500
//        cts.Cancel()
//    } |> Async.Start
//
//    let b =
//        async {
//            let! res = a
//            printfn "test"
//            do! Async.Sleep 100
//            return res
//        } |> fun a -> Async.RunSynchronously(a, cancellationToken = cts.Token)
//    ()
//
//[<Test>]
//let ``check task cancellation 3``() =
//    let tcs = new TaskCompletionSource<_>()
//    let cts = new CancellationTokenSource()
//    use reg = cts.Token.Register(fun () -> tcs.SetException(Exception "Something bad happened"))
//    let a =
//        async {
//            do! tcs.Task |> Async.AwaitTask
//        }
//
//    async {
//        do! Async.Sleep 500
//        cts.Cancel()
//    } |> Async.Start
//
//    let b =
//        async {
//            let! res = a
//            printfn "test"
//            do! Async.Sleep 100
//            return res
//        } |> fun a -> Async.StartAsTask(a, cancellationToken = cts.Token)
//    b.Result
//
//[<Test>]
//let ``check task cancellation 4``() =
//    let tcs = new TaskCompletionSource<_>()
//    let cts = new CancellationTokenSource()
//    use reg = cts.Token.Register(fun () -> tcs.SetException(Exception "Something bad happened"))
//    let a =
//        async {
//            do! tcs.Task |> Async.AwaitTask
//        }
//
//    async {
//        do! Async.Sleep 500
//        cts.Cancel()
//    } |> Async.Start
//
//    let b =
//        async {
//            let! res = a
//            printfn "test"
//            do! Async.Sleep 100
//            return res
//        } |> fun a -> Async.StartAsTaskTimeout(a, cancellationToken = cts.Token, cancelTimeout = 10000)
//    b.Result

[<Test>]
[<Ignore "Currently we no longer do cancellation on tasks, because they currently still finish eventually in the real world (#2440). But once we have this stuff worked out we should revisit this, because it improves error messages.">]
let ``should forward underlying cause when task properly cancels``() =
    let config = """
source http://www.nuget.org/api/v2
framework net46

nuget Chessie"""
    let cfg = DependenciesFile.FromSource(config)
    let groups = [Constants.MainDependencyGroup, None ] |> Map.ofSeq
    try
        // NOTE: This test is hard/improssible to debug, because of the Debugger.IsAttached checks in the resolver code!
        System.Environment.SetEnvironmentVariable("PAKET_RESOLVER_TASK_TIMEOUT", "500")
        try
            let groupResults =
                cfg.Resolve(
                    true,noSha1,VersionsFromGraphAsSeq graph1,(fun _ _ -> []),
                    // Will throw a proper exception when canceled
                    (fun _ ->
                        async {
                            let tcs = new TaskCompletionSource<_>()
                            //let! tok = Async.CancellationToken
                            //use _reg = tok.Register(fun () -> tcs.SetException (new TaskCanceledException("Some Url 'Blub' didn't respond")))
                            use! reg = Async.OnCancel (fun () ->
                                tcs.SetException (new Exception("Some Url 'Blub' didn't respond")))
                            return! tcs.Task |> Async.AwaitTask
                        }),
                    (fun _ _ _ -> None),groups, UpdateMode.UpdateAll)
            let resolved = groupResults.[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()
            Assert.Fail "Expected exception"
        with e ->
            match findExnWhichContains "Some Url 'Blub' didn't respond" e with
            | Some e -> ()
            | None -> Assert.Fail(sprintf "Expected exception explaining 'Some Url 'Blub' didn't respond', but was %O" e)
            match findExnWhichContains "Unable to retrieve package details for 'Chessie'-0.6.0" e with
            | Some e -> ()
            | None -> Assert.Fail(sprintf "Expected exception explaining Chessie could not be retrieved, but was %O" e)
    finally
        System.Environment.SetEnvironmentVariable("PAKET_RESOLVER_TASK_TIMEOUT", null)

[<Test>]
let ``task priorization works``() =
    use consoleTrace = Logging.event.Publish |> Observable.subscribe Logging.traceToConsole
    use cts = new CancellationTokenSource()

    let work = Array.init 1000 (fun _ -> Async.Sleep 100)
    let q = ResolverRequestQueue.Create()
    let worker1 = ResolverRequestQueue.startProcessing cts.Token q
    let worker2 = ResolverRequestQueue.startProcessing cts.Token q
    let handles =
        work
        |> Array.map (fun w -> ResolverRequestQueue.addWork WorkPriority.BackgroundWork (fun ct -> Async.StartAsTaskProperCancel(w, cancellationToken = ct)) q)
    let lastHandle = ResolverRequestQueue.addWork WorkPriority.BackgroundWork (fun ct -> Async.StartAsTaskProperCancel(async { return 5 }, cancellationToken = ct)) q
    lastHandle.Reprioritize WorkPriority.BlockingWork
    lastHandle.Task.Wait(200)
    |> shouldEqual true

    cts.Cancel()

[<Test>]
#if NO_UNIT_TIMEOUTATTRIBUTE
[<Ignore "TimeoutAttribute not supported by netstandard NUnit">]
#else
[<Timeout 5000>]
#endif
let ``cancellation fsharp.core``() =

    let StartCatchCancellation cancellationToken work =
        Async.FromContinuations(fun (cont, econt, _) ->
          // When the child is cancelled, report OperationCancelled
          // as an ordinary exception to "error continuation" rather
          // than using "cancellation continuation"
          let ccont e = econt e
          // Start the workflow using a provided cancellation token
          Async.StartWithContinuations( work, cont, econt, ccont,
                                        ?cancellationToken=cancellationToken) )

    /// Like StartAsTask but gives the computation time to so some regular cancellation work
    let StartAsTaskProperCancel taskCreationOptions  cancellationToken (computation : Async<_>) : Task<_> =
        let token = defaultArg cancellationToken Async.DefaultCancellationToken
        let taskCreationOptions = defaultArg taskCreationOptions TaskCreationOptions.None
        let tcs = new TaskCompletionSource<_>("StartAsTaskProperCancel", taskCreationOptions)

        let a =
            async {
                try
                    // To ensure we don't cancel this very async (which is required to properly forward the error condition)
                    let! result = StartCatchCancellation (Some token) computation
                    do
                        tcs.SetResult(result)
                with exn ->
                    tcs.SetException(exn)
            }
        Async.Start(a)
        tcs.Task

    let cts = new CancellationTokenSource()
    let tcs = TaskCompletionSource<_>()
    let t =
        async {
            do! tcs.Task |> Async.AwaitTask
        }
        |> StartAsTaskProperCancel (Some TaskCreationOptions.None) (Some cts.Token)

    // First cancel the token, then set the task as cancelled.
    async {
        do! Async.Sleep 100
        cts.Cancel()
        do! Async.Sleep 100
        tcs.TrySetException (TimeoutException "Cancellation was requested, but wasn't honered after 1 second. We finish the task forcefully (requests might still run in the background).")
            |> ignore
    } |> Async.Start

    try
        let res = t.Wait(1000)
        Assert.Fail (sprintf "Excepted TimeoutException wrapped in an AggregateException, but got %A" res)
    with :? AggregateException as agg -> ()

[<Test>]
#if NO_UNIT_TIMEOUTATTRIBUTE
[<Ignore "TimeoutAttribute not supported by netstandard NUnit">]
#else
[<Timeout 5000>]
#endif
let ``cancellation WorkerQueue``() =
    use cts = new CancellationTokenSource()
    let workerQueue = ResolverRequestQueue.Create()
    let workerCount = 1
    let workers =
        // start maximal 8 requests at the same time.
        [ 1 .. workerCount ]
        |> List.map (fun _ -> ResolverRequestQueue.startProcessing cts.Token workerQueue)

    // mainly for failing unit-tests to be faster
    let taskTimeout = 500

    let getAndReport (mem:ResolverTaskMemory<_>) =
        try
            let workHandle = mem.Work
            if workHandle.Task.IsCompleted then
                workHandle.Task.Result
            else
                workHandle.Reprioritize WorkPriority.BlockingWork
                let waitedAlready, isFinished = mem.Wait(taskTimeout)
                // When debugger is attached we just wait forever when calling .Result later ...
                // apparently the task didn't return, let's throw here
                if not isFinished (*&& not Debugger.IsAttached*) then
                    if waitedAlready then
                        raise <| TimeoutException(sprintf "Tried (again) to access an unfinished task, not waiting %d seconds this time..." (taskTimeout / 1000))
                    else
                        raise <|
                            TimeoutException(
                                sprintf "Waited %d seconds for a request to finish.\n" (taskTimeout / 1000))
                if waitedAlready && isFinished then
                    // recovered
                    ()
                let result = workHandle.Task.Result
                result
        with :? AggregateException as a when a.InnerExceptions.Count = 1 ->
            let flat = a.Flatten()
            if flat.InnerExceptions.Count = 1 then
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(flat.InnerExceptions.[0]).Throw()
            reraise()

    let mem =
        workerQueue
        |> ResolverRequestQueue.addWork WorkPriority.BackgroundWork (fun ct ->
            (new TaskCompletionSource<_>()).Task)
        |> ResolverTaskMemory.ofWork

    let mutable exceptionThrown = true
    try
        try
            getAndReport mem
            Assert.Fail "Expected Timeout Exception"
        with :? TimeoutException -> ()
        exceptionThrown <- false
    finally
        cts.Cancel()
        for w in workers do
            try
                w.Wait()
            with
            | :? ObjectDisposedException ->
                //if verbose then
                //    traceVerbose "Worker-Task was disposed"
                ()
            | :? AggregateException as a ->
                match a.InnerExceptions |> Seq.toArray with
                | [| :? OperationCanceledException as c |] ->
                    // Task was cancelled...
                    ()
                | _ ->
                    if exceptionThrown then
                        ()
                    else reraise()
            | e when exceptionThrown ->
                // traceErrorfn "Error while waiting for worker to finish: %O" e
                ()