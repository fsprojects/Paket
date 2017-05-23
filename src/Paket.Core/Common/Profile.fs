module Paket.Profile

open System.Diagnostics

type Categories =
    | ResolverAlgorithm
    | NuGetRequest
    | NuGetDownload
    | FileCopy
    | Other

let watches =
    [ ResolverAlgorithm; NuGetRequest; NuGetDownload; FileCopy; Other ]
    |> List.map (fun cat -> cat, new Stopwatch())
    |> Map.ofList
let other = watches.[Other]
let mutable current = other
let reset () =
    watches
    |> Seq.iter (fun (kv) -> kv.Value.Reset())
    other.Start()
    current <- other

let startCategory cat =
    watches
    |> Seq.iter (fun (kv) -> kv.Value.Stop())
    let prev = current
    let cw = watches.[cat]
    current <- cw
    cw.Start()
    
    { new System.IDisposable with member x.Dispose () = cw.Stop(); prev.Start()  }
    
let startCategoryRaw cat =
    watches
    |> Seq.iter (fun (kv) -> kv.Value.Stop())
    let cw = watches.[cat]
    current <- cw
    cw.Start()

let startCategoryF cat f =
    watches
    |> Seq.iter (fun (kv) -> kv.Value.Stop())
    let prev = current
    let cw = watches.[cat]
    current <- prev
    cw.Start()
    let res = f()
    cw.Stop()
    prev.Start()
    res
