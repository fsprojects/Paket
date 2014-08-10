open FSharp.ProjectTemplate.ConfigDSL

let completeConfig = 
    runConfig "myConfig.fsx"
     ==> runConfig "myConfig2.fsx"

for x in completeConfig do
    printfn "%s => %s" x.Key x.Value

System.Console.ReadKey() |> ignore