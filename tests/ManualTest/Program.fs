open Paket

let completeConfig = Config.ReadFromFile "myConfig.fsx"

let graph = [
    "Castle.Windsor-log4net","3.2",[]
    "Rx-Main","2.0",[]
]

// TODO: Remove me
let DictionaryDiscovery(graph : seq<string * string * (string * VersionRange) list>) = 
    { new IDiscovery with
          member __.GetDirectDependencies(sourceType, source, package, version) = 
              graph
              |> Seq.filter (fun (p,v,_) -> p = package && v = version) 
              |> Seq.map (fun (_,_,d) -> d) 
              |> Seq.head 
              |> List.map (fun (p,v) -> { Name = p; VersionRange = v; SourceType = sourceType; Source = source})
          member __.GetVersions package = 
              graph              
              |> Seq.filter (fun (p,_,_) -> p = package)
              |> Seq.map (fun (_,v,_) -> v) }


let discovery = DictionaryDiscovery graph

for x in completeConfig.Resolve(discovery) do
    printfn "%s => %A" x.Key x.Value
System.Console.ReadKey() |> ignore
