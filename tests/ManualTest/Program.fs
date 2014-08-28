open Paket

let completeConfig = Config.ReadFromFile "myConfig.fsx"

let resolution = completeConfig.Resolve(Nuget.NugetDiscovery())

printfn "%s" (LockFile.format resolution.DirectDependencies)

System.Console.ReadKey() |> ignore
