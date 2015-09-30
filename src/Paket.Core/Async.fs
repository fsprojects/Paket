namespace FSharp.Polyfill

open System.Threading

/// Extensions for async workflows.
[<AutoOpen>]
module AsyncExtensions = 
  open System

  type Microsoft.FSharp.Control.Async with 
     /// Runs both computations in parallel and returns the result as a tuple.
    static member Parallel (a : Async<'a>, b : Async<'b>) : Async<'a * 'b> =
        async {
            let! a' = Async.StartChild a
            let! b' = Async.StartChild b
            let! a'' = a'
            let! b'' = b'
            return (a'',b'')
        }

    static member Choice'(tasks : Async<'T option> seq) =
        async {
            let! t = Async.CancellationToken
            return! Async.FromContinuations <| 
                fun (cont,econt,ccont) ->
                let tasks = Seq.toArray tasks
                if tasks.Length = 0 then cont None else

                let innerCts = CancellationTokenSource.CreateLinkedTokenSource t

                let count = ref tasks.Length
                let completed = ref false
                let synchronize f =
                    lock count (fun () ->
                        if !completed then ()
                        else f ())
                
                // register for external cancellation
                do t.Register(
                    Action(fun () ->
                        synchronize (fun () ->
                            ccont <| OperationCanceledException()
                            completed := true))) |> ignore

                let wrap task =
                    async {
                        try
                            let! res = task
                            match res with
                            | Some r when Array.isEmpty r |> not -> // special Array.isEmpty case for Paket
                                synchronize (fun () ->
                                    cont (Some r)
                                    innerCts.Cancel()
                                    completed := true)
                            | _ -> 
                                synchronize (fun () ->
                                    decr count
                                    if !count = 0 then 
                                        cont None
                                        innerCts.Dispose ())
                        with e -> 
                            synchronize (fun () ->
                                econt e
                                innerCts.Cancel()
                                completed := true)
                    }

                for task in tasks do
                    Async.Start(wrap task, innerCts.Token)
        }