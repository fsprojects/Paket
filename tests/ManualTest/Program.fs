open Paket

let completeConfig = Config.ReadFromFile "myConfig.fsx"

for x in completeConfig.Resolve(Nuget.NugetDiscovery()) do
    printfn "%A" x.Value
System.Console.ReadKey() |> ignore
