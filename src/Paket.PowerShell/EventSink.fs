namespace Paket.PowerShell

open System
open System.Collections.Generic
open System.Collections.Concurrent

type EventSink<'T>() =
    let queue = new BlockingCollection<_>()
    let subscriptions = List()

    member __.Fill callback (source:IObservable<'T>) =
        source |> Observable.subscribe (fun state -> queue.Add((callback, state)))
        |> subscriptions.Add

    member __.Drain() =
        for callback, state in queue.GetConsumingEnumerable() do
            callback state

    member __.StopFill() =
        for sb in subscriptions do
            use d = sb
            ()
        queue.CompleteAdding()

    interface IDisposable with
        member x.Dispose() =
            for s in subscriptions do
                use d = s
                ()
            use d = queue
            ()