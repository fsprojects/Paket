open Paket
open TestHelpers

let completeConfig = Config.ReadFromFile "myConfig.fsx"

let graph = [
    "Castle.Windsor-log4net","3.2",[]
    "Rx-Main","2.0",[]
]

let discovery = DictionaryDiscovery graph

for x in completeConfig.Resolve(discovery) do
    printfn "%s => %A" x.Key x.Value
System.Console.ReadKey() |> ignore
