module Paket.Discovery

open Paket

let DictionaryDiscovery(graph : seq<string * string * (string * VersionRange) list>) = 
    { new IDiscovery with
          member __.GetDirectDependencies(package, version) = 
            graph 
            |> Seq.filter (fun (p,v,_) -> p = package && v = version) 
            |> Seq.map (fun (_,_,d) -> d) 
            |> Seq.head 

          member __.GetVersions package = 
              graph              
              |> Seq.filter (fun (p,_,_) -> p = package)
              |> Seq.map (fun (_,v,_) -> v) }