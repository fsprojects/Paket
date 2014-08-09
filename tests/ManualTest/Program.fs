open Paket.ConfigDSL

let completeConfig = 
    ReadFromFile "myConfig.fsx"
     ==> ReadFromFile "myConfig2.fsx"

for x in completeConfig do
    printfn "%s from %s => %s - %s" x.Key x.Value.Source x.Value.Version.Min x.Value.Version.Max

System.Console.ReadKey() |> ignore
