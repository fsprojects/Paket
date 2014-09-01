[<AutoOpen>]
module Paket.Utils

open System

let monitor = new Object()

let trace (s:string) = lock monitor (fun () -> printfn "%s" s)

let tracefn fmt = Printf.ksprintf trace fmt