module Performance.EventBoundarySpecs

open Paket.Profile

open FsCheck
open NUnit.Framework
open System

[<Test>]
let ``simple event boundaries work``() =
    let boundaries =
        [|
            Start(new DateTime(2017, 12, 23, 19, 7, 0))
            End(new DateTime(2017, 12, 23, 19, 15, 0))
        |]

    let results = getCoalescedEventTimeSpans(boundaries)
    let expected = [| new TimeSpan(0, 8, 0) |]
    Assert.AreEqual(expected, results)

[<Test>]
let ``coalescing event boundaries works``() =
    let boundaries =
        [|
            Start(new DateTime(2017, 12, 23, 19, 7, 0))
            Start(new DateTime(2017, 12, 23, 19, 14, 0))
            End(new DateTime(2017, 12, 23, 19, 15, 0))
            End(new DateTime(2017, 12, 23, 19, 16, 0))
        |]

    let results = getCoalescedEventTimeSpans(boundaries)
    let expected = [| new TimeSpan(0, 9, 0) |]
    Assert.AreEqual(expected, results)

[<Test>]
let ``skip mismatched event end boundaries``() =
    let boundaries =
        [|
            End(new DateTime(2017, 12, 23, 19, 6, 0))
            Start(new DateTime(2017, 12, 23, 19, 7, 0))
            Start(new DateTime(2017, 12, 23, 19, 14, 0))
            End(new DateTime(2017, 12, 23, 19, 15, 0))
            End(new DateTime(2017, 12, 23, 19, 16, 0))
            End(new DateTime(2017, 12, 23, 19, 17, 0))
        |]

    let results = getCoalescedEventTimeSpans(boundaries)
    let expected = [| new TimeSpan(0, 9, 0) |]
    Assert.AreEqual(expected, results)

[<Test>]
let ``arbitrary event boundaries produce at least one time span``() =
    let hasBeginBeforeEnd(bounds: EventBoundary array) =
        let firstStart = bounds |> Array.tryFindIndex EventBoundary.IsStartBoundary
        let firstEnd = bounds |> Array.tryFindIndex EventBoundary.IsEndBoundary

        match (firstStart, firstEnd) with
        | Some(fs), Some(fe) when fs < fe -> true
        | _ -> false

    let boundariesCoalesceToAtLeastOneTimeSpan(bounds: EventBoundary array) =
        not(hasBeginBeforeEnd(bounds)) || not(getCoalescedEventTimeSpans(bounds) |> Array.isEmpty)

    Check.QuickThrowOnFailure boundariesCoalesceToAtLeastOneTimeSpan