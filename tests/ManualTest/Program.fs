open Paket

let completeConfig = Config.ReadFromFile "myConfig.fsx"

for x in completeConfig.Resolve(Nuget.NugetDiscovery()).ResolvedVersionMap do
    printfn "%A" x.Value
System.Console.ReadKey() |> ignore
