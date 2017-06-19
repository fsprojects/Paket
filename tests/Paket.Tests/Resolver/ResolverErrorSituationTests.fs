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

let rec findExnWhichContains msg (exn:exn) =
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
    cfg.Resolve(true,noSha1,VersionsFromGraphAsSeq graph, (fun _ _ _ _ -> []),PackageDetailsFromGraph graph,(fun _ _ -> None),groups,updateMode).[Constants.MainDependencyGroup].ResolvedPackages.GetModelOrFail()

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
let ``should fallback to timeoutexception when task never canceles``() =
    use consoleTrace = Logging.event.Publish |> Observable.subscribe Logging.traceToConsole
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
                    true,noSha1,VersionsFromGraphAsSeq graph1,(fun _ _ _ _ -> []),
                    // Will never finish...
                    (fun _ _ _ _ -> (new TaskCompletionSource<_>()).Task |> Async.AwaitTask),
                    (fun _ _ -> None),groups, UpdateMode.UpdateAll)
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
                    true,noSha1,VersionsFromGraphAsSeq graph1,(fun _ _ _ _ -> []),
                    // Will throw a proper exception when canceled
                    (fun _ _ _ _ ->
                        async {
                            let tcs = new TaskCompletionSource<_>()
                            //let! tok = Async.CancellationToken
                            //use _reg = tok.Register(fun () -> tcs.SetException (new TaskCanceledException("Some Url 'Blub' didn't respond")))
                            use! reg = Async.OnCancel (fun () ->
                                tcs.SetException (new Exception("Some Url 'Blub' didn't respond")))
                            return! tcs.Task |> Async.AwaitTask
                        }),
                    (fun _ _ -> None),groups, UpdateMode.UpdateAll)
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

