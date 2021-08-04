module Paket.Profile

open System.Diagnostics
open System

type BlockReason =
    | PackageDetails
    | GetVersion

type Category =
    | ResolverAlgorithm
    | ResolverAlgorithmBlocked of BlockReason
    | ResolverAlgorithmNotBlocked of BlockReason
    | NuGetRequest
    | NuGetDownload
    | FileIO
    | Other

type EventBoundary =
    | Start of DateTime
    | End of DateTime
    with
        static member GetTime(b: EventBoundary) =
            match b with
            | Start(dt) -> dt
            | End(dt) -> dt
        static member IsEndBoundary(b: EventBoundary) =
            match b with
            | End _ -> true
            | _ -> false
        static member IsStartBoundary(b: EventBoundary) =
            match b with
            | Start _ -> true
            | _ -> false

type Event = { Category: Category; Start: EventBoundary; End: EventBoundary }

let private getNextSpan(startIndex: int, boundaries: EventBoundary array): (TimeSpan * (int * EventBoundary array)) option =
    let mutable i = startIndex
    while (i < boundaries.Length) && EventBoundary.IsEndBoundary(boundaries.[i]) do
        i <- i + 1

    if i >= boundaries.Length then
        None
    else
        let mutable spanStart = i
        i <- i + 1
        let mutable boundaryStartCount = 1

        while (boundaryStartCount > 0) && (i < boundaries.Length) do
            match boundaries.[i] with
            | Start _ ->
                boundaryStartCount <- boundaryStartCount + 1
            | End _ ->
                boundaryStartCount <- boundaryStartCount - 1

            i <- i + 1

        // Calculate the next time span.
        let startTime = EventBoundary.GetTime(boundaries.[spanStart])
        let endTime = EventBoundary.GetTime(boundaries.[Math.Min(Math.Max(0, boundaries.Length - 1), i - 1)])

        Some((endTime - startTime, (i, boundaries)))

let getCoalescedEventTimeSpans(boundaries: EventBoundary array): TimeSpan array =
    let sortedBoundaries =
        boundaries
        |> Array.sortBy (fun b -> EventBoundary.GetTime(b))

    let spans = Array.unfold getNextSpan (0, sortedBoundaries)
    spans

let events =
    System.Collections.Concurrent.ConcurrentBag<Event>()

let trackEvent cat =
    let now = DateTime.Now
    events.Add({ Category = cat; Start = Start(now); End = End(now) })

let startCategory cat =
    let cw = Stopwatch.StartNew()
    let mutable wasDisposed = false
    { new System.IDisposable with
        member x.Dispose () =
            if not wasDisposed then
                wasDisposed <- true
                let now = DateTime.Now
                let start = now - cw.Elapsed
                cw.Stop(); events.Add({ Category = cat; Start = Start(start); End = End(now)})
    }

let startCategoryF cat f =
    let cw = Stopwatch.StartNew()
    let res = f()
    cw.Stop()
    let now = DateTime.Now
    let start = now - cw.Elapsed
    events.Add({ Category = cat; Start = Start(start); End = End(now) })
    res
