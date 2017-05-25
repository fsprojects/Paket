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

    static member Choice(tasks : Async<'T option> seq) = async {
         match Seq.toArray tasks with
         | [||] -> return None
         | [|t|] -> return! t
         | tasks ->
 
         let! t = Async.CancellationToken
         return! Async.FromContinuations <|
             fun (sc,ec,cc) ->
                 let noneCount = ref 0
                 let exnCount = ref 0
                 let innerCts = CancellationTokenSource.CreateLinkedTokenSource t
 
                 let scont (result : 'T option) =
                     match result with
                     | Some _ when Interlocked.Increment exnCount = 1 -> innerCts.Cancel() ; sc result
                     | None when Interlocked.Increment noneCount = tasks.Length -> sc None
                     | _ -> ()
 
                 let econt (exn : exn) =
                     if Interlocked.Increment exnCount = 1 then 
                         innerCts.Cancel() ; ec exn
 
                 let ccont (exn : OperationCanceledException) =
                     if Interlocked.Increment exnCount = 1 then
                         innerCts.Cancel(); cc exn
 
                 for task in tasks do
                     ignore <| System.Threading.Tasks.Task.Factory.StartNew(fun () -> Async.StartWithContinuations(task, scont, econt, ccont, innerCts.Token))
     }
