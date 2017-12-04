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

type Event = { Category: Category; Duration : TimeSpan }

let events =
    System.Collections.Concurrent.ConcurrentBag<Event>()
    
let trackEvent cat =
    events.Add({ Category = cat; Duration = TimeSpan() })

let startCategory cat =
    let cw = Stopwatch.StartNew()
    let mutable wasDisposed = false
    { new System.IDisposable with
        member x.Dispose () = 
            if not wasDisposed then
                wasDisposed <- true
                cw.Stop(); events.Add({ Category = cat; Duration = cw.Elapsed })  }
    
let startCategoryF cat f =
    let cw = Stopwatch.StartNew()
    let res = f()
    cw.Stop()
    events.Add({ Category = cat; Duration = cw.Elapsed })
    res
