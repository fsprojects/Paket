open Paket

let completeConfig = Config.ReadFromFile "myConfig.fsx"

let graph = [
    "Castle.Windsor-log4net","3.2",[]
    "Rx-Main","2.0",[]
]

for x in completeConfig.Resolve(Nuget.NugetDiscovery()) do
    printfn "%A" x.Value
System.Console.ReadKey() |> ignore
