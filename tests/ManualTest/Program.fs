open Paket.ConfigDSL

let completeConfig = 
    ReadFromFile "myConfig.fsx"
     ==> ReadFromFile "myConfig2.fsx"

for x in completeConfig do
    printfn "%s from %s => %s" x.Key x.Value.Source x.Value.Version

System.Console.ReadKey() |> ignore
